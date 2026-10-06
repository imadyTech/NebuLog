namespace NebuLog.Server.Api;

/// <summary>
/// What an anonymous caller may learn about this server.
/// </summary>
/// <remarks>
/// Aggregates only. There is deliberately no field here that could carry a log body, an attribute,
/// a service name or an account — see <see cref="PublicEndpoints"/>. A test asserts the exact field
/// set, so adding one is a conscious act rather than an accident.
/// </remarks>
public sealed record PublicSummaryDto
{
    /// <summary>Entries ingested since the server started.</summary>
    public long IngestedTotal { get; init; }

    /// <summary>Entries received in each of the last 60 seconds, oldest first.</summary>
    public IReadOnlyList<int> RatePerSecondLast60s { get; init; } = [];

    /// <summary>How many distinct services have reported. A count, not the names.</summary>
    public int ServiceCount { get; init; }

    /// <summary>Seconds since the host started.</summary>
    public long UptimeSeconds { get; init; }
}

/// <summary>Links the site renders, resolved at runtime rather than baked into the build.</summary>
public sealed record SiteConfigDto
{
    /// <summary>Where the "Blog" link points, or empty to hide it.</summary>
    public string BlogUrl { get; init; } = string.Empty;

    /// <summary>Where the "GitHub" link points.</summary>
    public string GitHubUrl { get; init; } = string.Empty;
}
