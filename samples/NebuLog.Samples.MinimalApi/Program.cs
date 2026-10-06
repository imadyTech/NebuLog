using System.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;
using NebuLog.Contracts;
using NebuLog.OpenTelemetry;
using NebuLog.Samples.MinimalApi;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Which transport carries the logs. Either, or both at once — they converge on the server.
//   Exporter : NebuLogExporter over SignalR (lowest latency, and the only one that can also
//              receive commands and publish live statistics).
//   Otlp     : the official OTLP/HTTP exporter, i.e. any OpenTelemetry SDK in any language.
var transports = builder.Configuration["NebuLog:Transports"] ?? "Exporter,Otlp";
var useExporter = transports.Contains("Exporter", StringComparison.OrdinalIgnoreCase);
var useOtlp = transports.Contains("Otlp", StringComparison.OrdinalIgnoreCase);

var endpoint = new Uri(builder.Configuration["NebuLog:Endpoint"] ?? "http://localhost:5080");
var apiKey = builder.Configuration["NebuLog:ApiKey"] ?? string.Empty;

var levelSwitch = new LogLevelSwitch();
builder.Services.AddSingleton(levelSwitch);
builder.Services.AddSingleton<RequestCounter>();
builder.Logging.AddFilter((_, level) => levelSwitch.IsEnabled(level));

var otel = builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(
        serviceName: "sample-orders-api",
        serviceInstanceId: Environment.MachineName))
    .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation())
    .WithLogging(logging =>
    {
        // Redaction is registered first on purpose: processors run in order, so a sensitive
        // value is masked before the exporter ever sees it.
        logging.AddNebuLogRedaction();

        if (useExporter)
        {
            logging.AddNebuLogExporter(options =>
            {
                options.Endpoint = endpoint;
                options.ApiKey = apiKey;
            });
        }

        if (useOtlp)
        {
            logging.AddOtlpExporter(options =>
            {
                options.Protocol = OtlpExportProtocol.HttpProtobuf;
                options.Endpoint = new Uri(endpoint, "v1/logs");
                options.Headers = $"X-Api-Key={apiKey}";
            });
        }
    });

if (useExporter)
{
    // Only the SignalR transport can carry statistics and inbound commands.
    builder.Services.AddNebuLogClient();
    builder.Services.AddHostedService<OrdersTelemetry>();
}

var app = builder.Build();

app.MapGet("/orders/{id:int}", Results<Ok<Order>, NotFound> (int id, ILogger<Order> logger, RequestCounter counter) =>
{
    counter.Record();

    // ASP.NET Core's own Activity is already current here, so every entry below carries the
    // request's TraceId and the dashboard can group them.
    Log.OrderRequested(logger, id, Activity.Current?.TraceId.ToString() ?? "none");

    if (id % 7 == 0)
    {
        Log.OrderMissing(logger, id);
        return TypedResults.NotFound();
    }

    return TypedResults.Ok(new Order(id, "flat white", 5.50m));
});

app.MapPost("/sign-in", (SignInRequest request, ILogger<SignInRequest> logger) =>
{
    // What RedactionProcessor does, and what it cannot do.
    //
    // The {Password} attribute arrives at the dashboard as "***": the processor masks it before
    // the entry leaves the process, on both transports.
    //
    // The rendered message still reads "... with hunter2", because the logging pipeline formats
    // the text before any processor sees the record. [LoggerMessage] will not even let you pass a
    // parameter that is absent from the template (SYSLIB1015), so with source-generated logging
    // there is no way to have the attribute without also having it in the prose.
    //
    // The rule that follows is therefore not "redaction will catch it" but "do not log secrets".
    // Redaction is a safety net for attributes that happen to be named sensitively — a backstop
    // for the case you missed, not a licence to log credentials on purpose.
    Log.SignInAttempt(logger, request.Email, request.Password);

    return TypedResults.Accepted("/orders/1");
});

app.MapGet("/boom", (ILogger<Order> logger) =>
{
    try
    {
        throw new InvalidOperationException("The roaster is offline.");
    }
    catch (InvalidOperationException exception)
    {
        Log.RoasterOffline(logger, exception);
    }

    return TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable);
});

await app.RunAsync();

/// <summary>An order returned by the sample API.</summary>
/// <param name="Id">Order identifier.</param>
/// <param name="Product">What was ordered.</param>
/// <param name="Price">Price in NZD.</param>
public sealed record Order(int Id, string Product, decimal Price);

/// <summary>A deliberately naive sign-in request, used to demonstrate redaction.</summary>
/// <param name="Email">The user's address.</param>
/// <param name="Password">The password, which must never reach the log.</param>
public sealed record SignInRequest(string Email, string Password);

internal static partial class Log
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Order {OrderId} requested (trace {TraceId})")]
    public static partial void OrderRequested(ILogger logger, int orderId, string traceId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Order {OrderId} not found")]
    public static partial void OrderMissing(ILogger logger, int orderId);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information,
        Message = "Sign-in attempt for {Email} with {Password}")]
    public static partial void SignInAttempt(ILogger logger, string email, string password);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "The roaster is offline")]
    public static partial void RoasterOffline(ILogger logger, Exception exception);
}
