using System.Net;
using System.Text.Json;
using Xunit;

namespace NebuLog.Server.Tests;

/// <summary>
/// The anonymous surface. These endpoints face the whole internet, so what they must <em>not</em>
/// return matters more than what they do.
/// </summary>
public sealed class PublicEndpointTests
{
    [Fact]
    public async Task TheSummaryIsReadableWithoutSigningIn()
    {
        using var factory = new NebuLogAppFactory();
        using var client = factory.CreateCookieClient();

        using var response = await client.GetAsync(
            new Uri("/api/public/summary", UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TheSummaryReturnsExactlyTheAgreedFieldsAndNothingElse()
    {
        // Asserting the whole field set, not just the presence of the four: this is the test that
        // makes adding a field here a deliberate act rather than something that slips out onto an
        // anonymous endpoint with a refactor.
        using var factory = new NebuLogAppFactory();
        using var client = factory.CreateCookieClient();

        var json = await client.GetStringAsync(
            new Uri("/api/public/summary", UriKind.Relative),
            TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(json);
        var fields = document.RootElement.EnumerateObject().Select(property => property.Name).Order().ToArray();

        Assert.Equal(
            ["ingestedTotal", "ratePerSecondLast60s", "serviceCount", "uptimeSeconds"],
            fields);
    }

    [Fact]
    public async Task TheSummaryNamesNoServiceAndCarriesNoLogText()
    {
        var token = TestContext.Current.CancellationToken;
        using var factory = new NebuLogAppFactory();

        // Ingest something real, then check the anonymous endpoint cannot be used to read it back.
        var payload = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "otlp-logs.json"), token);

        using var producer = await factory.CreateProducerClientAsync();
        using var ingest = await producer.PostAsync(
            new Uri("/v1/logs", UriKind.Relative),
            new StringContent(payload, System.Text.Encoding.UTF8, "application/json"),
            token);
        Assert.Equal(HttpStatusCode.OK, ingest.StatusCode);

        using var client = factory.CreateCookieClient();
        var json = await client.GetStringAsync(new Uri("/api/public/summary", UriKind.Relative), token);

        // "OrderApi" and "otlp hello" are the service name and body carried by that fixture.
        Assert.DoesNotContain("OrderApi", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("otlp hello", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TheSiteConfigReturnsTheConfiguredLinks()
    {
        using var factory = new NebuLogAppFactory(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["NebuLog:Site:BlogUrl"] = "https://example.test/blog",
            ["NebuLog:Site:GitHubUrl"] = "https://example.test/repo",
        });
        using var client = factory.CreateCookieClient();

        var json = await client.GetStringAsync(
            new Uri("/api/public/site-config", UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Contains("https://example.test/blog", json, StringComparison.Ordinal);
        Assert.Contains("https://example.test/repo", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheAuthenticatedApiIsStillClosedToAnonymousCallers()
    {
        // The public group must not have widened anything next to it.
        using var factory = new NebuLogAppFactory();
        using var client = factory.CreateCookieClient();

        foreach (var path in new[] { "/api/logs", "/api/summary", "/api/info", "/api/clients" })
        {
            using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
