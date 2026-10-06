using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace NebuShop.Orders;

/// <summary>
/// Everything the two pipelines have in common. Both the Minimal API endpoints and the MVC
/// controller call this, so the business log lines are identical for an identical request and the
/// only difference the console shows is what the framework itself writes.
/// </summary>
public sealed class OrderService
{
    /// <summary>Name of the <see cref="HttpClient"/> used to reach the payments service.</summary>
    public const string PaymentsClientName = "payments";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<ShopOptions> _options;
    private readonly ILogger<OrderService> _logger;

    /// <summary>Creates the service.</summary>
    /// <param name="httpClientFactory">Factory for the payments client.</param>
    /// <param name="options">Shop settings.</param>
    /// <param name="logger">Where the business log lines go.</param>
    public OrderService(
        IHttpClientFactory httpClientFactory,
        IOptions<ShopOptions> options,
        ILogger<OrderService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    /// <summary>Looks up one order. Scenario 01.</summary>
    /// <param name="id">The order identifier.</param>
    /// <param name="pipeline">Which implementation is serving the request.</param>
    /// <returns>The order, or <see langword="null"/> when it does not exist.</returns>
    public OrderView? GetOrder(int id, string pipeline)
    {
        Log.OrderLookupStarted(_logger, id, pipeline);

        if (id <= 0)
        {
            Log.OrderNotFound(_logger, id);
            return null;
        }

        // A deterministic stand-in for a database: the id picks the product.
        var product = Catalog.Products[Math.Abs(id) % Catalog.Products.Count];
        var quantity = (Math.Abs(id) % 3) + 1;
        var order = new OrderView(id, product.Sku, product.Name, quantity, product.PriceNzd * quantity);

        Log.OrderReturned(_logger, order.Id, order.Sku, order.Quantity, order.TotalNzd);
        return order;
    }

    /// <summary>
    /// Places an order, rejecting a non-positive quantity. Scenario 03.
    /// </summary>
    /// <param name="request">The order to place.</param>
    /// <returns>
    /// The created order, or the name of the field that failed validation together with the reason.
    /// </returns>
    public (OrderView? Order, string? Field, string? Error) PlaceOrder(PlaceOrderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Quantity <= 0)
        {
            // The field name travels as an attribute, so the console can filter on it.
            Log.OrderRejected(_logger, nameof(request.Quantity), request.Quantity, request.Sku);
            return (null, nameof(PlaceOrderRequest.Quantity), "Quantity must be greater than zero.");
        }

        var product = Catalog.Find(request.Sku);
        if (product is null)
        {
            Log.OrderRejected(_logger, nameof(request.Sku), 0, request.Sku);
            return (null, nameof(PlaceOrderRequest.Sku), $"There is no product with code '{request.Sku}'.");
        }

        var id = Random.Shared.Next(1000, 9999);
        var order = new OrderView(id, product.Sku, product.Name, request.Quantity, product.PriceNzd * request.Quantity);
        Log.OrderPlaced(_logger, order.Id, order.Sku, order.Quantity, order.TotalNzd);
        return (order, null, null);
    }

    /// <summary>
    /// Checks out a basket by calling the payments service. Scenario 02.
    /// </summary>
    /// <param name="request">The basket.</param>
    /// <param name="cancellationToken">Cancels the outbound call.</param>
    /// <returns>The checkout result, or the reason it failed.</returns>
    /// <remarks>
    /// The outbound <see cref="HttpClient"/> call carries <c>traceparent</c> automatically, which is
    /// what makes both services' entries share one TraceId — the whole point of this scenario.
    /// </remarks>
    public async Task<(CheckoutResult? Result, string? Error)> CheckoutAsync(
        CheckoutRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Lines.Count == 0)
        {
            Log.CheckoutEmpty(_logger);
            return (null, "The basket is empty.");
        }

        var total = 0m;
        foreach (var line in request.Lines)
        {
            var product = Catalog.Find(line.Sku);
            if (product is null)
            {
                Log.CheckoutUnknownProduct(_logger, line.Sku);
                return (null, $"There is no product with code '{line.Sku}'.");
            }

            total += product.PriceNzd * Math.Max(1, line.Quantity);
        }

        var orderId = Random.Shared.Next(1000, 9999);
        Log.CheckoutStarted(_logger, orderId, request.Lines.Count, total);

        var client = _httpClientFactory.CreateClient(PaymentsClientName);
        var response = await client
            .PostAsJsonAsync("/api/payments/authorise", new { orderId, amountNzd = total }, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            Log.PaymentFailed(_logger, orderId, (int)response.StatusCode);
            return (null, "The payment service declined the authorisation.");
        }

        var authorisation = await response.Content
            .ReadFromJsonAsync<PaymentAuthorisation>(cancellationToken)
            .ConfigureAwait(false);

        if (authorisation is null)
        {
            Log.PaymentFailed(_logger, orderId, (int)response.StatusCode);
            return (null, "The payment service returned no authorisation.");
        }

