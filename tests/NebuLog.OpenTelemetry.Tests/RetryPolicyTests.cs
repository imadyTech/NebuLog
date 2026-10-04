using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace NebuLog.OpenTelemetry.Tests;

public sealed class RetryPolicyTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(3, 8)]
    [InlineData(5, 30)]
    [InlineData(50, 30)]
    public void BacksOffExponentiallyAndNeverGivesUp(int previousRetryCount, double expectedSeconds)
    {
        var policy = new NebuLogRetryPolicy();

        var delay = policy.NextRetryDelay(
            new RetryContext { PreviousRetryCount = previousRetryCount, ElapsedTime = TimeSpan.Zero });

        Assert.NotNull(delay);
        Assert.Equal(expectedSeconds, delay.Value.TotalSeconds);
    }
}
