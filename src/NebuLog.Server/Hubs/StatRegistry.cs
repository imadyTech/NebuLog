using System.Collections.Concurrent;
using NebuLog.Contracts;

namespace NebuLog.Server.Hubs;

/// <summary>Holds the live statistics producers have declared and their latest values.</summary>
public sealed class StatRegistry
{
    private readonly ConcurrentDictionary<string, StatDefinition> _definitions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, StatUpdate> _values = new(StringComparer.Ordinal);

    /// <summary>Declares a statistic, replacing any earlier declaration with the same id.</summary>
    /// <param name="definition">The statistic to declare.</param>
    public void Define(StatDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        _definitions[definition.Id] = definition;
    }

    /// <summary>Records a new value for a statistic.</summary>
    /// <param name="update">The value to record.</param>
    public void Update(StatUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);

        _values[update.Id] = update;
    }

    /// <summary>Returns every declared statistic together with its latest value, if any.</summary>
    public IReadOnlyList<StatSnapshot> Snapshot() =>
    [
        .. _definitions.Values
            .OrderBy(definition => definition.Id, StringComparer.Ordinal)
            .Select(definition => new StatSnapshot(
                definition,
                _values.TryGetValue(definition.Id, out var value) ? value : null)),
    ];
}

/// <summary>A declared statistic paired with its most recent value.</summary>
/// <param name="Definition">How the statistic should be rendered.</param>
/// <param name="Latest">The most recent value, or <see langword="null"/> if none has been published.</param>
public sealed record StatSnapshot(StatDefinition Definition, StatUpdate? Latest);
