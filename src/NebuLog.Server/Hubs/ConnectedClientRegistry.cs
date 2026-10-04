using System.Collections.Concurrent;
using NebuLog.Contracts;

namespace NebuLog.Server.Hubs;

/// <summary>Tracks which clients are connected to the hub and in what role.</summary>
public sealed class ConnectedClientRegistry
{
    /// <summary>The <see cref="ConnectedClientInfo.Kind"/> value for a log producer.</summary>
    public const string ProducerKind = "producer";

    /// <summary>The <see cref="ConnectedClientInfo.Kind"/> value for a dashboard.</summary>
    public const string ViewerKind = "viewer";

    private readonly ConcurrentDictionary<string, ConnectedClientInfo> _clients = new(StringComparer.Ordinal);

    /// <summary>Number of connected dashboards.</summary>
    public int ViewerCount => CountOf(ViewerKind);

    /// <summary>Number of connected producers.</summary>
    public int ProducerCount => CountOf(ProducerKind);

    /// <summary>Registers a connection.</summary>
    /// <param name="client">The client to record.</param>
    public void Add(ConnectedClientInfo client)
    {
        ArgumentNullException.ThrowIfNull(client);

        _clients[client.ConnectionId] = client;
    }

    /// <summary>Removes a connection.</summary>
    /// <param name="connectionId">The SignalR connection id.</param>
    /// <returns>True when a client was removed.</returns>
    public bool Remove(string connectionId) => _clients.TryRemove(connectionId, out _);

    /// <summary>Returns true when the given connection is a registered producer.</summary>
    /// <param name="connectionId">The SignalR connection id.</param>
    public bool IsProducer(string connectionId) =>
        _clients.TryGetValue(connectionId, out var client) &&
        string.Equals(client.Kind, ProducerKind, StringComparison.Ordinal);

    /// <summary>Returns every connected client, ordered by connection time.</summary>
    public IReadOnlyList<ConnectedClientInfo> Snapshot() =>
        [.. _clients.Values.OrderBy(client => client.ConnectedUnixMs).ThenBy(client => client.ConnectionId, StringComparer.Ordinal)];

    private int CountOf(string kind) =>
        _clients.Count(pair => string.Equals(pair.Value.Kind, kind, StringComparison.Ordinal));
}
