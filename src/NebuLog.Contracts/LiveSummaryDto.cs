using System.Collections.Generic;

namespace NebuLog.Contracts;

/// <summary>A rolling snapshot of what the server has ingested, pushed to dashboards once a second.</summary>
public sealed record LiveSummaryDto
{
    /// <summary>Total entries ingested since the server started.</summary>
    public long TotalIngested { get; init; }

    /// <summary>Cumulative entry counts keyed by severity band (see <see cref="Severity.Band"/>).</summary>
    public IReadOnlyDictionary<string, long> CountsByBand { get; init; } = new Dictionary<string, long>(0);

    /// <summary>
    /// Entries received in each of the last 60 seconds, oldest first. The final element is the
    /// second currently in progress and therefore still incomplete.
    /// </summary>
    public IReadOnlyList<int> RatePerSecond { get; init; } = [];

    /// <summary>Every <c>service.name</c> seen since the server started, sorted ordinally.</summary>
    public IReadOnlyList<string> Services { get; init; } = [];

    /// <summary>Entries currently held in the ring buffer.</summary>
    public int BufferedCount { get; init; }

    /// <summary>When this snapshot was taken, in Unix epoch milliseconds.</summary>
    public long TimestampUnixMs { get; init; }
}
