namespace NebuLog.Samples.DemoProducer;

/// <summary>
/// The minimum level the producer emits, adjustable at run time by the <c>set-min-level</c> command.
/// </summary>
/// <remarks>
/// Deliberately a tiny mutable holder read by an <see cref="ILoggerProvider"/>-level filter: it is
/// what makes the dashboard's command visibly change what arrives, which is the point of the demo.
/// </remarks>
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
