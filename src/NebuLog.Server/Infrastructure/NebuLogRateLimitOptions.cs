using System.ComponentModel.DataAnnotations;

namespace NebuLog.Server.Infrastructure;

/// <summary>Rate-limit settings, bound from configuration section <c>NebuLog:RateLimit</c>.</summary>
public sealed class NebuLogRateLimitOptions
{
    /// <summary>The configuration section these options are bound from.</summary>
    public const string SectionName = "NebuLog:RateLimit";

    /// <summary>Name of the token-bucket policy guarding the log receivers.</summary>
    public const string IngestPolicy = "ingest";

    /// <summary>Name of the fixed-window policy guarding the dashboard API.</summary>
    public const string ApiPolicy = "api";

    /// <summary>Tokens added to each client's ingest bucket every second.</summary>
    [Range(1, 1_000_000)]
    public int IngestTokensPerSecond { get; set; } = 50;

    /// <summary>Maximum size of each client's ingest bucket.</summary>
    [Range(1, 1_000_000)]
    public int IngestBucketCapacity { get; set; } = 200;

    /// <summary>Requests each client may make to <c>/api</c> within one window.</summary>
    [Range(1, 1_000_000)]
    public int ApiPermitsPerWindow { get; set; } = 100;

    /// <summary>Length of the <c>/api</c> fixed window.</summary>
    public TimeSpan ApiWindow { get; set; } = TimeSpan.FromSeconds(10);
}
