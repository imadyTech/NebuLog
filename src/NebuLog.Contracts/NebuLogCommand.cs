using System.Collections.Generic;

namespace NebuLog.Contracts;

/// <summary>A command issued from the dashboard down to a connected producer.</summary>
public sealed record NebuLogCommand
{
    /// <summary>The command name the producer dispatches on.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Command arguments, keyed by name.</summary>
    public IReadOnlyDictionary<string, string> Arguments { get; init; } = new Dictionary<string, string>(0);

    /// <summary>Identity of the operator that issued the command.</summary>
    public string IssuedBy { get; init; } = string.Empty;

    /// <summary>When the command was issued, in Unix epoch milliseconds.</summary>
    public long IssuedUnixMs { get; init; }
}
