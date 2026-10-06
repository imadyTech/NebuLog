using Microsoft.AspNetCore.SignalR.Client;
using NebuLog.Contracts;
using NebuLog.Server.Diagnostics;
using NebuLog.Server.Hubs;
using NebuLog.Server.Identity;
using Xunit;

namespace NebuLog.Server.Tests;

/// <summary>
/// Everything the server pushes, exercised over MessagePack.
/// </summary>
/// <remarks>
/// These exist because of a bug found only after deployment: <c>ClientsChanged</c> carried a list
/// built with a collection expression, whose compiler-generated type MessagePack cannot construct.
/// It serialised fine as JSON, so the whole suite passed while every MessagePack client was
/// disconnected the moment a producer appeared. Protocol choice is a real axis of behaviour, and
/// these tests pin it down.
/// </remarks>
public sealed class MessagePackProtocolTests
{
    private const NebuLogAppFactory.HubProtocol MsgPack = NebuLogAppFactory.HubProtocol.MessagePack;

    [Fact]
    public async Task EverythingTheServerPushesSurvivesMessagePack()
    {
        var token = TestContext.Current.CancellationToken;
        await using var factory = new NebuLogAppFactory();
        await using var viewer = await factory.ConnectViewerAsync(NebuLogRoles.Operator, MsgPack);

        var clients = new TaskCompletionSource<IReadOnlyList<ConnectedClientInfo>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var logs = new TaskCompletionSource<IReadOnlyList<NebuLogEntry>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var summary = new TaskCompletionSource<LiveSummaryDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var defined = new TaskCompletionSource<StatDefinition>(TaskCreationOptions.RunContinuationsAsynchronously);
        var updated = new TaskCompletionSource<StatUpdate>(TaskCreationOptions.RunContinuationsAsynchronously);

        viewer.On<IReadOnlyList<ConnectedClientInfo>>(HubRoutes.ClientsChanged, value =>
        {
            if (value.Any(client => client.Kind == "producer"))
            {
                clients.TrySetResult(value);
            }
        });
        viewer.On<IReadOnlyList<NebuLogEntry>>(HubRoutes.ReceiveLogs, value => logs.TrySetResult(value));
        viewer.On<LiveSummaryDto>(HubRoutes.SummaryUpdated, value =>
        {
            if (value.TotalIngested > 0)
            {
                summary.TrySetResult(value);
            }
        });
        viewer.On<StatDefinition>(HubRoutes.StatDefined, value => defined.TrySetResult(value));
        viewer.On<StatUpdate>(HubRoutes.StatUpdated, value => updated.TrySetResult(value));

        // A producer appearing is what used to kill every MessagePack viewer.
        await using var producer = await factory.ConnectProducerAsync("orders", MsgPack);

        var announced = await clients.Task.WaitAsync(TimeSpan.FromSeconds(10), token);
        Assert.Equal("orders", Assert.Single(announced, client => client.Kind == "producer").ServiceName);

        await producer.InvokeAsync(
            HubRoutes.PublishLogs,
            new[] { new NebuLogEntry { Body = "over messagepack", SeverityNumber = Severity.Error, ServiceName = "orders" } },
            token);

        var batch = await logs.Task.WaitAsync(TimeSpan.FromSeconds(10), token);
        Assert.Equal("over messagepack", Assert.Single(batch).Body);

        var received = await summary.Task.WaitAsync(TimeSpan.FromSeconds(10), token);
        Assert.Equal(1, received.TotalIngested);
        Assert.Equal(1, received.CountsByBand["Error"]);
        Assert.Contains("orders", received.Services);
        Assert.Equal(LiveSummary.RateWindowSeconds, received.RatePerSecond.Count);

        await producer.InvokeAsync(
            HubRoutes.DefineStat,
            new StatDefinition { Id = "depth", Title = "Queue depth", Color = "blue" },
            token);
        Assert.Equal("Queue depth", (await defined.Task.WaitAsync(TimeSpan.FromSeconds(10), token)).Title);

        await producer.InvokeAsync(
            HubRoutes.UpdateStat,
            new StatUpdate { Id = "depth", Value = "7", TimestampUnixMs = 1 },
            token);
        Assert.Equal("7", (await updated.Task.WaitAsync(TimeSpan.FromSeconds(10), token)).Value);
    }

    [Fact]
    public async Task HubReturnValuesSurviveMessagePack()
    {
        var token = TestContext.Current.CancellationToken;
        await using var factory = new NebuLogAppFactory();
        await using var viewer = await factory.ConnectViewerAsync(NebuLogRoles.Viewer, MsgPack);
        await using var producer = await factory.ConnectProducerAsync("orders", MsgPack);

        await producer.InvokeAsync(
            HubRoutes.DefineStat,
            new StatDefinition { Id = "depth", Title = "Queue depth", Color = "blue" },
            token);

        // GetStats returns the snapshot list; the same construction that broke ClientsChanged.
        var stats = await viewer.InvokeAsync<IReadOnlyList<StatSnapshot>>(HubRoutes.GetStats, token);
        Assert.Equal("depth", Assert.Single(stats).Definition.Id);

        await producer.InvokeAsync(
            HubRoutes.PublishLogs,
            new[] { new NebuLogEntry { Body = "history", ServiceName = "orders", SeverityNumber = Severity.Info } },
            token);

        // Give the pipeline a moment to assign ids and buffer the entry.
        await Task.Delay(TimeSpan.FromMilliseconds(500), token);

        var replayed = new List<NebuLogEntry>();
        await foreach (var entry in viewer
            .StreamAsync<NebuLogEntry>(HubRoutes.StreamHistory, new HistoryQuery(), token)
            .WithCancellation(token))
        {
            replayed.Add(entry);
        }

        Assert.Contains(replayed, entry => entry.Body == "history");
    }
}
