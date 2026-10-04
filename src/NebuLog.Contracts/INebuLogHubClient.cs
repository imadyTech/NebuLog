using System.Collections.Generic;
using System.Threading.Tasks;

namespace NebuLog.Contracts;

/// <summary>The strongly typed set of calls the NebuLog server makes on its connected clients.</summary>
public interface INebuLogHubClient
{
    /// <summary>Delivers a batch of log entries to a dashboard.</summary>
    /// <param name="entries">The entries, ordered by ascending <see cref="NebuLogEntry.Id"/>.</param>
    Task ReceiveLogs(IReadOnlyList<NebuLogEntry> entries);

    /// <summary>Notifies that a live statistic has been declared.</summary>
    /// <param name="definition">The declared statistic.</param>
    Task StatDefined(StatDefinition definition);

    /// <summary>Notifies that a live statistic has a new value.</summary>
    /// <param name="update">The new value.</param>
    Task StatUpdated(StatUpdate update);

    /// <summary>Notifies that the set of connected clients changed.</summary>
    /// <param name="clients">The clients currently connected.</param>
    Task ClientsChanged(IReadOnlyList<ConnectedClientInfo> clients);

    /// <summary>Delivers a command to a producer.</summary>
    /// <param name="command">The command to execute.</param>
    Task ReceiveCommand(NebuLogCommand command);
}
