namespace NebuLog.Contracts;

/// <summary>Filter for replaying buffered log history to a dashboard.</summary>
public sealed record HistoryQuery
{
    /// <summary>The default number of entries returned when <see cref="Limit"/> is not set.</summary>
    public const int DefaultLimit = 1000;

    /// <summary>The largest number of entries the server will return for one query.</summary>
    public const int MaxLimit = 10_000;

    /// <summary>Return only entries with an id greater than this value.</summary>
    public long? AfterId { get; init; }

    /// <summary>Return only entries whose severity number is at least this value.</summary>
    public int? MinSeverity { get; init; }

    /// <summary>Return only entries produced by this service.</summary>
    public string? Service { get; init; }

    /// <summary>Maximum number of entries to return, capped at <see cref="MaxLimit"/>.</summary>
    public int Limit { get; init; } = DefaultLimit;
}
