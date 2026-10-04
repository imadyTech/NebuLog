using Microsoft.Extensions.Logging;

namespace NebuLog.Contracts;

/// <summary>
/// Maps between <see cref="LogLevel"/> and the OpenTelemetry severity numbers used on the wire.
/// </summary>
public static class Severity
{
    /// <summary>Severity number meaning "not specified".</summary>
    public const int Unspecified = 0;

    /// <summary>Lowest severity number in the TRACE band.</summary>
    public const int Trace = 1;

    /// <summary>Lowest severity number in the DEBUG band.</summary>
    public const int Debug = 5;

    /// <summary>Lowest severity number in the INFO band.</summary>
    public const int Info = 9;

    /// <summary>Lowest severity number in the WARN band.</summary>
    public const int Warn = 13;

    /// <summary>Lowest severity number in the ERROR band.</summary>
    public const int Error = 17;

    /// <summary>Lowest severity number in the FATAL band.</summary>
    public const int Fatal = 21;

    /// <summary>Converts a <see cref="LogLevel"/> to its OpenTelemetry severity number.</summary>
    /// <param name="level">The level to convert.</param>
    /// <returns>The severity number, or <see cref="Unspecified"/> for <see cref="LogLevel.None"/>.</returns>
    public static int FromLogLevel(LogLevel level) => level switch
    {
        LogLevel.Trace => Trace,
        LogLevel.Debug => Debug,
        LogLevel.Information => Info,
        LogLevel.Warning => Warn,
        LogLevel.Error => Error,
        LogLevel.Critical => Fatal,
        _ => Unspecified,
    };

    /// <summary>Returns the band a severity number falls into.</summary>
    /// <param name="severityNumber">The severity number, normally 1–24.</param>
    /// <returns>One of <c>Trace</c>, <c>Debug</c>, <c>Info</c>, <c>Warn</c>, <c>Error</c>, <c>Fatal</c> or <c>Unspecified</c>.</returns>
    public static string Band(int severityNumber) => severityNumber switch
    {
        >= Fatal => "Fatal",
        >= Error => "Error",
        >= Warn => "Warn",
        >= Info => "Info",
        >= Debug => "Debug",
        >= Trace => "Trace",
        _ => "Unspecified",
    };

    /// <summary>Returns a fixed-width, upper-case abbreviation of the severity band.</summary>
    /// <param name="severityNumber">The severity number, normally 1–24.</param>
    /// <returns>A five-character abbreviation such as <c>INFO</c> or <c>FATAL</c>.</returns>
    public static string ShortName(int severityNumber) => severityNumber switch
    {
        >= Fatal => "FATAL",
        >= Error => "ERROR",
        >= Warn => "WARN",
        >= Info => "INFO",
        >= Debug => "DEBUG",
        >= Trace => "TRACE",
        _ => "NONE",
    };
}
