using Microsoft.AspNetCore.SignalR.Client;

namespace NebuLog.OpenTelemetry;

/// <summary>
/// Exponential backoff that doubles from one second up to a thirty-second ceiling and never gives up.
/// </summary>
internal sealed class NebuLogRetryPolicy : IRetryPolicy
{
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(30);

    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
        ArgumentNullException.ThrowIfNull(retryContext);

        var exponent = Math.Min(retryContext.PreviousRetryCount, 5);
        var seconds = Math.Min(Math.Pow(2, exponent), MaxDelay.TotalSeconds);
        return TimeSpan.FromSeconds(seconds);
    }
}
