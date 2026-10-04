using NebuLog.Contracts;

namespace NebuLog.OpenTelemetry;

/// <summary>The outbound channel the exporter hands finished batches to.</summary>
/// <remarks>Exists so tests can substitute the SignalR connection with a fake.</remarks>
public interface INebuLogTransport
{
    /// <summary>Publishes a batch of entries to the NebuLog server.</summary>
    /// <param name="entries">The entries to publish; never empty.</param>
    /// <param name="cancellationToken">Cancels the publish attempt.</param>
    Task PublishAsync(IReadOnlyList<NebuLogEntry> entries, CancellationToken cancellationToken);
}
