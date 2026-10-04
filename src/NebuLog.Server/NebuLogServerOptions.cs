using System.ComponentModel.DataAnnotations;

namespace NebuLog.Server;

/// <summary>Settings for the embeddable NebuLog server, bound from configuration section <c>NebuLog:Server</c>.</summary>
public sealed class NebuLogServerOptions
{
    /// <summary>The configuration section these options are bound from.</summary>
    public const string SectionName = "NebuLog:Server";

    /// <summary>How many entries the in-memory ring buffer holds before the oldest are overwritten.</summary>
    [Range(1_000, 1_000_000)]
    public int BufferCapacity { get; set; } = 50_000;

    /// <summary>How many entries may wait between the receivers and the pipeline before ingest is rejected.</summary>
    [Range(1_000, int.MaxValue)]
    public int IngestQueueCapacity { get; set; } = 100_000;

    /// <summary>How long the pipeline lets a broadcast batch fill before sending it.</summary>
    public TimeSpan BroadcastInterval { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>The largest number of entries sent to dashboards in one broadcast.</summary>
    [Range(1, 10_000)]
    public int MaxBroadcastBatch { get; set; } = 1_000;

    /// <summary>Attributes beyond this count are dropped from an entry during normalisation.</summary>
    [Range(0, 1_000)]
    public int MaxAttributesPerEntry { get; set; } = 64;

    /// <summary>
    /// Largest hub message the server will accept, in bytes.
    /// </summary>
    /// <remarks>
    /// SignalR defaults to 32 KiB, which a single <c>PublishLogs</c> call carrying a few hundred
    /// entries exceeds — the connection is then closed rather than the batch rejected. The default
    /// here is sized for the client exporter's largest batch.
    /// </remarks>
    [Range(32 * 1024, 64 * 1024 * 1024)]
    public int MaxHubMessageBytes { get; set; } = 4 * 1024 * 1024;

    /// <summary>Attribute values longer than this are truncated during normalisation.</summary>
    [Range(16, 1_000_000)]
    public int MaxAttributeValueLength { get; set; } = 8_192;
}
