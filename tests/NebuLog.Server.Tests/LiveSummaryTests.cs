using Microsoft.Extensions.Time.Testing;
using NebuLog.Contracts;
using NebuLog.Server.Diagnostics;
using Xunit;

namespace NebuLog.Server.Tests;

public sealed class LiveSummaryTests
{
    private static NebuLogEntry Entry(int severity, string service = "svc") =>
        new() { SeverityNumber = severity, ServiceName = service };

    [Fact]
    public void CountsBySeverityBandAndCollectsServices()
    {
        var summary = new LiveSummary(new FakeTimeProvider());

        summary.Record([Entry(Severity.Info, "orders"), Entry(Severity.Error, "orders"), Entry(Severity.Error, "shipping")]);

        var snapshot = summary.Snapshot(bufferedCount: 3);
        Assert.Equal(3, snapshot.TotalIngested);
        Assert.Equal(1, snapshot.CountsByBand["Info"]);
        Assert.Equal(2, snapshot.CountsByBand["Error"]);
        Assert.Equal(["orders", "shipping"], snapshot.Services);
        Assert.Equal(3, snapshot.BufferedCount);
    }

    [Fact]
    public void RecordingNothingChangesNothing()
    {
        var summary = new LiveSummary(new FakeTimeProvider());

        summary.Record([]);

        Assert.Equal(0, summary.Snapshot(0).TotalIngested);
    }

    [Fact]
    public void RateWindowPlacesTheCurrentSecondLast()
    {
        var time = new FakeTimeProvider();
        var summary = new LiveSummary(time);

        summary.Record([Entry(Severity.Info), Entry(Severity.Info)]);

        var window = summary.Snapshot(0).RatePerSecond;
        Assert.Equal(LiveSummary.RateWindowSeconds, window.Count);
        Assert.Equal(2, window[^1]);
        Assert.Equal(0, window[^2]);
    }

    [Fact]
    public void RateWindowScrollsAsTimePasses()
    {
        var time = new FakeTimeProvider();
        var summary = new LiveSummary(time);

        summary.Record([Entry(Severity.Info)]);
        time.Advance(TimeSpan.FromSeconds(3));
        summary.Record([Entry(Severity.Info), Entry(Severity.Info)]);

        var window = summary.Snapshot(0).RatePerSecond;
        Assert.Equal(2, window[^1]);
        Assert.Equal(1, window[^4]);
        Assert.Equal(3, summary.Snapshot(0).TotalIngested);
    }

    [Fact]
    public void RateWindowEmptiesAfterAFullMinuteOfSilence()
    {
        var time = new FakeTimeProvider();
        var summary = new LiveSummary(time);

        summary.Record([Entry(Severity.Info)]);
        time.Advance(TimeSpan.FromSeconds(LiveSummary.RateWindowSeconds + 5));

        var snapshot = summary.Snapshot(0);
        Assert.All(snapshot.RatePerSecond, bucket => Assert.Equal(0, bucket));

        // Cumulative totals are unaffected by the rolling window.
        Assert.Equal(1, snapshot.TotalIngested);
    }
}
