using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using NebuLog.Server.Authentication;
using NebuLog.Server.Hubs;
using NebuLog.Server.Identity;

namespace NebuLog.Server.Api;

/// <summary>Administrator endpoints for issuing and revoking API keys.</summary>
internal static class ApiKeyEndpoints
{
    public static IEndpointRouteBuilder MapNebuLogApiKeys(this IEndpointRouteBuilder endpoints)
    {
        var keys = endpoints.MapGroup("/api/keys")
            .WithTags("API keys")
            .RequireAuthorization(NebuLogPolicies.Admin)
            .AddEndpointFilter<CsrfHeaderFilter>();

        keys.MapGet("/", ListAsync)
            .WithName("ListApiKeys")
            .WithSummary("List issued API keys. Secrets are never returned.")
            .Produces<IReadOnlyList<ApiKeyDto>>();

        keys.MapPost("/", CreateAsync)
            .WithName("CreateApiKey")
            .WithSummary("Issue a new API key.")
            .WithDescription("The clear-text key is returned once and cannot be recovered afterwards.")
            .Produces<CreatedApiKeyDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        keys.MapDelete("/{id:guid}", RevokeAsync)
            .WithName("RevokeApiKey")
            .WithSummary("Revoke an API key. It stops working immediately.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<Ok<IReadOnlyList<ApiKeyDto>>> ListAsync(
        ApiKeyService keys,
        CancellationToken cancellationToken)
    {
        var stored = await keys.ListAsync(cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok<IReadOnlyList<ApiKeyDto>>(
        [
            .. stored.Select(key => new ApiKeyDto
            {
                Id = key.Id,
                Name = key.Name,
                Prefix = key.Prefix,
                ServiceName = key.ServiceName,
                CreatedAt = key.CreatedAt,
                RevokedAt = key.RevokedAt,
                LastUsedAt = key.LastUsedAt,
                CreatedBy = key.CreatedBy,
            }),
        ]);
    }

    private static async Task<Results<Created<CreatedApiKeyDto>, ValidationProblem>> CreateAsync(
        CreateApiKeyRequest request,
        ApiKeyService keys,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["name"] = ["A name is required."],
            });
        }

        var createdBy = principal.FindFirstValue(ClaimTypes.Email)
            ?? principal.FindFirstValue(ClaimTypes.Name)
            ?? "unknown";

        var (key, clearText) = await keys
            .CreateAsync(request.Name.Trim(), request.ServiceName?.Trim(), createdBy, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Created($"/api/keys/{key.Id}", new CreatedApiKeyDto
        {
            Id = key.Id,
            Name = key.Name,
            Prefix = key.Prefix,
            ServiceName = key.ServiceName,
            CreatedAt = key.CreatedAt,
            ApiKey = clearText,
        });
    }

    private static async Task<Results<NoContent, NotFound>> RevokeAsync(
        Guid id,
        ApiKeyService keys,
        CancellationToken cancellationToken) =>
        await keys.RevokeAsync(id, cancellationToken).ConfigureAwait(false)
            ? TypedResults.NoContent()
            : TypedResults.NotFound();
}

/// <summary>A request to issue a key.</summary>
public sealed record CreateApiKeyRequest
{
    /// <summary>Human-readable name for the key.</summary>
    public string? Name { get; init; }

    /// <summary>Optional service this key belongs to.</summary>
    public string? ServiceName { get; init; }
}

/// <summary>An issued key, without any secret material.</summary>
public sealed record ApiKeyDto
{
    /// <summary>The key's id.</summary>
    public Guid Id { get; init; }

    /// <summary>The key's name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The key's public prefix.</summary>
    public string Prefix { get; init; } = string.Empty;

    /// <summary>The service the key belongs to, if pinned.</summary>
    public string? ServiceName { get; init; }

    /// <summary>When the key was issued.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>When the key was revoked, if it has been.</summary>
    public DateTimeOffset? RevokedAt { get; init; }

    /// <summary>When the key last authenticated a request.</summary>
    public DateTimeOffset? LastUsedAt { get; init; }

    /// <summary>Who issued the key.</summary>
    public string CreatedBy { get; init; } = string.Empty;
}

/// <summary>A newly issued key. This is the only time the clear text is available.</summary>
public sealed record CreatedApiKeyDto
{
    /// <summary>The key's id.</summary>
    public Guid Id { get; init; }

    /// <summary>The key's name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The key's public prefix.</summary>
    public string Prefix { get; init; } = string.Empty;

    /// <summary>The service the key belongs to, if pinned.</summary>
    public string? ServiceName { get; init; }

    /// <summary>When the key was issued.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>The clear-text key. Store it now; it cannot be retrieved again.</summary>
    public string ApiKey { get; init; } = string.Empty;
}
