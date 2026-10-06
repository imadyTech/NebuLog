using Microsoft.Extensions.Hosting;

namespace NebuLog.Server.Api;

/// <summary>When the server started, so uptime does not depend on process-wide state.</summary>
/// <remarks>
/// Stamped by <see cref="ServerStartTimeStamper"/> while the host is starting, deliberately not in
/// a DI factory: a factory runs on first resolution, which is the first request to
/// <c>/api/info</c>. That made uptime start counting from whenever somebody first looked at it —
/// a server running for three hours reported a few seconds.
/// </remarks>
public sealed class ServerStartTime
{
    private long _startedUnixMs;

    /// <summary>When the server started, in Unix epoch milliseconds.</summary>
    /// <param name="timeProvider">Clock used if the host never started, as in a bare unit test.</param>
    public long GetStartedUnixMs(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        var stamped = Volatile.Read(ref _startedUnixMs);
        return stamped != 0 ? stamped : timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
    }

    /// <summary>Records the start time. Only the first call takes effect.</summary>
    /// <param name="startedUnixMs">The moment the host began starting.</param>
    internal void Stamp(long startedUnixMs) => Interlocked.CompareExchange(ref _startedUnixMs, startedUnixMs, 0);
}

/// <summary>Stamps <see cref="ServerStartTime"/> before anything else in the host runs.</summary>
internal sealed class ServerStartTimeStamper : IHostedLifecycleService
{
    private readonly ServerStartTime _startTime;
    private readonly TimeProvider _timeProvider;

    public ServerStartTimeStamper(ServerStartTime startTime, TimeProvider timeProvider)
    {
        _startTime = startTime;
        _timeProvider = timeProvider;
    }

    public Task StartingAsync(CancellationToken cancellationToken)
    {
        _startTime.Stamp(_timeProvider.GetUtcNow().ToUnixTimeMilliseconds());
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
