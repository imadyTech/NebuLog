using System.Diagnostics.Metrics;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NebuLog.Contracts;
using NebuLog.Server;
using NebuLog.Server.Diagnostics;
using NebuLog.Server.Ingestion;
using Xunit;

namespace NebuLog.Server.Tests;

public sealed class IngestNormalisationTests
{
    private static (LogIngestor Ingestor, FakeTimeProvider Time) CreateIngestor(
        Action<NebuLogServerOptions>? configure = null)
    {
        var options = new NebuLogServerOptions { MaxAttributesPerEntry = 3, MaxAttributeValueLength = 8 };
        configure?.Invoke(options);

        var time = new FakeTimeProvider(DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000));
        var metrics = new ServerMetrics(new DummyMeterFactory());

        return (new LogIngestor(Options.Create(options), metrics, time), time);
    }

    private static NebuLogEntry ReadOne(LogIngestor ingestor)
    {
        Assert.True(ingestor.Reader.TryRead(out var entry));
        return entry;
    }

    [Fact]
    public void FillsInTheServiceNameWhenMissing()
    {
        var (ingestor, _) = CreateIngestor();

        ingestor.TryIngest([new NebuLogEntry { ServiceName = "  " }], IngestSource.Otlp);

        Assert.Equal(LogIngestor.UnknownServiceName, ReadOne(ingestor).ServiceName);
    }

    [Fact]
    public void FillsInTimestampsFromTheClock()
    {
        var (ingestor, time) = CreateIngestor();

        ingestor.TryIngest([new NebuLogEntry()], IngestSource.Otlp);

        var entry = ReadOne(ingestor);
        var now = time.GetUtcNow().ToUnixTimeMilliseconds();
        Assert.Equal(now, entry.ObservedUnixMs);
        Assert.Equal(now, entry.TimestampUnixMs);
    }

    [Fact]
    public void KeepsTimestampsTheProducerSupplied()
    {
        var (ingestor, _) = CreateIngestor();

        ingestor.TryIngest(
            [new NebuLogEntry { TimestampUnixMs = 111, ObservedUnixMs = 222 }],
            IngestSource.SignalR);

        var entry = ReadOne(ingestor);
        Assert.Equal(111, entry.TimestampUnixMs);
        Assert.Equal(222, entry.ObservedUnixMs);
    }

    [Fact]
    public void TruncatesLongAttributeValuesAndCapsTheirCount()
    {
        var (ingestor, _) = CreateIngestor();

        ingestor.TryIngest(
            [
                new NebuLogEntry
                {
                    Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["a"] = new('x', 50),
                        ["b"] = "short",
                        ["c"] = "short",
                        ["d"] = "short",
                        ["e"] = "short",
                    },
                },
            ],
            IngestSource.Otlp);

        var entry = ReadOne(ingestor);
        Assert.Equal(3, entry.Attributes.Count);
        Assert.All(entry.Attributes.Values, value => Assert.True(value.Length <= 8));
    }

    [Fact]
    public void LeavesConformingAttributesUntouched()
    {
        var (ingestor, _) = CreateIngestor();
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal) { ["a"] = "ok" };

        ingestor.TryIngest([new NebuLogEntry { Attributes = attributes }], IngestSource.Otlp);

        Assert.Same(attributes, ReadOne(ingestor).Attributes);
    }

    [Fact]
    public void StampsTheIngestSourceAndClearsTheClientSuppliedId()
    {
        var (ingestor, _) = CreateIngestor();

        ingestor.TryIngest([new NebuLogEntry { Id = 999, Source = IngestSource.SignalR }], IngestSource.Otlp);

        var entry = ReadOne(ingestor);
        Assert.Equal(0, entry.Id);
        Assert.Equal(IngestSource.Otlp, entry.Source);
    }

    [Fact]
    public void RejectsEntriesOnceTheQueueIsFull()
    {
        var (ingestor, _) = CreateIngestor(options => options.IngestQueueCapacity = 1_000);

        var entries = Enumerable.Range(0, 1_200).Select(_ => new NebuLogEntry()).ToArray();
        var result = ingestor.TryIngest(entries, IngestSource.Otlp);

        Assert.True(result.IsRejected);
        Assert.Equal(1_000, result.Accepted);
        Assert.Equal(200, result.Rejected);
    }

    private sealed class DummyMeterFactory : IMeterFactory
    {
        private readonly List<Meter> _meters = [];

        public Meter Create(MeterOptions options)
        {
            var meter = new Meter(options);
            _meters.Add(meter);
            return meter;
        }

        public void Dispose()
        {
            foreach (var meter in _meters)
            {
                meter.Dispose();
            }
        }
    }
}
