using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using NebuLog.Contracts;
using NebuLog.Server.Diagnostics;
using NebuLog.Server.Hubs;
using NebuLog.Server.Ingestion;

namespace NebuLog.Server.Api;

/// <summary>Maps the dashboard's read-only REST surface under <c>/api</c>.</summary>
internal static class LogApiEndpoints
{
    /// <summary>The rate-limiting policy applied to the whole <c>/api</c> group.</summary>
    public const string RateLimitPolicy = "api";

    public static IEndpointRouteBuilder MapNebuLogApi(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api")
            .WithTags("NebuLog")
            .RequireRateLimiting(RateLimitPolicy)
            .RequireAuthorization(NebuLogPolicies.Viewer);

        api.MapGet("/logs", GetLogs)
            .WithName("GetLogs")
            .WithSummary("Query the in-memory log buffer.")
            .WithDescription("Returns buffered entries oldest first, filtered by id, severity, service and body text.")
            .Produces<IReadOnlyList<NebuLogEntry>>()
            .ProducesValidationProblem()
            .AddEndpointFilter<LogQueryValidationFilter>();

        api.MapGet("/summary", GetSummary)
            .WithName("GetSummary")
            .WithSummary("Current ingest totals, severity breakdown and per-second rate.")
            .Produces<LiveSummaryDto>();

        api.MapGet("/clients", GetClients)
            .WithName("GetClients")
            .WithSummary("Clients currently connected to the hub.")
            .Produces<IReadOnlyList<ConnectedClientInfo>>();

        api.MapGet("/custom-stats", GetCustomStats)
            .WithName("GetCustomStats")
            .WithSummary("Live statistics declared by producers, with their latest values.")
            .Produces<IReadOnlyList<StatSnapshot>>();

        api.MapGet("/info", GetInfo)
            .WithName("GetInfo")
            .WithSummary("Server version, uptime and buffer occupancy.")
            .Produces<ServerInfoDto>();

        return endpoints;
    }

    private static Ok<IReadOnlyList<NebuLogEntry>> GetLogs(
        LogRingBuffer buffer,
        long? afterId,
        int? minSeverity,
        string? service,
        string? search,
        int? limit)
    {
        var query = new HistoryQuery
        {
            AfterId = afterId,
            MinSeverity = minSeverity,
            Service = service,
            Limit = limit ?? HistoryQuery.DefaultLimit,
        };

        return TypedResults.Ok(buffer.Query(query, search));
    }

    private static Ok<LiveSummaryDto> GetSummary(LiveSummary summary, LogRingBuffer buffer) =>
        TypedResults.Ok(summary.Snapshot(buffer.Count));

    private static Ok<IReadOnlyList<ConnectedClientInfo>> GetClients(ConnectedClientRegistry clients) =>
        TypedResults.Ok(clients.Snapshot());

    private static Ok<IReadOnlyList<StatSnapshot>> GetCustomStats(StatRegistry stats) =>
        TypedResults.Ok(stats.Snapshot());

    private static Ok<ServerInfoDto> GetInfo(
        LogRingBuffer buffer,
        ServerStartTime startTime,
        TimeProvider timeProvider) =>
        TypedResults.Ok(new ServerInfoDto
        {
            Version = typeof(LogApiEndpoints).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? typeof(LogApiEndpoints).Assembly.GetName().Version?.ToString()
                ?? "unknown",
            StartedUnixMs = startTime.StartedUnixMs,
            UptimeSeconds = (long)(timeProvider.GetUtcNow().ToUnixTimeMilliseconds() - startTime.StartedUnixMs) / 1000,
            BufferCapacity = buffer.Capacity,
            BufferedCount = buffer.Count,
        });
}
