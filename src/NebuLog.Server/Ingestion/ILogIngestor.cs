using NebuLog.Contracts;

namespace NebuLog.Server.Ingestion;

/// <summary>The single entry point every receiver uses to hand entries to the server pipeline.</summary>
/// <remarks>
/// Both ingestion paths — the SignalR hub and the OTLP/HTTP receiver — converge here, so
/// normalisation, sequencing and back-pressure are decided in exactly one place.
/// </remarks>
public interface ILogIngestor
{
    /// <summary>Normalises and enqueues a batch of entries without blocking.</summary>
    /// <param name="entries">The entries to ingest.</param>
    /// <param name="source">The path the entries arrived through.</param>
    /// <returns>
    /// How many entries were accepted, and whether the batch was rejected because the queue is full.
    /// Callers map a rejection to HTTP 503 or a hub error.
    /// </returns>
    IngestResult TryIngest(IReadOnlyList<NebuLogEntry> entries, IngestSource source);
}
