using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using NebuLog.Server.Diagnostics;
using NebuLog.Server.Ingestion;

namespace NebuLog.Server.Api;

/// <summary>
/// The only endpoints an anonymous visitor may call: enough for the landing page to show that the
/// server is alive, and nothing more.
/// </summary>
/// <remarks>
/// Everything here is public to the whole internet, so the rule that shapes it is narrow: no log
/// content, ever. Counts, a rate curve, a service count and an uptime are aggregates — they cannot
/// be turned back into anything a producer logged. Service <em>names</em> are deliberately excluded
/// too: on a private deployment they would disclose the shape of someone's estate.
/// </remarks>
public static class PublicEndpoints
{
    /// <summary>Rate-limiting policy name for the anonymous endpoints.</summary>
    public const string RateLimitPolicy = "public";

    /// <summary>Name of the output-cache policy applied to the summary.</summary>
    public const string CachePolicy = "public-summary";

    /// <summary>Maps the anonymous endpoints under <c>/api/public</c>.</summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapNebuLogPublic(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var api = endpoints.MapGroup("/api/public")
            .WithTags("NebuLog (public)")
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicy);

        api.MapGet("/summary", GetSummary)
            .WithName("GetPublicSummary")
            .WithSummary("Aggregate ingest figures for the landing page.")
            .WithDescription("Anonymous and cached. Returns totals and rates only — never log content.")
            .WithMetadata(new OutputCacheAttribute { PolicyName = CachePolicy })
            .Produces<PublicSummaryDto>();

        api.MapGet("/site-config", GetSiteConfig)
            .WithName("GetSiteConfig")
            .WithSummary("Links the site shows, so they can be changed without rebuilding the page.")
            .WithMetadata(new OutputCacheAttribute { PolicyName = CachePolicy })
            .Produces<SiteConfigDto>();

        return endpoints;
    }

    private static Ok<PublicSummaryDto> GetSummary(
        LiveSummary summary,
        LogRingBuffer buffer,
        ServerStartTime startTime,
        TimeProvider timeProvider)
    {
        var snapshot = summary.Snapshot(buffer.Count);

        return TypedResults.Ok(new PublicSummaryDto
        {
            IngestedTotal = snapshot.TotalIngested,
            RatePerSecondLast60s = snapshot.RatePerSecond,
            // The count, not the names: a count says the server is busy, names say who is using it.
            ServiceCount = snapshot.Services.Count,
            UptimeSeconds = (timeProvider.GetUtcNow().ToUnixTimeMilliseconds()
                - startTime.GetStartedUnixMs(timeProvider)) / 1000,
        });
    }

    private static Ok<SiteConfigDto> GetSiteConfig(IOptions<NebuLogSiteOptions> options) =>
        TypedResults.Ok(new SiteConfigDto
        {
            BlogUrl = options.Value.BlogUrl,
            GitHubUrl = options.Value.GitHubUrl,
        });
}
