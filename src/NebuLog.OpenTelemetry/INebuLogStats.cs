using NebuLog.Contracts;

namespace NebuLog.OpenTelemetry;

/// <summary>Publishes live statistics alongside the log stream, over the same connection.</summary>
public interface INebuLogStats
{
    /// <summary>Declares a statistic so dashboards can render a tile for it.</summary>
    /// <param name="definition">The statistic to declare.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    Task DefineAsync(StatDefinition definition, CancellationToken cancellationToken = default);

    /// <summary>Publishes a new value for a previously declared statistic.</summary>
    /// <param name="id">The statistic's identifier.</param>
    /// <param name="value">The formatted value to display.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    Task UpdateAsync(string id, string value, CancellationToken cancellationToken = default);
}
