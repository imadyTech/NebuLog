namespace NebuLog.Contracts;

/// <summary>Identifies the ingestion path a <see cref="NebuLogEntry"/> arrived through.</summary>
public enum IngestSource
{
    /// <summary>The entry was received over OTLP/HTTP.</summary>
    Otlp = 0,

    /// <summary>The entry was pushed over the SignalR hub.</summary>
    SignalR = 1,
}
