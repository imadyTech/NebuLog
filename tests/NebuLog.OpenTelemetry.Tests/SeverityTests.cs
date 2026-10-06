using Microsoft.Extensions.Logging;
using NebuLog.Contracts;
using Xunit;

namespace NebuLog.OpenTelemetry.Tests;

public sealed class SeverityTests
{
    [Theory]
    [InlineData(LogLevel.Trace, 1)]
    [InlineData(LogLevel.Debug, 5)]
    [InlineData(LogLevel.Information, 9)]
    [InlineData(LogLevel.Warning, 13)]
    [InlineData(LogLevel.Error, 17)]
    [InlineData(LogLevel.Critical, 21)]
    [InlineData(LogLevel.None, 0)]
    public void FromLogLevelMapsEveryLevel(LogLevel level, int expected) =>
        Assert.Equal(expected, Severity.FromLogLevel(level));

    [Theory]
    [InlineData(0, "Unspecified", "NONE")]
    [InlineData(1, "Trace", "TRACE")]
    [InlineData(4, "Trace", "TRACE")]
    [InlineData(5, "Debug", "DEBUG")]
    [InlineData(9, "Info", "INFO")]
    [InlineData(12, "Info", "INFO")]
    [InlineData(13, "Warn", "WARN")]
    [InlineData(17, "Error", "ERROR")]
    [InlineData(21, "Fatal", "FATAL")]
    [InlineData(24, "Fatal", "FATAL")]
    [InlineData(-1, "Unspecified", "NONE")]
    public void BandAndShortNameCoverEveryRange(int severityNumber, string band, string shortName)
    {
        Assert.Equal(band, Severity.Band(severityNumber));
        Assert.Equal(shortName, Severity.ShortName(severityNumber));
    }
}
