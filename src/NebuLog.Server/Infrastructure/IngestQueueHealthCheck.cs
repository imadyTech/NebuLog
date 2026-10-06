using Microsoft.Extensions.Diagnostics.HealthChecks;
using NebuLog.Server.Ingestion;

namespace NebuLog.Server.Infrastructure;

/// <summary>Reports the server unready once the ingest queue is more than 90% full.</summary>
internal sealed class IngestQueueHealthCheck : IHealthCheck
{
    /// <summary>The name this check is registered under.</summary>
    public const string Name = "ingest-queue";

    /// <summary>The tag that selects the readiness set.</summary>
    public const string ReadyTag = "ready";

    private const double UnhealthyThreshold = 0.9;

    private readonly ILogIngestor _ingestor;

    public IngestQueueHealthCheck(ILogIngestor ingestor) => _ingestor = ingestor;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var utilisation = ((LogIngestor)_ingestor).QueueUtilisation;
        var description = $"Ingest queue is {utilisation:P1} full.";

        return Task.FromResult(utilisation < UnhealthyThreshold
            ? HealthCheckResult.Healthy(description)
            : HealthCheckResult.Unhealthy(description));
    }
}
