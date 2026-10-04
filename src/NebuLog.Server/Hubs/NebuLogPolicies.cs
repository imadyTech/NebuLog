namespace NebuLog.Server.Hubs;

/// <summary>
/// Authorisation policy names reserved for WO-0005. The hub and API already carry these names on
/// their members so that enabling authentication is additive rather than a restructuring.
/// </summary>
public static class NebuLogPolicies
{
    /// <summary>May publish logs and statistics.</summary>
    public const string Producer = "NebuLog.Producer";

    /// <summary>May read logs, history and statistics.</summary>
    public const string Viewer = "NebuLog.Viewer";

    /// <summary>May send commands to producers.</summary>
    public const string Operator = "NebuLog.Operator";
}
