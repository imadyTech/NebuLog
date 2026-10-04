namespace NebuLog.Server.Ingestion;

/// <summary>The outcome of one call to <see cref="ILogIngestor.TryIngest"/>.</summary>
/// <param name="Accepted">How many entries were enqueued.</param>
/// <param name="Rejected">How many entries were dropped because the ingest queue was full.</param>
public readonly record struct IngestResult(int Accepted, int Rejected)
{
    /// <summary>True when at least one entry could not be enqueued.</summary>
    public bool IsRejected => Rejected > 0;
}
