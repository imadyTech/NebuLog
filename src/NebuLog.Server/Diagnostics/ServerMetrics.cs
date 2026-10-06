using System.Diagnostics;
using System.Diagnostics.Metrics;
using NebuLog.Contracts;

namespace NebuLog.Server.Diagnostics;

/// <summary>Publishes the server's own metrics and activity source.</summary>
public sealed class ServerMetrics : IDisposable
{
    /// <summary>Name of the meter and activity source the server publishes under.</summary>
    public const string Name = "NebuLog.Server";

    private readonly Meter _meter;
    private readonly Counter<long> _ingested;
    private readonly Counter<long> _rejected;
    private readonly Histogram<int> _broadcastBatchSize;
    private bool _disposed;

    /// <summary>Creates the meter, instruments and activity source.</summary>
    /// <param name="meterFactory">The factory the host uses, so the meter participates in DI-scoped metering.</param>
    public ServerMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        _meter = meterFactory.Create(Name);
        _ingested = _meter.CreateCounter<long>(
            "nebulog.logs.ingested", unit: "{entry}", description: "Log entries accepted into the pipeline.");
        _rejected = _meter.CreateCounter<long>(
            "nebulog.logs.rejected", unit: "{entry}", description: "Log entries rejected because the ingest queue was full.");
        _broadcastBatchSize = _meter.CreateHistogram<int>(
            "nebulog.broadcast.batch_size", unit: "{entry}", description: "Entries per broadcast to dashboards.");
    }

    /// <summary>The activity source that wraps each ingest batch.</summary>
    public static ActivitySource ActivitySource { get; } = new(Name);

    /// <summary>Registers the gauges whose values are read on demand.</summary>
    /// <param name="bufferedCount">Reads the current ring-buffer occupancy.</param>
    /// <param name="viewerCount">Reads the current number of connected dashboards.</param>
    /// <param name="producerCount">Reads the current number of connected producers.</param>
    public void RegisterGauges(Func<int> bufferedCount, Func<int> viewerCount, Func<int> producerCount)
    {
        ArgumentNullException.ThrowIfNull(bufferedCount);
        ArgumentNullException.ThrowIfNull(viewerCount);
        ArgumentNullException.ThrowIfNull(producerCount);

        _meter.CreateObservableGauge(
            "nebulog.buffer.count", bufferedCount, unit: "{entry}", description: "Entries held in the ring buffer.");
        _meter.CreateObservableGauge(
            "nebulog.clients.viewers", viewerCount, unit: "{client}", description: "Connected dashboards.");
        _meter.CreateObservableGauge(
            "nebulog.clients.producers", producerCount, unit: "{client}", description: "Connected producers.");
    }

    /// <summary>Counts entries accepted from one source.</summary>
    /// <param name="count">How many entries were accepted.</param>
    /// <param name="source">The ingestion path.</param>
    public void RecordIngested(int count, IngestSource source)
    {
        if (count > 0)
        {
            _ingested.Add(count, new KeyValuePair<string, object?>("source", source.ToString()));
        }
    }

    /// <summary>Counts entries rejected from one source.</summary>
    /// <param name="count">How many entries were rejected.</param>
    /// <param name="source">The ingestion path.</param>
    public void RecordRejected(int count, IngestSource source)
    {
        if (count > 0)
        {
            _rejected.Add(count, new KeyValuePair<string, object?>("source", source.ToString()));
        }
    }

    /// <summary>Records the size of one broadcast to dashboards.</summary>
    /// <param name="size">Entries in the broadcast.</param>
    public void RecordBroadcast(int size) => _broadcastBatchSize.Record(size);

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _meter.Dispose();
    }
}
