namespace NebuLog.Server.Api;

/// <summary>What <c>GET /api/info</c> reports about the running server.</summary>
public sealed record ServerInfoDto
{
    /// <summary>The server assembly's informational version.</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>When the server started, in Unix epoch milliseconds.</summary>
    public long StartedUnixMs { get; init; }

    /// <summary>How long the server has been running, in seconds.</summary>
    public long UptimeSeconds { get; init; }

    /// <summary>How many entries the ring buffer can hold.</summary>
    public int BufferCapacity { get; init; }

    /// <summary>How many entries the ring buffer currently holds.</summary>
    public int BufferedCount { get; init; }
}
