namespace NebuLog.Contracts;

/// <summary>Describes one client currently connected to the NebuLog hub.</summary>
public sealed record ConnectedClientInfo
{
    /// <summary>The SignalR connection id.</summary>
    public string ConnectionId { get; init; } = string.Empty;

    /// <summary>The connection kind: <c>producer</c> or <c>viewer</c>.</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>The producer's <c>service.name</c>, when the client is a producer.</summary>
    public string? ServiceName { get; init; }

    /// <summary>The producer's <c>service.instance.id</c>, when the client is a producer.</summary>
    public string? ServiceInstanceId { get; init; }

    /// <summary>When the client connected, in Unix epoch milliseconds.</summary>
    public long ConnectedUnixMs { get; init; }
}
