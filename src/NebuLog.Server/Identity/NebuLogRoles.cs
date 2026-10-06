namespace NebuLog.Server.Identity;

/// <summary>The role names NebuLog seeds and authorises against.</summary>
public static class NebuLogRoles
{
    /// <summary>May read logs, history, summaries, clients and statistics.</summary>
    public const string Viewer = "Viewer";

    /// <summary>Everything a viewer may do, plus sending commands to producers.</summary>
    public const string Operator = "Operator";

    /// <summary>Everything an operator may do, plus managing API keys.</summary>
    public const string Admin = "Admin";

    /// <summary>Granted only to API-key principals: publishing logs and statistics.</summary>
    public const string Producer = "Producer";

    /// <summary>The roles seeded into the database at startup.</summary>
    public static IReadOnlyList<string> All { get; } = [Viewer, Operator, Admin, Producer];
}
