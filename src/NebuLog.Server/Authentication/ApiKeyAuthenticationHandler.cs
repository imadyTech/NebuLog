using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NebuLog.Server.Identity;

namespace NebuLog.Server.Authentication;

/// <summary>Options for the <c>X-Api-Key</c> authentication scheme.</summary>
public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    /// <summary>The scheme name.</summary>
    public const string SchemeName = "NebuLog.ApiKey";

    /// <summary>The request header carrying the key.</summary>
    public const string HeaderName = "X-Api-Key";

    /// <summary>Claim type holding the key's name.</summary>
    public const string KeyNameClaim = "nebulog:key_name";

    /// <summary>Claim type holding the key's pinned service name.</summary>
    public const string ServiceNameClaim = "nebulog:service_name";
}

/// <summary>
/// Authenticates log producers from the <c>X-Api-Key</c> header, issuing a principal in the
/// <see cref="NebuLogRoles.Producer"/> role.
/// </summary>
public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly ApiKeyService _keys;

    /// <summary>Creates the handler.</summary>
    /// <param name="options">Scheme options.</param>
    /// <param name="logger">Logger factory.</param>
    /// <param name="encoder">URL encoder.</param>
    /// <param name="keys">The key store.</param>
    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ApiKeyService keys)
        : base(options, logger, encoder) => _keys = keys;

    /// <inheritdoc />
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var presented = Context.Request.Headers[ApiKeyAuthenticationOptions.HeaderName].ToString();
        if (string.IsNullOrEmpty(presented))
        {
            return AuthenticateResult.NoResult();
        }

        var key = await _keys.VerifyAsync(presented, Context.RequestAborted).ConfigureAwait(false);
        if (key is null)
        {
            return AuthenticateResult.Fail("The supplied API key is unknown, malformed or revoked.");
        }

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, key.Id.ToString()),
            new Claim(ClaimTypes.Name, key.Name),
            new Claim(ClaimTypes.Role, NebuLogRoles.Producer),
            new Claim(ApiKeyAuthenticationOptions.KeyNameClaim, key.Name),
            .. key.ServiceName is { Length: > 0 } service
                ? new[] { new Claim(ApiKeyAuthenticationOptions.ServiceNameClaim, service) }
                : [],
        ], ApiKeyAuthenticationOptions.SchemeName);

        return AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }

    /// <inheritdoc />
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        // An API client gets a status code, never a redirect to a login page.
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }
}
