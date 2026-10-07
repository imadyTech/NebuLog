using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using NebuShop.Orders;
using Xunit;

namespace NebuShop.Tests;

/// <summary>
/// Scenario 02: one checkout, two services, one TraceId.
/// </summary>
public sealed class CrossServiceTraceTests
{
    [Theory]
    [InlineData("minimal")]
    [InlineData("mvc")]
    public async Task ACheckoutProducesEntriesFromBothServicesUnderOneTraceId(string api)
    {
        using var payments = new PaymentsFactory();
        _ = payments.CreateClient();

        using var orders = new OrdersFactory(payments);
        using var client = orders.CreateClient();

        using var response = await client.PostAsJsonAsync(
            new Uri($"/api/{api}/checkout", UriKind.Relative),
            new { lines = new[] { new { sku = "MER-0101", quantity = 2 } } },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CheckoutResult>(TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.NotEmpty(result.AuthorisationCode);

        var orderTraces = TracesOf(orders, "Checkout");
        var paymentTraces = TracesOf(payments, "Authorising");

        Assert.NotEmpty(orderTraces);
        Assert.NotEmpty(paymentTraces);

        // The actual claim: not merely that both logged, but that the two sets of entries name the
        // same trace. Without traceparent propagation each service would invent its own.
        Assert.True(
            orderTraces.Overlaps(paymentTraces),
            $"Order traces [{string.Join(", ", orderTraces)}] and payment traces [{string.Join(", ", paymentTraces)}] share none.");
    }

    [Fact]
    public async Task AnEmptyBasketIsRejectedBeforeThePaymentServiceIsCalled()
    {
        using var payments = new PaymentsFactory();
        _ = payments.CreateClient();

        using var orders = new OrdersFactory(payments);
        using var client = orders.CreateClient();

        using var response = await client.PostAsJsonAsync(
            new Uri("/api/minimal/checkout", UriKind.Relative),
            new { lines = Array.Empty<object>() },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain(
            payments.Logs.Entries,
            entry => entry.Message.Contains("Authorising", StringComparison.Ordinal));
    }

    private static HashSet<string> TracesOf(OrdersFactory factory, string messageFragment) =>
        [.. factory.Logs.Entries
            .Where(entry => entry.Message.Contains(messageFragment, StringComparison.Ordinal))
            .Select(entry => entry.TraceId)
            .Where(trace => !string.IsNullOrEmpty(trace))];

    private static HashSet<string> TracesOf(PaymentsFactory factory, string messageFragment) =>
        [.. factory.Logs.Entries
            .Where(entry => entry.Level >= LogLevel.Information
                && entry.Message.Contains(messageFragment, StringComparison.Ordinal))
            .Select(entry => entry.TraceId)
            .Where(trace => !string.IsNullOrEmpty(trace))];
}
