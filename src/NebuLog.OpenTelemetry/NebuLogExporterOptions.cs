using System.ComponentModel.DataAnnotations;

namespace NebuLog.OpenTelemetry;

/// <summary>Configuration for the NebuLog SignalR transport and exporter.</summary>
public sealed class NebuLogExporterOptions
{
    /// <summary>
    /// Root address of the NebuLog server, for example <c>https://logs.example.com</c>.
    /// The hub path is appended by the client.
    /// </summary>
    [Required]
    public Uri? Endpoint { get; set; }

    /// <summary>API key sent as the <c>X-Api-Key</c> header on every connection attempt.</summary>
    [Required(AllowEmptyStrings = false)]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Maximum number of entries buffered while the connection is down.</summary>
    [Range(1, 1_000_000)]
    public int QueueCapacity { get; set; } = 10_000;

    /// <summary>Maximum number of entries sent in one hub call.</summary>
    [Range(1, 10_000)]
    public int MaxBatchSize { get; set; } = 500;

    /// <summary>How long the sender waits for a batch to fill before flushing what it has.</summary>
    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>How long <see cref="NebuLogExporter"/> drains its queue during shutdown.</summary>
    public TimeSpan ShutdownDrainTimeout { get; set; } = TimeSpan.FromSeconds(5);
}
