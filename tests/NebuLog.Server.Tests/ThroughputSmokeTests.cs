using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;
using NebuLog.Contracts;
using NebuLog.Server.Api;
using NebuLog.Server.Identity;
using Xunit;

namespace NebuLog.Server.Tests;

public sealed class ThroughputSmokeTests
{
    private const int TargetPerSecond = 10_000;
    private const int DurationSeconds = 5;
    private const int BatchSize = 500;
    private const int TotalEntries = TargetPerSecond * DurationSeconds;

    [Fact]
    public async Task SustainsTenThousandEntriesPerSecondForFiveSeconds()
    {
        var token = TestContext.Current.CancellationToken;
        await using var factory = new NebuLogAppFactory(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["NebuLog:Server:BufferCapacity"] = "60000",
            ["NebuLog:Server:IngestQueueCapacity"] = "100000",
            ["NebuLog:RateLimit:ApiPermitsPerWindow"] = "1000",
        });

        await using var viewer = await factory.ConnectViewerAsync();
        await using var producer = await factory.ConnectProducerAsync("load");

        var deliveredToViewer = 0;
        var latencies = new List<double>(TotalEntries / BatchSize);
        viewer.On<IReadOnlyList<NebuLogEntry>>(HubRoutes.ReceiveLogs, batch =>
        {
            Interlocked.Add(ref deliveredToViewer, batch.Count);

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            lock (latencies)
            {
                latencies.Add(now - batch[0].TimestampUnixMs);
            }
        });

        var batchesPerSecond = TargetPerSecond / BatchSize;
        var interval = TimeSpan.FromSeconds(1.0 / batchesPerSecond);
        var published = 0;
        var stopwatch = Stopwatch.StartNew();

        for (var second = 0; second < DurationSeconds; second++)
        {
            for (var i = 0; i < batchesPerSecond; i++)
            {
                var dueAt = TimeSpan.FromTicks(interval.Ticks * ((second * batchesPerSecond) + i));
                var wait = dueAt - stopwatch.Elapsed;
                if (wait > TimeSpan.Zero)
                {
                    await Task.Delay(wait, token);
                }

                // A rejected batch surfaces as HubException("ingest queue full"); letting it throw
                // is the assertion that the pipeline kept up.
                await producer.InvokeAsync(HubRoutes.PublishLogs, NextBatch(), token);
                published += BatchSize;
            }
        }

        stopwatch.Stop();

        Assert.Equal(TotalEntries, published);

        var achieved = published / stopwatch.Elapsed.TotalSeconds;
        Assert.True(
            achieved >= TargetPerSecond * 0.8,
            $"Only sustained {achieved:N0} entries/s against a target of {TargetPerSecond:N0}.");

        // Let the pipeline drain what is still queued before inspecting the buffer.
        using var client = await factory.SignInAsAsync(NebuLogRoles.Viewer);
        var buffered = 0;
        for (var attempt = 0; attempt < 50 && buffered < TotalEntries; attempt++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), token);
            buffered = (await client.GetFromJsonAsync<ServerInfoDto>("/api/info", token))!.BufferedCount;
        }

        Assert.Equal(TotalEntries, buffered);

        var summary = await client.GetFromJsonAsync<LiveSummaryDto>("/api/summary", token);
        Assert.Equal(TotalEntries, summary!.TotalIngested);

        double p50, p95;
        lock (latencies)
        {
            Assert.NotEmpty(latencies);
            latencies.Sort();
            p50 = Percentile(latencies, 0.50);
            p95 = Percentile(latencies, 0.95);
        }

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"published={published:N0} in {stopwatch.Elapsed.TotalSeconds:N2}s " +
            $"({achieved:N0}/s); broadcasts={latencies.Count:N0}; " +
            $"delivered={Volatile.Read(ref deliveredToViewer):N0}; latency p50={p50:N0}ms p95={p95:N0}ms");

        Assert.True(p95 < 1_000, $"Broadcast latency p95 was {p95:N0} ms.");
    }

    private static NebuLogEntry[] NextBatch()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var batch = new NebuLogEntry[BatchSize];

        for (var i = 0; i < BatchSize; i++)
        {
            batch[i] = new NebuLogEntry
            {
                Body = "load entry",
                SeverityNumber = Severity.Info,
                ServiceName = "load",
                TimestampUnixMs = now,
                ObservedUnixMs = now,
            };
        }

        return batch;
    }

    private static double Percentile(List<double> sorted, double percentile)
    {
        var index = (int)Math.Ceiling(percentile * sorted.Count) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
    }
}
