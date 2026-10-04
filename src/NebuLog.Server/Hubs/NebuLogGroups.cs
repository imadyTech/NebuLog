namespace NebuLog.Server.Hubs;

/// <summary>SignalR group names used by the hub.</summary>
public static class NebuLogGroups
{
    /// <summary>
    /// The group every dashboard joins. Log broadcasts target this group rather than
    /// <c>Clients.All</c>, so producers never receive the stream back.
    /// </summary>
    public const string Viewers = "viewers";
}
