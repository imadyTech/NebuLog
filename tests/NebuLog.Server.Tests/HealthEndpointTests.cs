using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace NebuLog.Server.Tests;

public sealed class HealthEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task LivenessEndpointReturnsOk()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            new Uri("/health/live", UriKind.Relative),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
