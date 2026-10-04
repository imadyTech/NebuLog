namespace NebuLog.Contracts;

/// <summary>A new value for a previously defined <see cref="StatDefinition"/>.</summary>
public sealed record StatUpdate
{
    /// <summary>Identifier of the statistic being updated.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>The formatted value to display.</summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>When the value was produced, in Unix epoch milliseconds.</summary>
    public long TimestampUnixMs { get; init; }
}