        Log.CheckoutCompleted(_logger, orderId, authorisation.AuthorisationCode, total, authorisation.ElapsedMs);
        return (new CheckoutResult(orderId, total, authorisation.AuthorisationCode, authorisation.ElapsedMs), null);
    }

    /// <summary>
    /// Signs in, recording the password as an attribute so the redaction processor can mask it.
    /// Scenario 05.
    /// </summary>
    /// <param name="request">The credentials.</param>
    /// <remarks>
    /// The password is attached as structured state and is deliberately absent from the message
    /// template. That ordering is the whole demonstration: <c>RedactionProcessor</c> rewrites
    /// attribute values, and a value already formatted into the message text is beyond its reach,
    /// because by then it is indistinguishable from the rest of the sentence (WO-0007 §9).
    /// So the rule is not "redaction will catch it" but "never format a secret into the message".
    /// </remarks>
    public void SignIn(SignInRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Written with ILogger.Log rather than [LoggerMessage] on purpose: the source generator
        // refuses a parameter that the template does not mention (SYSLIB1015), and here that is
        // exactly what is needed — the attribute must exist without appearing in the prose.
        _logger.Log(
            LogLevel.Information,
            new EventId(20, nameof(SignIn)),
            new List<KeyValuePair<string, object?>>
            {
                new("Username", request.Username),
                new("Password", request.Password),
                new("AuthMethod", "password"),
            },
            exception: null,
            formatter: static (state, _) =>
            {
                var username = state.FirstOrDefault(pair => pair.Key == "Username").Value;
                return string.Create(
                    CultureInfo.InvariantCulture,
                    $"Sign-in attempt for {username} (credentials travel as attributes, not as text)");
            });

        Log.SignInCompleted(_logger, request.Username);
    }

    /// <summary>Throws, so the host's exception handler can turn it into a 500. Scenario 04.</summary>
    /// <exception cref="InvalidOperationException">Always.</exception>
    public static void Break() =>
        throw new InvalidOperationException("The order ledger is unavailable (this button is meant to fail).");

    private sealed record PaymentAuthorisation(string AuthorisationCode, decimal AmountNzd, int ElapsedMs, string TraceId);
}

/// <summary>The shop's business log lines, shared by both pipelines.</summary>
internal static partial class Log
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
        Message = "Looking up order {OrderId} via the {Pipeline} pipeline")]
    public static partial void OrderLookupStarted(ILogger logger, int orderId, string pipeline);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information,
        Message = "Order {OrderId}: {Quantity} x {Sku}, {TotalNzd:F2} NZD")]
    public static partial void OrderReturned(ILogger logger, int orderId, string sku, int quantity, decimal totalNzd);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Order {OrderId} not found")]
    public static partial void OrderNotFound(ILogger logger, int orderId);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning,
        Message = "Order rejected: {Field} was {Quantity} for {Sku}")]
    public static partial void OrderRejected(ILogger logger, string field, int quantity, string sku);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information,
        Message = "Order {OrderId} placed: {Quantity} x {Sku}, {TotalNzd:F2} NZD")]
    public static partial void OrderPlaced(ILogger logger, int orderId, string sku, int quantity, decimal totalNzd);

    [LoggerMessage(EventId = 6, Level = LogLevel.Information,
        Message = "Checkout {OrderId} started: {LineCount} lines, {TotalNzd:F2} NZD")]
    public static partial void CheckoutStarted(ILogger logger, int orderId, int lineCount, decimal totalNzd);

    [LoggerMessage(EventId = 7, Level = LogLevel.Information,
        Message = "Checkout {OrderId} authorised as {AuthorisationCode}, {TotalNzd:F2} NZD in {PaymentMs} ms")]
    public static partial void CheckoutCompleted(
        ILogger logger,
        int orderId,
        string authorisationCode,
        decimal totalNzd,
        int paymentMs);

    [LoggerMessage(EventId = 8, Level = LogLevel.Error,
        Message = "Checkout {OrderId} failed: the payment service answered {StatusCode}")]
    public static partial void PaymentFailed(ILogger logger, int orderId, int statusCode);

    [LoggerMessage(EventId = 9, Level = LogLevel.Warning, Message = "Checkout attempted with an empty basket")]
    public static partial void CheckoutEmpty(ILogger logger);

    [LoggerMessage(EventId = 10, Level = LogLevel.Warning,
        Message = "Checkout contains an unknown product {Sku}")]
    public static partial void CheckoutUnknownProduct(ILogger logger, string sku);

    [LoggerMessage(EventId = 11, Level = LogLevel.Information, Message = "{Username} signed in")]
    public static partial void SignInCompleted(ILogger logger, string username);

    [LoggerMessage(EventId = 12, Level = LogLevel.Information,
        Message = "Burst started: {EntriesPerSecond} entries/s for {Seconds} s")]
    public static partial void BurstStarted(ILogger logger, int entriesPerSecond, int seconds);

    [LoggerMessage(EventId = 13, Level = LogLevel.Information,
        Message = "Burst finished after {Emitted} entries")]
    public static partial void BurstFinished(ILogger logger, int emitted);

    [LoggerMessage(EventId = 14, Level = LogLevel.Debug,
        Message = "Burst entry {Index} of {Total}, basket {BasketId}")]
    public static partial void BurstEntry(ILogger logger, int index, int total, int basketId);

    [LoggerMessage(EventId = 15, Level = LogLevel.Error, Message = "The burst ended unexpectedly")]
    public static partial void BurstFailed(ILogger logger, Exception exception);
}
