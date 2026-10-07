using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NebuLog.Server.Authentication;
using NebuLog.Server.Hubs;
using Yarp.ReverseProxy.Configuration;

namespace NebuLog.Server.Proxy;

/// <summary>
/// Reverse-proxies the NebuShop demo application under <c>/apps/shop</c>.
/// </summary>
/// <remarks>
/// The shop runs as two containers with no published ports; this route is the only way in. Serving
/// it from the server's own origin is deliberate: the shop window and the console can then talk
/// over a <c>BroadcastChannel</c>, and the shop's calls carry the same session cookie, so nothing
/// needs a second credential or a relaxed CSP.
/// </remarks>
public static class NebuLogShopProxy
{
    private const string RouteId = "nebushop";
    private const string ClusterId = "nebushop-orders";

    /// <summary>Registers the proxy, unless no shop address is configured.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddNebuLogShopProxy(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // The route and cluster are built from options when YARP asks for them, not here: reading
        // configuration during registration freezes whatever happened to be loaded at start-up,
        // which is the mistake this project has already made three times (WO-0004 §9, WO-0005 §9).
        services.AddReverseProxy();
        services.AddSingleton<IProxyConfigProvider, ShopProxyConfigProvider>();

        return services;
    }

    /// <summary>Maps the shop route when one is configured.</summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapNebuLogShopProxy(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var options = endpoints.ServiceProvider.GetRequiredService<IOptions<NebuLogShopOptions>>().Value;
        if (string.IsNullOrWhiteSpace(options.OrdersUrl))
        {
            // No shop deployed: the path stays a 404 like any other unknown route.
            return endpoints;
        }

        endpoints.MapReverseProxy(pipeline =>
        {
            // The shop's state-changing calls ride the browser's session cookie, so they need the
            // same custom-header proof that every other cookie-authenticated POST on this server
            // needs. YARP routes are not route handlers, so this cannot be an endpoint filter.
            pipeline.Use(async (context, next) =>
            {
                if (!IsSafeMethod(context.Request.Method) &&
                    !string.Equals(
                        context.Request.Headers[CsrfHeaderFilter.HeaderName],
                        CsrfHeaderFilter.HeaderValue,
                        StringComparison.Ordinal))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }

                await next().ConfigureAwait(false);
            });
        })
        .RequireAuthorization(NebuLogPolicies.Viewer)
        .RequireRateLimiting(NebuLogShopOptions.RateLimitPolicy);

        return endpoints;
    }

    /// <summary>
    /// Adds the shop's rate-limiting policy: a small token bucket per signed-in user.
    /// </summary>
    /// <param name="limiter">The rate limiter options being configured.</param>
    internal static void AddShopRateLimitPolicy(RateLimiterOptions limiter)
    {
        limiter.AddPolicy(NebuLogShopOptions.RateLimitPolicy, static context =>
        {
            var options = context.RequestServices
                .GetRequiredService<IOptions<NebuLogShopOptions>>().Value;

            return RateLimitPartition.GetTokenBucketLimiter(PartitionKey(context), _ =>
                new TokenBucketRateLimiterOptions
                {
                    TokenLimit = options.BurstCapacity,
                    TokensPerPeriod = 1,
                    ReplenishmentPeriod = options.ReplenishmentPeriod,
                    QueueLimit = 0,
                    AutoReplenishment = true,
                });
        });
    }

    /// <summary>
    /// Partitions per signed-in user, so one visitor cannot spend everyone else's allowance.
    /// Demo visitors all share one account, so the address is mixed in as well.
    /// </summary>
    private static string PartitionKey(HttpContext context)
    {
        var user = context.User.Identity?.Name ?? "anonymous";
        var address = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return string.Create(CultureInfo.InvariantCulture, $"{user}|{address}");
    }

    private static bool IsSafeMethod(string method) =>
        HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method);

    /// <summary>Builds the YARP route and cluster for the shop.</summary>
    /// <param name="options">The shop settings.</param>
    /// <returns>The route and cluster YARP should serve.</returns>
    internal static (RouteConfig Route, ClusterConfig Cluster) BuildConfig(NebuLogShopOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var route = new RouteConfig
        {
            RouteId = RouteId,
            ClusterId = ClusterId,
            Match = new RouteMatch { Path = $"{NebuLogShopOptions.PathPrefix}/{{**rest}}" },
            // The shop is served under a prefix but built as if it owned its root, so the prefix
            // comes off before the request is forwarded.
            Transforms =
            [
                new Dictionary<string, string> { ["PathRemovePrefix"] = NebuLogShopOptions.PathPrefix },
            ],
        };

        var cluster = new ClusterConfig
        {
            ClusterId = ClusterId,
            Destinations = new Dictionary<string, DestinationConfig>
            {
                ["orders"] = new() { Address = options.OrdersUrl },
            },
        };

        return (route, cluster);
    }

    /// <summary>
    /// Hands YARP the shop route, built from the current options.
    /// </summary>
    private sealed class ShopProxyConfigProvider : IProxyConfigProvider
    {
        private readonly IOptions<NebuLogShopOptions> _options;

        public ShopProxyConfigProvider(IOptions<NebuLogShopOptions> options) => _options = options;

        public IProxyConfig GetConfig()
        {
            var options = _options.Value;

            if (string.IsNullOrWhiteSpace(options.OrdersUrl))
            {
                return new ShopProxyConfig([], []);
            }

            var (route, cluster) = BuildConfig(options);
            return new ShopProxyConfig([route], [cluster]);
        }
    }

    private sealed class ShopProxyConfig : IProxyConfig
    {
        public ShopProxyConfig(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters)
        {
            Routes = routes;
            Clusters = clusters;
        }

        public IReadOnlyList<RouteConfig> Routes { get; }

        public IReadOnlyList<ClusterConfig> Clusters { get; }

        // The shop address comes from the environment and does not change while the process runs,
        // so there is nothing to signal a reload for.
        public Microsoft.Extensions.Primitives.IChangeToken ChangeToken { get; } =
            new Microsoft.Extensions.Primitives.CancellationChangeToken(System.Threading.CancellationToken.None);
    }
}
