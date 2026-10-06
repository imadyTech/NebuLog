using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace NebuShop.Orders;

/// <summary>
/// Produces a high-rate stream of log entries on demand, with at most one burst in flight for the
/// whole process.
/// </summary>
/// <remarks>
/// The single-burst rule is not politeness: this endpoint is reachable by any demo visitor, and
/// without it a handful of clicks would multiply into a load generator pointed at the server's
/// ingest pipeline.
/// </remarks>
public sealed class BurstRunner : IDisposable
{
    private readonly IOptions<ShopOptions> _options;
    private readonly ILogger<BurstRunner> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _stopping = new();

    private long _endsAtTicks;

    /// <summary>Creates the runner.</summary>
    /// <param name="options">Shop settings, which carry the rate and duration.</param>
    /// <param name="timeProvider">Clock, so tests do not wait in real time.</param>
    /// <param name="logger">Where the burst entries go.</param>
    public BurstRunner(IOptions<ShopOptions> options, TimeProvider timeProvider, ILogger<BurstRunner> logger)
    {
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>Seconds remaining on the burst currently running, or zero when none is.</summary>
    public int SecondsRemaining
    {
        get
        {
            var endsAt = Volatile.Read(ref _endsAtTicks);
            if (endsAt == 0)
            {
                return 0;
            }

            var remaining = endsAt - _timeProvider.GetUtcNow().UtcTicks;
            return remaining <= 0 ? 0 : (int)Math.Ceiling(TimeSpan.FromTicks(remaining).TotalSeconds);
        }
    }

    /// <summary>Starts a burst if none is running.</summary>
    /// <returns>
    /// The accepted burst, or <see langword="null"/> when one is already running — in which case
    /// <see cref="SecondsRemaining"/> says how long the caller should wait.
    /// </returns>
    /// <remarks>
    /// Returns as soon as the burst has been accepted. The entries are produced on a background
    /// task, because holding the request open for the whole burst would make the shop window look
    /// frozen for ten seconds — and the visitor is supposed to be watching the console, not a
    /// spinner.
    /// </remarks>
    public BurstAccepted? TryStart()
    {
        if (!_gate.Wait(0))
        {
            return null;
        }

        var settings = _options.Value;
        Volatile.Write(
            ref _endsAtTicks,
            _timeProvider.GetUtcNow().AddSeconds(settings.BurstSeconds).UtcTicks);

        _ = Task.Run(async () =>
        {
            try
            {
                await RunAsync(settings, _stopping.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Shutdown; nothing to report.
            }
            catch (Exception exception)
            {
                Log.BurstFailed(_logger, exception);
            }
            finally
            {
                Volatile.Write(ref _endsAtTicks, 0);
                _gate.Release();
            }
        });

        return new BurstAccepted(settings.BurstEntriesPerSecond, settings.BurstSeconds);
    }

    private async Task RunAsync(ShopOptions settings, CancellationToken cancellationToken)
    {
        Log.BurstStarted(_logger, settings.BurstEntriesPerSecond, settings.BurstSeconds);

        var total = settings.BurstEntriesPerSecond * settings.BurstSeconds;
        var emitted = 0;

        // Ten slices per second, so the rate arrives smoothly rather than as one spike.
        var perSlice = Math.Max(1, settings.BurstEntriesPerSecond / 10);
        var slices = settings.BurstSeconds * 10;

        using var activity = new Activity("shop.burst").Start();

        for (var slice = 0; slice < slices && !cancellationToken.IsCancellationRequested; slice++)
        {
            for (var i = 0; i < perSlice; i++)
            {
                Log.BurstEntry(_logger, ++emitted, total, Random.Shared.Next(1000, 9999));
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), _timeProvider, cancellationToken)
                .ConfigureAwait(false);
        }

        Log.BurstFinished(_logger, emitted);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _stopping.Cancel();
        _stopping.Dispose();
        _gate.Dispose();
    }
}
