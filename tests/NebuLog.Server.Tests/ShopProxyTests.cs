using System.Net;
using NebuLog.Server.Authentication;
using NebuLog.Server.Identity;
using NebuLog.Server.Proxy;
using Xunit;

namespace NebuLog.Server.Tests;

/// <summary>
/// The reverse proxy that puts the NebuShop demo on this server's origin.
/// </summary>
/// <remarks>
/// These tests deliberately point the proxy at an address nothing is listening on. What is under
/// test is the gate in front of the route — authorisation, the CSRF header, rate limiting and
/// whether the route exists at all — not the shop behind it, which has its own tests. A request
/// that gets as far as trying to reach the destination has already passed everything that matters
/// here, and arrives as 502 rather than 401/403/404.
/// </remarks>
public sealed class ShopProxyTests
{
    private static Dictionary<string, string?> ShopConfigured(string? extra = null) =>
        new(StringComparer.Ordinal)
        {
            ["NebuLog:Shop:OrdersUrl"] = extra ?? "http://127.0.0.1:59999",
        };

    [Fact]
    public async Task TheShopRouteDoesNotExistWhenNoAddressIsConfigured()
    {
        // Anyone self-hosting NebuLog has no demo shop, and must not end up with a route that
        // proxies to nowhere.
        using var factory = new NebuLogAppFactory();
        using var client = await factory.SignInAsAsync(NebuLogRoles.Viewer);

        using var response = await client.GetAsync(
            new Uri($"{NebuLogShopOptions.PathPrefix}/", UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AnAnonymousVisitorCannotReachTheShop()
    {
        using var factory = new NebuLogAppFactory(ShopConfigured());
        using var client = factory.CreateCookieClient();

        using var response = await client.GetAsync(
            new Uri($"{NebuLogShopOptions.PathPrefix}/", UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ASignedInViewerPassesTheGate()
    {
        using var factory = new NebuLogAppFactory(ShopConfigured());
        using var client = await factory.SignInAsAsync(NebuLogRoles.Viewer);

        using var response = await client.GetAsync(
            new Uri($"{NebuLogShopOptions.PathPrefix}/", UriKind.Relative),
            TestContext.Current.CancellationToken);

        // Past authorisation, so the proxy tried to forward: the shop is not running in this test.
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task AStateChangingRequestWithoutTheCsrfHeaderIsRefused()
    {
        using var factory = new NebuLogAppFactory(ShopConfigured());
        using var client = await factory.SignInAsAsync(NebuLogRoles.Viewer);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri($"{NebuLogShopOptions.PathPrefix}/api/minimal/checkout", UriKind.Relative));
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AStateChangingRequestWithTheCsrfHeaderPassesTheGate()
    {
        using var factory = new NebuLogAppFactory(ShopConfigured());
        using var client = await factory.SignInAsAsync(NebuLogRoles.Viewer);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri($"{NebuLogShopOptions.PathPrefix}/api/minimal/checkout", UriKind.Relative));
        request.Headers.Add(CsrfHeaderFilter.HeaderName, CsrfHeaderFilter.HeaderValue);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task TheShopApiIsRateLimitedPerUser()
    {
        var settings = ShopConfigured();
        settings["NebuLog:Shop:BurstCapacity"] = "2";
        settings["NebuLog:Shop:ReplenishmentPeriod"] = "00:01:00";

        using var factory = new NebuLogAppFactory(settings);
        using var client = await factory.SignInAsAsync(NebuLogRoles.Viewer);

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            using var response = await client.GetAsync(
                new Uri($"{NebuLogShopOptions.PathPrefix}/api/catalog", UriKind.Relative),
                TestContext.Current.CancellationToken);
            statuses.Add(response.StatusCode);
        }

        // The bucket holds two, and a minute's replenishment never arrives during the test.
        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
        Assert.Equal(2, statuses.Count(status => status != HttpStatusCode.TooManyRequests));
    }
}
