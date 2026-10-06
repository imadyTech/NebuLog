using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using NebuLog.Contracts;
using OpenTelemetry;
using OpenTelemetry.Logs;
using Xunit;

namespace NebuLog.OpenTelemetry.Tests;

public sealed class ExporterQueueTests
{
    private static NebuLogExporterOptions OptionsWith(int queueCapacity, TimeSpan flushInterval) => new()
    {
        Endpoint = new Uri("https://logs.example.test"),
        ApiKey = "k",
        QueueCapacity = queueCapacity,
        FlushInterval = flushInterval,
    };

    private static ILoggerFactory CreatePipeline(NebuLogExporter exporter) =>
        LoggerFactory.Create(builder => builder
            .SetMinimumLevel(LogLevel.Trace)
            .AddOpenTelemetry(options => options.AddProcessor(new SimpleLogRecordExportProcessor(exporter))));

    [Fact]
    public void ExportReturnsImmediatelyWhileTransportBlocks()
    {
        var blocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = new FakeTransport { Gate = blocked };
        using var exporter = new NebuLogExporter(
            OptionsWith(10_000, TimeSpan.FromMilliseconds(1)),
            _ => transport);

        using var factory = CreatePipeline(exporter);
        var logger = factory.CreateLogger("Queue");

        // Let the sender loop pick up a first batch and stall inside the transport.
        logger.LogInformation("warm up");
        Thread.Sleep(50);

        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < 1_000; i++)
        {
            logger.LogInformation("entry {Index}", i);
        }

        stopwatch.Stop();
        blocked.SetResult();

        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(1),
            $"Logging blocked for {stopwatch.Elapsed} while the transport was stalled.");
    }

    [Fact]
    public void DropsAndCountsEntriesWhenQueueIsFull()
    {
        var dropped = 0L;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == NebuLogExporter.MeterName &&
                instrument.Name == "nebulog.client.dropped")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => Interlocked.Add(ref dropped, value));
        listener.Start();

        var blocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = new FakeTransport { Gate = blocked };
        using var exporter = new NebuLogExporter(
            OptionsWith(8, TimeSpan.FromMinutes(5)),
            _ => transport);

        using (var factory = CreatePipeline(exporter))
        {
            var logger = factory.CreateLogger("Queue");
            for (var i = 0; i < 200; i++)
            {
                logger.LogInformation("entry {Index}", i);
            }
        }

        blocked.SetResult();

        Assert.True(dropped > 0, "Expected the full queue to drop entries and count them.");
    }

    [Fact]
    public async Task SenderDeliversQueuedEntries()
    {
        var transport = new FakeTransport();
        using var exporter = new NebuLogExporter(
            OptionsWith(10_000, TimeSpan.FromMilliseconds(5)),
            _ => transport);

        using (var factory = CreatePipeline(exporter))
        {
            var logger = factory.CreateLogger("Queue");
            logger.LogInformation("one");
            logger.LogInformation("two");
            logger.LogInformation("three");
        }

        await transport.FirstBatchReceived.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal(3, transport.Entries.Count);
        Assert.All(transport.Entries, entry => Assert.Equal(IngestSource.SignalR, entry.Source));
    }
}
