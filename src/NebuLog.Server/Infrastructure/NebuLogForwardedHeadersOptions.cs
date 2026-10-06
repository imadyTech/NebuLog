namespace NebuLog.Server.Infrastructure;

/// <summary>
/// Which proxies the server trusts, bound from configuration section <c>NebuLog:ForwardedHeaders</c>.
/// </summary>
/// <remarks>
/// In the deployed topology cloudflared is the only way in and the container's direct peer is the
/// compose network gateway, so the trusted network is that subnet and the client address is taken
/// from <c>CF-Connecting-IP</c>. When no networks are configured the feature stays off, which is
/// the right default for local development.
/// </remarks>
public sealed class NebuLogForwardedHeadersOptions
{
    /// <summary>The configuration section these options are bound from.</summary>
    public const string SectionName = "NebuLog:ForwardedHeaders";

    /// <summary>The header carrying the real client address.</summary>
    public const string ClientIpHeaderName = "CF-Connecting-IP";

    /// <summary>Comma-separated CIDR blocks whose forwarded headers are trusted.</summary>
    public string KnownNetworks { get; set; } = string.Empty;
}
