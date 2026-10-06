namespace NebuLog.Contracts;

/// <summary>Declares a named live statistic that producers can update and dashboards render.</summary>
public sealed record StatDefinition
{
    /// <summary>Stable identifier of the statistic.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Human-readable title shown on the dashboard.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Optional CSS colour hint for the dashboard.</summary>
    public string? Color { get; init; }
}
