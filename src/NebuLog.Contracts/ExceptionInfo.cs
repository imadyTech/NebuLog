namespace NebuLog.Contracts;

/// <summary>A transport-friendly projection of an exception attached to a log entry.</summary>
public sealed record ExceptionInfo
{
    /// <summary>The exception's CLR type name.</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>The exception message.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>The stack trace, when one was captured.</summary>
    public string? StackTrace { get; init; }
}
