using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using NebuShop.Orders;
using Xunit;

namespace NebuShop.Tests;

/// <summary>
/// The scenarios the guided demo promises, asserted against both implementations.
/// </summary>
/// <remarks>
/// Every behavioural test runs against <c>minimal</c> and <c>mvc</c>. The two pipelines exist to be
/// compared, and a comparison is only honest if both sides do the same thing.
/// </remarks>
public sealed class ShopEndpointTests
{
    public static TheoryData<string> Pipelines => new() { "minimal", "mvc" };

    [Theory]
    [MemberData(nameof(Pipelines))]
    public async Task OpeningAnOrderReturnsItAndCarriesTheTraceHeader(string api)
    {
        using var factory = new OrdersFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri($"/api/{api}/orders/42", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var order = await response.Content.ReadFromJsonAsync<OrderView>(TestContext.Current.CancellationToken);
        Assert.NotNull(order);
        Assert.Equal(42, order.Id);

        // The shop window shows this value and the console filters on it.
        Assert.True(response.Headers.TryGetValues("X-Trace-Id", out var traceIds));
        Assert.NotEmpty(Assert.Single(traceIds));
    }

    [Theory]
    [MemberData(nameof(Pipelines))]
    public async Task AQuantityOfZeroIsRejectedWithProblemDetailsAndAWarning(string api)
    {
        using var factory = new OrdersFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            new Uri($"/api/{api}/orders", UriKind.Relative),
            new { sku = "MER-0101", quantity = 0 },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("application/problem+json", response.Content.Headers.ContentType?.MediaType ?? string.Empty,
            StringComparison.Ordinal);

        // The failing field travels as an attribute so the console can filter on it.
        var warning = Assert.Single(
            factory.Logs.Entries,
            entry => entry.Level == LogLevel.Warning && entry.Attributes.ContainsKey("Field"));
        Assert.Equal("Quantity", warning.Attributes["Field"]);
    }

    [Theory]
    [MemberData(nameof(Pipelines))]
    public async Task TheBrokenButtonProducesA500AndAnErrorEntryWithTheException(string api)
    {
        using var factory = new OrdersFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(new Uri($"/api/{api}/broken", UriKind.Relative), null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains(
            factory.Logs.Entries,
            entry => entry.Level == LogLevel.Error && entry.Message.Contains("Unhandled", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Pipelines))]
    public async Task AnUnknownOrderIsNotFound(string api)
    {
        using var factory = new OrdersFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri($"/api/{api}/orders/0", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TheMvcPipelineProducesMoreFrameworkEntriesThanMinimalApiForTheSameRequest()
    {
        // Scenario 01's entire claim. If this ever stops holding, the scenario's description is
        // wrong and the demo would be teaching something false.
        var minimal = await CountFrameworkEntriesAsync("minimal");
        var mvc = await CountFrameworkEntriesAsync("mvc");

        Assert.True(
            mvc > minimal,
            string.Create(
                CultureInfo.InvariantCulture,
                $"MVC produced {mvc} framework entries and Minimal API produced {minimal}; scenario 01 claims MVC produces more."));
    }

    [Fact]
    public async Task OnlyOneBurstRunsAtATime()
    {
        using var factory = new OrdersFactory(settings: new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["NebuShop:BurstEntriesPerSecond"] = "10",
            ["NebuShop:BurstSeconds"] = "30",
        });
        using var client = factory.CreateClient();

        using var first = await client.PostAsync(new Uri("/api/burst", UriKind.Relative), null, TestContext.Current.CancellationToken);
        using var second = await client.PostAsync(new Uri("/api/burst", UriKind.Relative), null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var busy = await second.Content.ReadFromJsonAsync<BurstBusy>(TestContext.Current.CancellationToken);
        Assert.NotNull(busy);
        Assert.True(busy.SecondsRemaining > 0);
    }

    [Fact]
    public async Task TheCatalogueIsServedForTheShopPage()
    {
        using var factory = new OrdersFactory();
        using var client = factory.CreateClient();

        var products = await client.GetFromJsonAsync<IReadOnlyList<Product>>(
            new Uri("/api/catalog", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.NotNull(products);
        Assert.Equal(3, products.Count);
    }

    private static async Task<int> CountFrameworkEntriesAsync(string api)
    {
        using var factory = new OrdersFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri($"/api/{api}/orders/42", UriKind.Relative), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        return factory.Logs.Entries.Count(entry =>
            entry.Category.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) ||
            entry.Category.StartsWith("NebuShop.Orders.ShopActionFilter", StringComparison.Ordinal));
    }
}
