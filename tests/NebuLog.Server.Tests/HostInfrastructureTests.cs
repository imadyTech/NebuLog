using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NebuLog.Server.Api;
using NebuLog.Server.Identity;
using Xunit;

namespace NebuLog.Server.Tests;

public sealed class HostInfrastructureTests
{
    [Fact]
    public async Task SecurityHeadersArePresentOnEveryResponse()
    {
        await using var factory = new NebuLogAppFactory();
        using var client = await factory.SignInAsAsync(NebuLogRoles.Viewer);

        using var response = await client.GetAsync(
            new Uri("/api/info", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(
            "default-src 'self'; connect-src 'self'; img-src 'self' data:; style-src 'self'; " +
            "script-src 'self'; frame-ancestors 'none'; base-uri 'self'",
            Assert.Single(response.Headers.GetValues("Content-Security-Policy")));
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        Assert.Contains("camera=()", Assert.Single(response.Headers.GetValues("Permissions-Policy")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScalarGetsItsOwnWiderPolicy()
    {
        await using var factory = new NebuLogAppFactory();
        using var client = await factory.SignInAsAsync(NebuLogRoles.Viewer);

        using var response = await client.GetAsync(
            new Uri("/scalar/v1", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Scalar renders from the OpenAPI document, so pointing at it is what makes every
        // /api endpoint and response model show up in the reference page.
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("openapi/v1.json", html, StringComparison.Ordinal);

        var policy = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("cdn.jsdelivr.net", policy, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'none'", policy, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenApiDocumentListsEveryApiEndpoint()
    {
        await using var factory = new NebuLogAppFactory();
        using var client = await factory.SignInAsAsync(NebuLogRoles.Viewer);

        using var document = JsonDocument.Parse(await client.GetStringAsync(
            new Uri("/openapi/v1.json", UriKind.Relative), TestContext.Current.CancellationToken));

        var paths = document.RootElement.GetProperty("paths");
        foreach (var expected in new[] { "/api/logs", "/api/summary", "/api/clients", "/api/custom-stats", "/api/info" })
        {
            Assert.True(paths.TryGetProperty(expected, out _), $"{expected} is missing from the OpenAPI document.");
        }
    }

    [Fact]
    public async Task OpenApiDocumentDescribesTheOtlpEndpointAndItsTwoEncodings()
    {
        await using var factory = new NebuLogAppFactory();
        using var client = await factory.SignInAsAsync(NebuLogRoles.Viewer);

        using var document = JsonDocument.Parse(await client.GetStringAsync(
            new Uri("/openapi/v1.json", UriKind.Relative), TestContext.Current.CancellationToken));

        var operation = document.RootElement
            .GetProperty("paths")
            .GetProperty("/v1/logs")
            .GetProperty("post");

        var content = operation.GetProperty("requestBody").GetProperty("content");
        Assert.True(content.TryGetProperty("application/x-protobuf", out _));
        Assert.True(content.TryGetProperty("application/json", out _));

        var responses = operation.GetProperty("responses");
        foreach (var status in new[] { "200", "400", "413", "415", "429", "503" })
        {
            Assert.True(responses.TryGetProperty(status, out _), $"{status} is missing from /v1/logs.");
        }
    }

    [Fact]
    public async Task UptimeCountsFromHostStartNotFromTheFirstRequest()
    {
        await using var factory = new NebuLogAppFactory();
        using var client = await factory.SignInAsAsync(NebuLogRoles.Viewer);
        var token = TestContext.Current.CancellationToken;

        // The host is already running by the time SignInAsAsync returns. Wait, then ask for the
        // first time: a start time captured lazily on first resolution would report zero here.
        await Task.Delay(TimeSpan.FromSeconds(1.5), token);

        var info = await client.GetFromJsonAsync<ServerInfoDto>(new Uri("/api/info", UriKind.Relative), token);

        Assert.True(info!.StartedUnixMs > 0);
        Assert.True(
            info.UptimeSeconds >= 1,
            $"Uptime was {info.UptimeSeconds}s, so the start time was captured on first use rather than at host start.");
    }

    [Fact]
    public async Task ReadinessReportsTheIngestQueue()
    {
        await using var factory = new NebuLogAppFactory();
        using var client = await factory.SignInAsAsync(NebuLogRoles.Viewer);

        using var response = await client.GetAsync(
            new Uri("/health/ready", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("Healthy", document.RootElement.GetProperty("status").GetString());
        Assert.True(document.RootElement.GetProperty("entries").TryGetProperty("ingest-queue", out _));
    }

    [Fact]
    public async Task LivenessRunsNoChecks()
    {
        await using var factory = new NebuLogAppFactory();
        using var client = await factory.SignInAsAsync(NebuLogRoles.Viewer);

        using var response = await client.GetAsync(
            new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Empty(document.RootElement.GetProperty("entries").EnumerateObject());
    }

    [Theory]
    [InlineData("/api/nope")]
    [InlineData("/hubs/nope")]
    [InlineData("/v1/nope")]
    [InlineData("/health/nope")]
    [InlineData("/openapi/nope")]
    [InlineData("/scalar/nope/deeper")]
    public async Task ReservedPrefixesDoNotFallBackToTheDashboard(string path)
    {
        await using var factory = new NebuLogAppFactory();
        using var client = await factory.SignInAsAsync(NebuLogRoles.Viewer);

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UnknownDashboardRouteServesIndexHtml()
    {
        await using var factory = new NebuLogAppFactory();
        using var client = await factory.SignInAsAsync(NebuLogRoles.Viewer);

        using var response = await client.GetAsync(
            new Uri("/logs/detail/42", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ApiRateLimitReturns429WithRetryAfter()
    {
        await using var factory = new NebuLogAppFactory(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["NebuLog:RateLimit:ApiPermitsPerWindow"] = "3",
            ["NebuLog:RateLimit:ApiWindow"] = "00:00:30",
        });
        using var client = await factory.SignInAsAsync(NebuLogRoles.Viewer);

        for (var i = 0; i < 3; i++)
        {
            using var allowed = await client.GetAsync(
                new Uri("/api/info", UriKind.Relative), TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        using var limited = await client.GetAsync(
            new Uri("/api/info", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.NotNull(limited.Headers.RetryAfter);
    }

    [Fact]
    public async Task ForwardedClientAddressIsAcceptedFromATrustedPeer()
    {
        // The peer is inside the trusted network, so CF-Connecting-IP wins and each spoofed
        // address gets its own rate-limit partition: three requests under a limit of two all pass.
        await using var factory = new NebuLogAppFactory(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["NebuLog:ForwardedHeaders:KnownNetworks"] = "172.30.20.0/24",
            ["NebuLog:RateLimit:ApiPermitsPerWindow"] = "2",
            ["NebuLog:RateLimit:ApiWindow"] = "00:00:30",
        });
        using var client = await factory.SignInAsAsync(NebuLogRoles.Viewer);

        for (var i = 0; i < 3; i++)
        {
            using var response = await SendAsync(client, peer: "172.30.20.1", forwardedFor: $"203.0.113.{i}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task ForwardedClientAddressIsIgnoredFromAnUntrustedPeer()
    {
        // Same headers, but the peer is outside the trusted network, so the header is ignored and
        // every request shares the peer's partition — the third is rejected.
        await using var factory = new NebuLogAppFactory(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["NebuLog:ForwardedHeaders:KnownNetworks"] = "172.30.20.0/24",
            ["NebuLog:RateLimit:ApiPermitsPerWindow"] = "2",
            ["NebuLog:RateLimit:ApiWindow"] = "00:00:30",
        });
        using var client = await factory.SignInAsAsync(NebuLogRoles.Viewer);

        HttpStatusCode? last = null;
        for (var i = 0; i < 3; i++)
        {
            using var response = await SendAsync(client, peer: "198.51.100.7", forwardedFor: $"203.0.113.{i}");
            last = response.StatusCode;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last);
    }

    [Fact]
    public async Task ForwardedHeadersStayOffWhenNoNetworkIsTrusted()
    {
        await using var factory = new NebuLogAppFactory(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["NebuLog:ForwardedHeaders:KnownNetworks"] = "",
            ["NebuLog:RateLimit:ApiPermitsPerWindow"] = "2",
            ["NebuLog:RateLimit:ApiWindow"] = "00:00:30",
        });
        using var client = await factory.SignInAsAsync(NebuLogRoles.Viewer);

        HttpStatusCode? last = null;
        for (var i = 0; i < 3; i++)
        {
            using var response = await SendAsync(client, peer: "172.30.20.1", forwardedFor: $"203.0.113.{i}");
            last = response.StatusCode;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string peer, string forwardedFor)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/info", UriKind.Relative));
        request.Headers.Add(TestPeerAddressStartupFilter.HeaderName, peer);
        request.Headers.Add("CF-Connecting-IP", forwardedFor);

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UnhandledExceptionsBecomeProblemDetailsWithoutAStackTrace()
    {
        await using var factory = new NebuLogAppFactory(withThrowingEndpoint: true);
        using var client = await factory.SignInAsAsync(NebuLogRoles.Viewer);

        using var response = await client.GetAsync(
            new Uri(ThrowingEndpointStartupFilter.Path, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("An unexpected error occurred.", problem.GetProperty("title").GetString());
        Assert.Equal(500, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));

        // Outside Development the payload must not leak the exception type or stack.
        var body = problem.ToString();
        Assert.DoesNotContain("InvalidOperationException", body, StringComparison.Ordinal);
        Assert.DoesNotContain("deliberate failure", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidationProblemsAreReturnedAsProblemDetails()
    {
        await using var factory = new NebuLogAppFactory();
        using var client = await factory.SignInAsAsync(NebuLogRoles.Viewer);

        using var response = await client.GetAsync(
            new Uri("/api/logs?limit=abc", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.True(problem.TryGetProperty("errors", out var errors));
        Assert.True(errors.TryGetProperty("limit", out _));
    }
}
