using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using NebuLog.Contracts;
using NebuLog.Server.Identity;
using Xunit;

namespace NebuLog.Server.Tests;

public sealed class HubIntegrationTests
{
    private static NebuLogEntry Entry(string body, int severity = Severity.Info, string service = "orders") =>
        new() { Body = body, SeverityNumber = severity, ServiceName = service };

    [Fact]
    public async Task PublishedLogsReachViewersInOneBatch()
    {
        await using var factory = new NebuLogAppFactory();
        await using var viewer = await factory.ConnectViewerAsync();
        await using var producer = await factory.ConnectProducerAsync("orders");

        var batches = new List<IReadOnlyList<NebuLogEntry>>();
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        viewer.On<IReadOnlyList<NebuLogEntry>>(HubRoutes.ReceiveLogs, batch =>
        {
            lock (batches)
            {
                batches.Add(batch);
            }

            received.TrySetResult();
        });

        var stopwatch = Stopwatch.StartNew();
        await producer.InvokeAsync(
            HubRoutes.PublishLogs,
            new[] { Entry("one"), Entry("two"), Entry("three") },
            TestContext.Current.CancellationToken);

        await received.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        stopwatch.Stop();

        lock (batches)
        {
            // One broadcast carrying three entries, not three broadcasts.
            var batch = Assert.Single(batches);
            Assert.Equal(["one", "two", "three"], batch.Select(entry => entry.Body));
            Assert.Equal([1L, 2L, 3L], batch.Select(entry => entry.Id));
            Assert.All(batch, entry => Assert.Equal(IngestSource.SignalR, entry.Source));
        }

        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(1),
            $"End-to-end latency was {stopwatch.ElapsedMilliseconds} ms.");
    }

    [Fact]
    public async Task StreamHistoryReplaysBufferedEntries()
    {
        await using var factory = new NebuLogAppFactory();
        await using var viewer = await factory.ConnectViewerAsync();
        await using var producer = await factory.ConnectProducerAsync("orders");

        await PublishAndWaitAsync(viewer, producer, [Entry("alpha"), Entry("beta", Severity.Error)]);

        var replayed = new List<NebuLogEntry>();
        var stream = viewer.StreamAsync<NebuLogEntry>(
            HubRoutes.StreamHistory,
            new HistoryQuery { MinSeverity = Severity.Error },
            TestContext.Current.CancellationToken);

        await foreach (var entry in stream.WithCancellation(TestContext.Current.CancellationToken))
        {
            replayed.Add(entry);
        }

        var only = Assert.Single(replayed);
        Assert.Equal("beta", only.Body);
    }

    [Fact]
    public async Task SendCommandReachesTheTargetProducer()
    {
        await using var factory = new NebuLogAppFactory();

        // SendCommand is the one hub method that needs more than Viewer.
        await using var viewer = await factory.ConnectViewerAsync(NebuLogRoles.Operator);
        await using var producer = await factory.ConnectProducerAsync("orders");

        var delivered = new TaskCompletionSource<NebuLogCommand>(TaskCreationOptions.RunContinuationsAsynchronously);
        producer.On<NebuLogCommand>(HubRoutes.ReceiveCommand, command => delivered.TrySetResult(command));

        await viewer.InvokeAsync(
            HubRoutes.SendCommand,
            producer.ConnectionId,
            new NebuLogCommand { Name = "flush", IssuedBy = "frank" },
            TestContext.Current.CancellationToken);

        var command = await delivered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal("flush", command.Name);
        Assert.Equal("frank", command.IssuedBy);
    }

    [Fact]
    public async Task SendCommandToAnUnknownTargetFails()
    {
        await using var factory = new NebuLogAppFactory();
        await using var viewer = await factory.ConnectViewerAsync(NebuLogRoles.Operator);

        var error = await Assert.ThrowsAsync<HubException>(() => viewer.InvokeAsync(
            HubRoutes.SendCommand,
            "no-such-connection",
            new NebuLogCommand { Name = "flush" },
            TestContext.Current.CancellationToken));

        Assert.Contains("no connected producer", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StatsAreBroadcastAndReadable()
    {
        await using var factory = new NebuLogAppFactory();
        await using var viewer = await factory.ConnectViewerAsync();
        await using var producer = await factory.ConnectProducerAsync("orders");

        var defined = new TaskCompletionSource<StatDefinition>(TaskCreationOptions.RunContinuationsAsynchronously);
        var updated = new TaskCompletionSource<StatUpdate>(TaskCreationOptions.RunContinuationsAsynchronously);
        viewer.On<StatDefinition>(HubRoutes.StatDefined, definition => defined.TrySetResult(definition));
        viewer.On<StatUpdate>(HubRoutes.StatUpdated, update => updated.TrySetResult(update));

        await producer.InvokeAsync(
            HubRoutes.DefineStat,
            new StatDefinition { Id = "rps", Title = "Requests/s" },
            TestContext.Current.CancellationToken);
        await producer.InvokeAsync(
            HubRoutes.UpdateStat,
            new StatUpdate { Id = "rps", Value = "42", TimestampUnixMs = 1 },
            TestContext.Current.CancellationToken);

        Assert.Equal("Requests/s", (await defined.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Title);
        Assert.Equal("42", (await updated.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Value);
    }

    [Fact]
    public async Task ConnectedClientsAreAnnouncedToViewers()
    {
        await using var factory = new NebuLogAppFactory();
        await using var viewer = await factory.ConnectViewerAsync();

        var announced = new TaskCompletionSource<IReadOnlyList<ConnectedClientInfo>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        viewer.On<IReadOnlyList<ConnectedClientInfo>>(HubRoutes.ClientsChanged, clients =>
        {
            if (clients.Any(client => client.Kind == "producer"))
            {
                announced.TrySetResult(clients);
            }
        });

        await using var producer = await factory.ConnectProducerAsync("orders");

        var snapshot = await announced.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var registered = Assert.Single(snapshot, client => client.Kind == "producer");
        Assert.Equal("orders", registered.ServiceName);
    }

    [Fact]
    public async Task ViewersReceiveTheSummary()
    {
        await using var factory = new NebuLogAppFactory();
        await using var viewer = await factory.ConnectViewerAsync();
        await using var producer = await factory.ConnectProducerAsync("orders");

        var summaries = new TaskCompletionSource<LiveSummaryDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        viewer.On<LiveSummaryDto>(HubRoutes.SummaryUpdated, summary =>
        {
            if (summary.TotalIngested > 0)
            {
                summaries.TrySetResult(summary);
            }
        });

        await producer.InvokeAsync(
            HubRoutes.PublishLogs,
            new[] { Entry("one", Severity.Error) },
            TestContext.Current.CancellationToken);

        var received = await summaries.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(1, received.TotalIngested);
        Assert.Equal(1, received.CountsByBand["Error"]);
        Assert.Contains("orders", received.Services);
    }

    internal static async Task PublishAndWaitAsync(
        HubConnection viewer,
        HubConnection producer,
        IReadOnlyList<NebuLogEntry> entries)
    {
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = viewer.On<IReadOnlyList<NebuLogEntry>>(
            HubRoutes.ReceiveLogs,
            _ => received.TrySetResult());

        await producer.InvokeAsync(HubRoutes.PublishLogs, entries, TestContext.Current.CancellationToken);
        await received.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }
}
