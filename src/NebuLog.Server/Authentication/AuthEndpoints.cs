using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using NebuLog.Server.Identity;
using NebuLog.Server.Infrastructure;

namespace NebuLog.Server.Authentication;

/// <summary>
/// The minimal sign-in surface for dashboard users.
/// </summary>
/// <remarks>
/// Written by hand rather than with <c>MapIdentityApi</c>, which would also publish registration,
/// password-reset, two-factor and email-confirmation endpoints that NebuLog neither needs nor wants
/// exposed.
/// </remarks>
internal static class AuthEndpoints
{
    /// <summary>The rate-limiting policy guarding sign-in attempts.</summary>
    public const string RateLimitPolicy = "auth";

    public static IEndpointRouteBuilder MapNebuLogAuth(this IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup("/api/auth")
            .WithTags("Authentication")
            .AddEndpointFilter<CsrfHeaderFilter>();

        auth.MapPost("/login", LoginAsync)
            .WithName("Login")
            .WithSummary("Sign in with email and password.")
            .RequireRateLimiting(RateLimitPolicy)
            .AllowAnonymous()
            .Produces<CurrentUserDto>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status423Locked);

        auth.MapPost("/demo", DemoAsync)
            .WithName("LoginAsDemo")
            .WithSummary("Sign in as the read-only demo user, when the demo account is enabled.")
            .RequireRateLimiting(RateLimitPolicy)
            .AllowAnonymous()
            .Produces<CurrentUserDto>()
            .Produces(StatusCodes.Status404NotFound);

        auth.MapPost("/logout", LogoutAsync)
            .WithName("Logout")
            .WithSummary("Sign out of the current session.")
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent);

        auth.MapGet("/me", GetMe)
            .WithName("GetCurrentUser")
            .WithSummary("The signed-in user and their roles.")
            .RequireAuthorization()
            .Produces<CurrentUserDto>()
            .Produces(StatusCodes.Status401Unauthorized);

        return endpoints;
    }

    private static async Task<Results<Ok<CurrentUserDto>, UnauthorizedHttpResult, StatusCodeHttpResult>> LoginAsync(
        LoginRequest request,
        SignInManager<NebuLogUser> signInManager,
        UserManager<NebuLogUser> userManager)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await signInManager.PasswordSignInAsync(
            request.Email ?? string.Empty,
            request.Password ?? string.Empty,
            isPersistent: false,
            lockoutOnFailure: true).ConfigureAwait(false);

        if (result.IsLockedOut)
        {
            return TypedResults.StatusCode(StatusCodes.Status423Locked);
        }

        if (!result.Succeeded)
        {
            return TypedResults.Unauthorized();
        }

        var user = await userManager.FindByEmailAsync(request.Email!).ConfigureAwait(false);
        return TypedResults.Ok(await DescribeAsync(user!, userManager).ConfigureAwait(false));
    }

    private static async Task<Results<Ok<CurrentUserDto>, NotFound>> DemoAsync(
        SignInManager<NebuLogUser> signInManager,
        UserManager<NebuLogUser> userManager,
        IOptions<NebuLogDemoOptions> demoOptions)
    {
        if (!demoOptions.Value.Enabled)
        {
            return TypedResults.NotFound();
        }

        var user = await userManager.FindByEmailAsync(NebuLogDemoOptions.DemoEmail).ConfigureAwait(false);
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        await signInManager.SignInAsync(user, isPersistent: false).ConfigureAwait(false);
        return TypedResults.Ok(await DescribeAsync(user, userManager).ConfigureAwait(false));
    }

    private static async Task<NoContent> LogoutAsync(SignInManager<NebuLogUser> signInManager)
    {
        await signInManager.SignOutAsync().ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static Ok<CurrentUserDto> GetMe(ClaimsPrincipal principal) =>
        TypedResults.Ok(new CurrentUserDto
        {
            Email = principal.FindFirstValue(ClaimTypes.Email)
                ?? principal.FindFirstValue(ClaimTypes.Name)
                ?? string.Empty,
            Roles = [.. principal.FindAll(ClaimTypes.Role).Select(claim => claim.Value).Order(StringComparer.Ordinal)],
        });

    private static async Task<CurrentUserDto> DescribeAsync(NebuLogUser user, UserManager<NebuLogUser> userManager) =>
        new()
        {
            Email = user.Email ?? string.Empty,
            Roles = [.. (await userManager.GetRolesAsync(user).ConfigureAwait(false)).Order(StringComparer.Ordinal)],
        };
}

/// <summary>A sign-in attempt.</summary>
public sealed record LoginRequest
{
    /// <summary>The user's email address.</summary>
    public string? Email { get; init; }

    /// <summary>The user's password.</summary>
    public string? Password { get; init; }
}

/// <summary>Who the caller is signed in as.</summary>
public sealed record CurrentUserDto
{
    /// <summary>The signed-in user's email address.</summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>The roles the user holds, sorted.</summary>
    public IReadOnlyList<string> Roles { get; init; } = [];
}
