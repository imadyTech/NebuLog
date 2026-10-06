namespace NebuLog.Contracts;

/// <summary>Well-known hub path and method names shared by clients and the server.</summary>
public static class HubRoutes
{
    /// <summary>The path the NebuLog hub is mapped at.</summary>
    public const string Path = "/hubs/nebulog";

    /// <summary>Client → server: publish a batch of log entries.</summary>
    public const string PublishLogs = nameof(PublishLogs);

    /// <summary>Client → server: declare a live statistic.</summary>
    public const string DefineStat = nameof(DefineStat);

    /// <summary>Client → server: publish a new value for a live statistic.</summary>
    public const string UpdateStat = nameof(UpdateStat);

    /// <summary>Client → server: stream buffered history back to a dashboard.</summary>
    public const string StreamHistory = nameof(StreamHistory);

    /// <summary>Client → server: fetch every declared live statistic.</summary>
    public const string GetStats = nameof(GetStats);

    /// <summary>Client → server: send a command to connected producers.</summary>
    public const string SendCommand = nameof(SendCommand);

    /// <summary>Server → client: deliver a batch of log entries.</summary>
    public const string ReceiveLogs = nameof(ReceiveLogs);

    /// <summary>Server → client: a statistic was declared.</summary>
    public const string StatDefined = nameof(StatDefined);

    /// <summary>Server → client: a statistic value changed.</summary>
    public const string StatUpdated = nameof(StatUpdated);

    /// <summary>Server → client: the set of connected clients changed.</summary>
    public const string ClientsChanged = nameof(ClientsChanged);

    /// <summary>Server → client: the activity summary was refreshed.</summary>
    public const string SummaryUpdated = nameof(SummaryUpdated);

    /// <summary>Server → client: deliver a command to a producer.</summary>
    public const string ReceiveCommand = nameof(ReceiveCommand);
}
