namespace NebuLog.Samples.MinimalApi;

/// <summary>The minimum level this sample emits, changed at run time by the dashboard's command.</summary>
public sealed class LogLevelSwitch
{
    private volatile int _minimumLevel = (int)LogLevel.Debug;

    /// <summary>The lowest level currently emitted.</summary>
    public LogLevel MinimumLevel
    {
        get => (LogLevel)_minimumLevel;
        set => _minimumLevel = (int)value;
    }

    /// <summary>True when a message at this level should be emitted.</summary>
    /// <param name="level">The candidate level.</param>
    public bool IsEnabled(LogLevel level) => level >= MinimumLevel;
}

/// <summary>Counts requests so the sample can publish a requests-per-second statistic.</summary>
public sealed class RequestCounter
{
    private int _count;

    /// <summary>Records one request.</summary>
    public void Record() => Interlocked.Increment(ref _count);

    /// <summary>Returns the count since the last call and resets it.</summary>
    public int Drain() => Interlocked.Exchange(ref _count, 0);
}
