namespace NebuLog.Server.Proxy;

/// <summary>
/// Settings for the NebuShop demo application, bound from configuration section
/// <c>NebuLog:Shop</c>.
/// </summary>
/// <remarks>
/// When <see cref="OrdersUrl"/> is empty the proxy route is not registered at all, so a host that
/// knows nothing about the demo shop — the usual case for anyone self-hosting NebuLog — runs
/// unchanged.
/// </remarks>
public sealed class NebuLogShopOptions
{
    /// <summary>The configuration section these options are bound from.</summary>
    public const string SectionName = "NebuLog:Shop";

    /// <summary>The path the shop is served under.</summary>
    public const string PathPrefix = "/apps/shop";

    /// <summary>Name of the rate-limiting policy applied to the shop's API.</summary>
    public const string RateLimitPolicy = "shop";

    /// <summary>
    /// Base address of the orders service, for example <c>http://nebushop-orders:8080</c>.
    /// Empty disables the route.
    /// </summary>
    public string OrdersUrl { get; set; } = string.Empty;

    /// <summary>
    /// How many shop API requests one user may make back to back. A visitor clicking through a
    /// scenario sends a few in quick succession, so the bucket has to allow a small burst.
    /// </summary>
    public int BurstCapacity { get; set; } = 3;

    /// <summary>How often one request is added back to each user's bucket.</summary>
    public TimeSpan ReplenishmentPeriod { get; set; } = TimeSpan.FromSeconds(2);
}
