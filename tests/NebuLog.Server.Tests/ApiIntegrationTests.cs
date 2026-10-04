using System.Net;
using Microsoft.AspNetCore.SignalR.Client;
using System.Net.Http.Json;
using NebuLog.Contracts;
using NebuLog.Server.Api;
using NebuLog.Server.Diagnostics;
using NebuLog.Server.Hubs;
using Xunit;

namespace NebuLog.Server.Tests;

public sealed class ApiIntegrationTests
{
    private static NebuLogEntry Entry(string body, int severity, string service) =>
        new() { Body = body, SeverityNumber = severity, ServiceName = service };

    private static async Task<(NebuLogAppFactory Factory, HttpClient Client)> SeedAsync()
    {
        var factory = new NebuLogAppFactory(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            // Keep the API window generous so seeding does not trip the limiter.
            ["NebuLog:RateLimit:ApiPermitsPerWindow"] = "1000",
        });

        await using (var viewer = await factory.ConnectAsync("viewer"))
        await using (var producer = await factory.ConnectAsync("producer", "orders"))
        {
            await HubIntegrationTests.PublishAndWaitAsync(viewer, producer,
            [
                Entry("cart opened", Severity.Debug, "orders"),
                Entry("payment DECLINED", Severity.Error, "orders"),
                Entry("label failed", Severity.Error, "shipping"),
            ]);
        }

        return (factory, factory.CreateClient());
    }

    [Fact]
    public async Task LogsEndpointFiltersAndPages()
    {
        var (factory, client) = await SeedAsync();
        await using var _ = factory;
        using var __ = client;
        var token = TestContext.Current.CancellationToken;

        var all = await client.GetFromJsonAsync<List<NebuLogEntry>>("/api/logs", token);
        Assert.Equal(3, all!.Count);

        var errors = await client.GetFromJsonAsync<List<NebuLogEntry>>($"/api/logs?minSeverity={Severity.Error}", token);
        Assert.Equal(2, errors!.Count);

        var orders = await client.GetFromJsonAsync<List<NebuLogEntry>>("/api/logs?service=orders", token);
        Assert.Equal(2, orders!.Count);

        var searched = await client.GetFromJsonAsync<List<NebuLogEntry>>("/api/logs?search=payment", token);
        Assert.Equal("payment DECLINED", Assert.Single(searched!).Body);

        var firstPage = await client.GetFromJsonAsync<List<NebuLogEntry>>("/api/logs?limit=2", token);
        Assert.Equal(2, firstPage!.Count);

        var nextPage = await client.GetFromJsonAsync<List<NebuLogEntry>>(
            $"/api/logs?afterId={firstPage[^1].Id}", token);
        Assert.Equal(3, Assert.Single(nextPage!).Id);
    }

    [Theory]
    [InlineData("/api/logs?limit=0")]
    [InlineData("/api/logs?limit=99999")]
    [InlineData("/api/logs?limit=abc")]
    [InlineData("/api/logs?minSeverity=99")]
    [InlineData("/api/logs?afterId=-1")]
    public async Task LogsEndpointRejectsBadQueryParameters(string url)
    {
        var (factory, client) = await SeedAsync();
        await using var _ = factory;
        using var __ = client;

        using var response = await client.GetAsync(new Uri(url, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("application/problem+json", response.Content.Headers.ContentType?.MediaType, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SummaryClientsStatsAndInfoReportTheServerState()
    {
        var (factory, client) = await SeedAsync();
        await using var _ = factory;
        using var __ = client;
        var token = TestContext.Current.CancellationToken;

        var summary = await client.GetFromJsonAsync<LiveSummaryDto>("/api/summary", token);
        Assert.Equal(3, summary!.TotalIngested);
        Assert.Equal(2, summary.CountsByBand["Error"]);
        Assert.Equal(LiveSummary.RateWindowSeconds, summary.RatePerSecond.Count);
        Assert.Equal(["orders", "shipping"], summary.Services);

        // Both hub connections were disposed by the seeding helper.
        var clients = await client.GetFromJsonAsync<List<ConnectedClientInfo>>("/api/clients", token);
        Assert.Empty(clients!);

        var stats = await client.GetFromJsonAsync<List<StatSnapshot>>("/api/custom-stats", token);
        Assert.Empty(stats!);

        var info = await client.GetFromJsonAsync<ServerInfoDto>("/api/info", token);
        Assert.Equal(3, info!.BufferedCount);
        Assert.True(info.BufferCapacity >= 1_000);
        Assert.True(info.StartedUnixMs > 0);
        Assert.False(string.IsNullOrWhiteSpace(info.Version));
    }

    [Fact]
    public async Task CustomStatsSurviveTheProducerDisconnecting()
    {
        await using var factory = new NebuLogAppFactory();
        await using (var producer = await factory.ConnectAsync("producer", "orders"))
        {
            await producer.InvokeAsync(
                HubRoutes.DefineStat,
                new StatDefinition { Id = "rps", Title = "Requests/s", Color = "#0af" },
                TestContext.Current.CancellationToken);
            await producer.InvokeAsync(
                HubRoutes.UpdateStat,
                new StatUpdate { Id = "rps", Value = "42", TimestampUnixMs = 7 },
                TestContext.Current.CancellationToken);
        }

        using var client = factory.CreateClient();
        var stats = await client.GetFromJsonAsync<List<StatSnapshot>>(
            "/api/custom-stats", TestContext.Current.CancellationToken);

        var snapshot = Assert.Single(stats!);
        Assert.Equal("Requests/s", snapshot.Definition.Title);
        Assert.Equal("42", snapshot.Latest!.Value);
    }
}
