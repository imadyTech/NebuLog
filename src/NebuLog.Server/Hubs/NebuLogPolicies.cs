namespace NebuLog.Server.Hubs;

/// <summary>Authorisation policy names used by the hub, the REST API and the OTLP receiver.</summary>
public static class NebuLogPolicies
{
    /// <summary>May publish logs and statistics.</summary>
    public const string Producer = "NebuLog.Producer";

    /// <summary>May read logs, history and statistics.</summary>
    public const string Viewer = "NebuLog.Viewer";

    /// <summary>May send commands to producers.</summary>
    public const string Operator = "NebuLog.Operator";

    /// <summary>May manage API keys.</summary>
    public const string Admin = "NebuLog.Admin";
}
