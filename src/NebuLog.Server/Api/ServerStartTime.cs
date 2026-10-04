namespace NebuLog.Server.Api;

/// <summary>Records when the server started, so uptime does not depend on process-wide state.</summary>
/// <param name="StartedUnixMs">Start time in Unix epoch milliseconds.</param>
public sealed record ServerStartTime(long StartedUnixMs);
