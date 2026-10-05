using System.Diagnostics.Metrics;
using System.Globalization;
using System.Threading.Channels;
using NebuLog.Contracts;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

namespace NebuLog.OpenTelemetry;

/// <summary>
/// Exports <see cref="LogRecord"/>s to a NebuLog server over SignalR.
/// </summary>
/// <remarks>
/// <see cref="Export"/> only maps records into a bounded in-memory queue; all network I/O happens on a
/// background sender loop, so logging never blocks on the server. When the queue is full the entry being
/// written is dropped and counted on the <c>nebulog.client.dropped</c> counter of the
/// <c>NebuLog.Client</c> meter.
/// </remarks>
public sealed class NebuLogExporter : BaseExporter<LogRecord>
{
    /// <summary>Name of the meter that publishes this exporter's own diagnostics.</summary>
    public const string MeterName = "NebuLog.Client";

    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> DroppedCounter = Meter.CreateCounter<long>(
        "nebulog.client.dropped",
        unit: "{entry}",
        description: "Log entries dropped because the client queue was full.");

    private readonly NebuLogExporterOptions _options;
    private readonly Func<NebuLogClientIdentity, INebuLogTransport> _transportFactory;
    private readonly Channel<NebuLogEntry> _queue;
    private readonly CancellationTokenSource _stopping = new();
    private readonly ManualResetEventSlim _drained = new(initialState: false);
    private NebuLogClientIdentity? _identity;
    private bool _disposed;

    /// <summary>Creates an exporter that publishes through the given transport factory.</summary>
    /// <param name="options">Queue, batching and connection settings.</param>
    /// <param name="transportFactory">Supplies the transport once the resource identity is known.</param>
    internal NebuLogExporter(
        NebuLogExporterOptions options,
        Func<NebuLogClientIdentity, INebuLogTransport> transportFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(transportFactory);

        _options = options;
        _transportFactory = transportFactory;
        _queue = Channel.CreateBounded<NebuLogEntry>(
            new BoundedChannelOptions(options.QueueCapacity)
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
                SingleWriter = false,
            },
            itemDropped: _ => DroppedCounter.Add(1));

        _ = Task.Run(() => RunSenderAsync(_stopping.Token));
    }

    /// <inheritdoc />
    public override ExportResult Export(in Batch<LogRecord> batch)
    {
        var identity = _identity ??= ReadIdentity();

        foreach (var record in batch)
        {
            _queue.Writer.TryWrite(Map(record, identity));
        }

        return ExportResult.Success;
    }

    /// <inheritdoc />
    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        _queue.Writer.TryComplete();

        var timeout = timeoutMilliseconds < 0
            ? _options.ShutdownDrainTimeout
            : TimeSpan.FromMilliseconds(timeoutMilliseconds);

        var drained = _drained.Wait(timeout);
        _stopping.Cancel();
        return drained;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _queue.Writer.TryComplete();
            _stopping.Cancel();
            _stopping.Dispose();
            _drained.Dispose();
        }

        base.Dispose(disposing);
    }

    private async Task RunSenderAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SendLoopAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _drained.Set();
        }
    }

    private async Task SendLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new List<NebuLogEntry>(_options.MaxBatchSize);
        var reader = _queue.Reader;

        while (await WaitForEntriesAsync(reader, cancellationToken).ConfigureAwait(false))
        {
            while (buffer.Count < _options.MaxBatchSize && reader.TryRead(out var entry))
            {
                buffer.Add(entry);
            }

            if (buffer.Count == 0)
            {
                continue;
            }

            if (!await TryPublishAsync(buffer, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            buffer.Clear();
        }
    }

    private async Task<bool> TryPublishAsync(List<NebuLogEntry> buffer, CancellationToken cancellationToken)
    {
        try
        {
            var transport = _transportFactory(_identity ?? NebuLogClientIdentity.Unknown);
            await transport.PublishAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception)
        {
            // The batch is lost. The connection reconnects on its own and later batches keep flowing;
            // failing here must never surface back into the logging pipeline.
        }

        return true;
    }

    private async Task<bool> WaitForEntriesAsync(ChannelReader<NebuLogEntry> reader, CancellationToken cancellationToken)
    {
        try
        {
            if (!await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            // Give a partial batch a short window to fill before flushing it.
            await Task.Delay(_options.FlushInterval, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>
    /// Reads <c>service.name</c> and <c>service.instance.id</c> from the OpenTelemetry resource.
    /// Internal so the registry can ask for it when the connection opens before the first export.
    /// </summary>
    internal NebuLogClientIdentity ReadIdentity()
    {
        var resource = ParentProvider?.GetResource() ?? Resource.Empty;
        string? service = null;
        string? instance = null;

        foreach (var attribute in resource.Attributes)
        {
            if (attribute.Key is "service.name")
            {
                service = Stringify(attribute.Value);
            }
            else if (attribute.Key is "service.instance.id")
            {
                instance = Stringify(attribute.Value);
            }
        }

        return service is null
            ? NebuLogClientIdentity.Unknown with { ServiceInstanceId = instance }
            : new NebuLogClientIdentity(service, instance);
    }

    private static NebuLogEntry Map(LogRecord record, NebuLogClientIdentity identity)
    {
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
        if (record.Attributes is { } recordAttributes)
        {
            foreach (var attribute in recordAttributes)
            {
                if (attribute.Key is "{OriginalFormat}" || attribute.Value is null)
                {
                    continue;
                }

                attributes[attribute.Key] = Stringify(attribute.Value);
            }
        }

        return new NebuLogEntry
        {
            TimestampUnixMs = ToUnixMs(record.Timestamp),
            ObservedUnixMs = ToUnixMs(record.ObservedTimestamp),
            SeverityNumber = Severity.FromLogLevel(record.LogLevel),
            SeverityText = record.LogLevel.ToString(),
            Body = record.FormattedMessage ?? record.Body ?? string.Empty,
            ServiceName = identity.ServiceName,
            ServiceInstanceId = identity.ServiceInstanceId,
            ScopeName = record.CategoryName ?? string.Empty,
            TraceId = record.TraceId == default ? null : record.TraceId.ToHexString(),
            SpanId = record.SpanId == default ? null : record.SpanId.ToHexString(),
            EventName = string.IsNullOrEmpty(record.EventId.Name) ? null : record.EventId.Name,
            Attributes = attributes,
            Exception = record.Exception is { } exception
                ? new ExceptionInfo
                {
                    Type = exception.GetType().FullName ?? exception.GetType().Name,
                    Message = exception.Message,
                    StackTrace = exception.StackTrace,
                }
                : null,
            Source = IngestSource.SignalR,
        };
    }

    private static long ToUnixMs(DateTime timestamp)
    {
        var utc = timestamp.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)
            : timestamp.ToUniversalTime();

        return new DateTimeOffset(utc).ToUnixTimeMilliseconds();
    }

    private static string Stringify(object value) => value switch
    {
        string text => text,
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };
}
