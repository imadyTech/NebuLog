using NebuLog.Contracts;

namespace NebuLog.Server.Diagnostics;

/// <summary>
/// Running totals over everything the server has ingested: counts per severity band, the
/// per-second receive rate over the last minute, and the set of services seen.
/// </summary>
public sealed class LiveSummary
{
    /// <summary>How many one-second buckets the rate window keeps.</summary>
    public const int RateWindowSeconds = 60;

    private readonly Lock _gate = new();
    private readonly Dictionary<string, long> _countsByBand = new(StringComparer.Ordinal);
    private readonly SortedSet<string> _services = new(StringComparer.Ordinal);
    private readonly int[] _rate = new int[RateWindowSeconds];
    private readonly TimeProvider _timeProvider;
    private long _totalIngested;
    private long _currentSecond;

    /// <summary>Creates a summary that reads the clock from the given provider.</summary>
    /// <param name="timeProvider">Clock used to roll the per-second rate window.</param>
    public LiveSummary(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        _timeProvider = timeProvider;
        _currentSecond = CurrentSecond();
    }

    /// <summary>Folds a batch of entries into the running totals.</summary>
    /// <param name="entries">The entries that were just buffered.</param>
    public void Record(IReadOnlyList<NebuLogEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (entries.Count == 0)
        {
            return;
        }

        lock (_gate)
        {
            RollUnlocked();

            foreach (var entry in entries)
            {
                var band = Severity.Band(entry.SeverityNumber);
                _countsByBand[band] = _countsByBand.TryGetValue(band, out var count) ? count + 1 : 1;
                _services.Add(entry.ServiceName);
            }

            _totalIngested += entries.Count;
            _rate[(int)(_currentSecond % RateWindowSeconds)] += entries.Count;
        }
    }

    /// <summary>Takes a snapshot for dashboards.</summary>
    /// <param name="bufferedCount">The ring buffer's current occupancy.</param>
    public LiveSummaryDto Snapshot(int bufferedCount)
    {
        lock (_gate)
        {
            RollUnlocked();

            // Order the window oldest-first, so index 59 is always the second in progress.
            var ordered = new int[RateWindowSeconds];
            for (var i = 0; i < RateWindowSeconds; i++)
            {
                var second = _currentSecond - (RateWindowSeconds - 1 - i);
                ordered[i] = second < 0 ? 0 : _rate[(int)(second % RateWindowSeconds)];
            }

            return new LiveSummaryDto
            {
                TotalIngested = _totalIngested,
                CountsByBand = new Dictionary<string, long>(_countsByBand, StringComparer.Ordinal),
                RatePerSecond = ordered,
                Services = [.. _services],
                BufferedCount = bufferedCount,
                TimestampUnixMs = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds(),
            };
        }
    }

    private long CurrentSecond() => _timeProvider.GetUtcNow().ToUnixTimeSeconds();

    /// <summary>Clears the buckets that elapsed since the last update. Caller holds <see cref="_gate"/>.</summary>
    private void RollUnlocked()
    {
        var now = CurrentSecond();
        if (now == _currentSecond)
        {
            return;
        }

        var elapsed = now - _currentSecond;
        if (elapsed >= RateWindowSeconds)
        {
            Array.Clear(_rate);
        }
        else
        {
            for (var second = _currentSecond + 1; second <= now; second++)
            {
                _rate[(int)(second % RateWindowSeconds)] = 0;
            }
        }

        _currentSecond = now;
    }
}
