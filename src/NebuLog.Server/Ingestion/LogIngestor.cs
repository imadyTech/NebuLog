using System.Threading.Channels;
using Microsoft.Extensions.Options;
using NebuLog.Contracts;
using NebuLog.Server.Diagnostics;

namespace NebuLog.Server.Ingestion;

/// <summary>
/// Normalises incoming entries and hands them to the pipeline through a bounded channel.
/// </summary>
/// <remarks>
/// The channel is created with <see cref="BoundedChannelFullMode.Wait"/>, but this class only ever
/// calls <see cref="ChannelWriter{T}.TryWrite"/>: a producer that outruns the pipeline is told so
/// immediately rather than being blocked, which keeps back-pressure visible at the edge.
/// </remarks>
internal sealed class LogIngestor : ILogIngestor
{
    /// <summary>The service name given to entries that arrive without one.</summary>
    public const string UnknownServiceName = "unknown_service";

    private readonly Channel<NebuLogEntry> _channel;
    private readonly NebuLogServerOptions _options;
    private readonly ServerMetrics _metrics;
    private readonly TimeProvider _timeProvider;

    public LogIngestor(IOptions<NebuLogServerOptions> options, ServerMetrics metrics, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
        _metrics = metrics;
        _timeProvider = timeProvider;
        _channel = Channel.CreateBounded<NebuLogEntry>(new BoundedChannelOptions(_options.IngestQueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    /// <summary>The queue the pipeline drains.</summary>
    public ChannelReader<NebuLogEntry> Reader => _channel.Reader;

    /// <summary>How full the ingest queue is, as a fraction between 0 and 1.</summary>
    public double QueueUtilisation => (double)_channel.Reader.Count / _options.IngestQueueCapacity;

    /// <inheritdoc />
    public IngestResult TryIngest(IReadOnlyList<NebuLogEntry> entries, IngestSource source)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var accepted = 0;
        var rejected = 0;

        foreach (var entry in entries)
        {
            if (_channel.Writer.TryWrite(Normalise(entry, source)))
            {
                accepted++;
            }
            else
            {
                rejected++;
            }
        }

        _metrics.RecordIngested(accepted, source);
        _metrics.RecordRejected(rejected, source);

        return new IngestResult(accepted, rejected);
    }

    private NebuLogEntry Normalise(NebuLogEntry entry, IngestSource source)
    {
        var observed = entry.ObservedUnixMs > 0
            ? entry.ObservedUnixMs
            : _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();

        return entry with
        {
            Id = 0,
            Source = source,
            ObservedUnixMs = observed,
            TimestampUnixMs = entry.TimestampUnixMs > 0 ? entry.TimestampUnixMs : observed,
            ServiceName = string.IsNullOrWhiteSpace(entry.ServiceName) ? UnknownServiceName : entry.ServiceName,
            Attributes = NormaliseAttributes(entry.Attributes),
        };
    }

    private IReadOnlyDictionary<string, string> NormaliseAttributes(IReadOnlyDictionary<string, string> attributes)
    {
        if (attributes.Count == 0)
        {
            return attributes;
        }

        var needsWork = attributes.Count > _options.MaxAttributesPerEntry;
        if (!needsWork)
        {
            foreach (var value in attributes.Values)
            {
                if (value.Length > _options.MaxAttributeValueLength)
                {
                    needsWork = true;
                    break;
                }
            }
        }

        if (!needsWork)
        {
            return attributes;
        }

        var normalised = new Dictionary<string, string>(
            Math.Min(attributes.Count, _options.MaxAttributesPerEntry),
            StringComparer.Ordinal);

        foreach (var pair in attributes)
        {
            if (normalised.Count >= _options.MaxAttributesPerEntry)
            {
                break;
            }

            normalised[pair.Key] = pair.Value.Length > _options.MaxAttributeValueLength
                ? pair.Value[.._options.MaxAttributeValueLength]
                : pair.Value;
        }

        return normalised;
    }
}
