using NebuLog.Contracts;
using NebuLog.Server.Ingestion;
using Xunit;

namespace NebuLog.Server.Tests;

public sealed class LogRingBufferTests
{
    private static NebuLogEntry Entry(long id, int severity = Severity.Info, string service = "svc", string body = "m") =>
        new()
        {
            Id = id,
            SeverityNumber = severity,
            ServiceName = service,
            Body = body,
        };

    [Fact]
    public void OverwritesTheOldestEntriesOnceFull()
    {
        using var buffer = new LogRingBuffer(3);

        buffer.Append([Entry(1), Entry(2), Entry(3), Entry(4), Entry(5)]);

        Assert.Equal(3, buffer.Count);
        Assert.Equal([3L, 4L, 5L], buffer.Snapshot().Select(entry => entry.Id));
    }

    [Fact]
    public void AppendingNothingIsANoOp()
    {
        using var buffer = new LogRingBuffer(4);

        buffer.Append([]);

        Assert.Equal(0, buffer.Count);
        Assert.Empty(buffer.Snapshot());
    }

    [Fact]
    public void QueryFiltersByIdSeverityServiceAndBody()
    {
        using var buffer = new LogRingBuffer(10);
        buffer.Append(
        [
            Entry(1, Severity.Debug, "orders", "cart opened"),
            Entry(2, Severity.Error, "orders", "payment DECLINED"),
            Entry(3, Severity.Error, "shipping", "label failed"),
            Entry(4, Severity.Info, "orders", "payment accepted"),
        ]);

        Assert.Equal([2L, 3L, 4L], buffer.Query(new HistoryQuery { AfterId = 1 }).Select(entry => entry.Id));
        Assert.Equal([2L, 3L], buffer.Query(new HistoryQuery { MinSeverity = Severity.Error }).Select(entry => entry.Id));
        Assert.Equal([1L, 2L, 4L], buffer.Query(new HistoryQuery { Service = "ORDERS" }).Select(entry => entry.Id));
        Assert.Equal([2L, 4L], buffer.Query(new HistoryQuery(), search: "payment").Select(entry => entry.Id));
    }

    [Fact]
    public void QueryClampsTheLimit()
    {
        using var buffer = new LogRingBuffer(10);
        buffer.Append([.. Enumerable.Range(1, 10).Select(i => Entry(i))]);

        Assert.Equal(2, buffer.Query(new HistoryQuery { Limit = 2 }).Count);
        Assert.Equal(10, buffer.Query(new HistoryQuery { Limit = int.MaxValue }).Count);
        Assert.Single(buffer.Query(new HistoryQuery { Limit = 0 }));
    }

    [Fact]
    public async Task SurvivesConcurrentReadsWhileWriting()
    {
        using var buffer = new LogRingBuffer(500);
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var writer = Task.Run(() =>
        {
            var id = 0L;
            while (!stop.IsCancellationRequested)
            {
                buffer.Append([Entry(++id)]);
            }
        }, TestContext.Current.CancellationToken);

        var readers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                var snapshot = buffer.Snapshot();
                Assert.True(snapshot.Count <= buffer.Capacity);
                Assert.DoesNotContain(snapshot, entry => entry is null);

                var queried = buffer.Query(new HistoryQuery { MinSeverity = Severity.Info });
                Assert.True(queried.Count <= buffer.Capacity);
            }
        }, TestContext.Current.CancellationToken)).ToArray();

        await Task.WhenAll([writer, .. readers]);

        Assert.Equal(buffer.Capacity, buffer.Count);
    }

    [Fact]
    public void RejectsNonPositiveCapacity() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new LogRingBuffer(0));
}
