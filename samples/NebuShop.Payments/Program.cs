using System.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using NebuLog.OpenTelemetry;
using NebuShop.Payments;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddOptions<PaymentOptions>()
    .Bind(builder.Configuration.GetSection(PaymentOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(
        serviceName: "shop-payments",
        serviceInstanceId: Environment.MachineName))
    // AspNetCore instrumentation is what continues the caller's trace: it reads the inbound
    // `traceparent` header, so every entry below shares the order service's TraceId.
    .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation())
    .WithLogging(logging =>
    {
        logging.AddNebuLogRedaction();
        logging.AddNebuLogExporter(options =>
        {
            options.Endpoint = new Uri(builder.Configuration["NebuLog:Endpoint"] ?? "http://localhost:5080");
            options.ApiKey = builder.Configuration["NebuLog:ShopPayments:ApiKey"] ?? string.Empty;
        });
    });

builder.Services.AddNebuLogClient();

var app = builder.Build();

app.MapPost("/api/payments/authorise", async Task<Results<Ok<AuthorisationResult>, BadRequest<string>>> (
    AuthorisationRequest request,
    IOptions<PaymentOptions> options,
    ILogger<AuthorisationRequest> logger,
    TimeProvider timeProvider,
    CancellationToken cancellationToken) =>
{
    var settings = options.Value;

    if (request.AmountNzd <= 0)
    {
        Log.AmountRejected(logger, request.OrderId, request.AmountNzd);
        return TypedResults.BadRequest("Amount must be greater than zero.");
    }

    Log.AuthorisationStarted(logger, request.OrderId, request.AmountNzd);

    // A gateway that sometimes takes longer than we would like, so the scenario has something
    // worth noticing: the warning below is the point of the exercise.
    var latency = Random.Shared.Next(settings.MinLatencyMs, settings.MaxLatencyMs + 1);
    var started = timeProvider.GetTimestamp();
    await Task.Delay(TimeSpan.FromMilliseconds(latency), timeProvider, cancellationToken).ConfigureAwait(false);
    var elapsed = timeProvider.GetElapsedTime(started);

    if (elapsed.TotalMilliseconds > settings.SlowThresholdMs)
    {
        Log.GatewaySlow(logger, request.OrderId, (int)elapsed.TotalMilliseconds, settings.SlowThresholdMs);
    }

    var authorisation = $"AUTH-{Random.Shared.Next(100000, 999999)}";
    Log.AuthorisationGranted(logger, request.OrderId, authorisation, (int)elapsed.TotalMilliseconds);

    return TypedResults.Ok(new AuthorisationResult(
        authorisation,
        request.AmountNzd,
        (int)elapsed.TotalMilliseconds,
        Activity.Current?.TraceId.ToString() ?? string.Empty));
});

app.MapGet("/health/live", () => TypedResults.Ok(new { status = "ok" }));

await app.RunAsync();

namespace NebuShop.Payments
{
    /// <summary>A request to authorise payment for one order.</summary>
    /// <param name="OrderId">The order being paid for.</param>
    /// <param name="AmountNzd">Amount in New Zealand dollars.</param>
    public sealed record AuthorisationRequest(int OrderId, decimal AmountNzd);

    /// <summary>The outcome of an authorisation.</summary>
    /// <param name="AuthorisationCode">The gateway's reference.</param>
    /// <param name="AmountNzd">Amount authorised.</param>
    /// <param name="ElapsedMs">How long the gateway took.</param>
    /// <param name="TraceId">The trace this authorisation belongs to.</param>
    public sealed record AuthorisationResult(
        string AuthorisationCode,
        decimal AmountNzd,
        int ElapsedMs,
        string TraceId);

    internal static partial class Log
    {
        [LoggerMessage(EventId = 1, Level = LogLevel.Information,
            Message = "Authorising {AmountNzd:F2} NZD for order {OrderId}")]
        public static partial void AuthorisationStarted(ILogger logger, int orderId, decimal amountNzd);

        [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
            Message = "Payment gateway took {ElapsedMs} ms for order {OrderId}, over the {ThresholdMs} ms threshold")]
        public static partial void GatewaySlow(ILogger logger, int orderId, int elapsedMs, int thresholdMs);

        [LoggerMessage(EventId = 3, Level = LogLevel.Information,
            Message = "Order {OrderId} authorised as {AuthorisationCode} in {ElapsedMs} ms")]
        public static partial void AuthorisationGranted(
            ILogger logger,
            int orderId,
            string authorisationCode,
            int elapsedMs);

        [LoggerMessage(EventId = 4, Level = LogLevel.Warning,
            Message = "Order {OrderId} rejected: amount {AmountNzd:F2} is not positive")]
        public static partial void AmountRejected(ILogger logger, int orderId, decimal amountNzd);
    }
}
