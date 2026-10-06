using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using NebuLog.OpenTelemetry;
using NebuShop.Orders;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddOptions<ShopOptions>()
    .Bind(builder.Configuration.GetSection(ShopOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(
        serviceName: "shop-orders",
        serviceInstanceId: Environment.MachineName))
    .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation())
    .WithLogging(logging =>
    {
        // Redaction first: processors run in order, so the sign-in scenario's password is masked
        // before the exporter ever sees the record.
        logging.AddNebuLogRedaction();
        logging.AddNebuLogExporter(options =>
        {
            options.Endpoint = new Uri(builder.Configuration["NebuLog:Endpoint"] ?? "http://localhost:5080");
            options.ApiKey = builder.Configuration["NebuLog:ShopOrders:ApiKey"] ?? string.Empty;
        });
    });

builder.Services.AddNebuLogClient();

// The payments client is resolved from configuration at request time rather than at registration
// time: reading it here would freeze whatever the configuration happened to hold during start-up.
builder.Services.AddHttpClient(OrderService.PaymentsClientName)
    .ConfigureHttpClient((provider, client) =>
    {
        var options = provider.GetRequiredService<IOptions<ShopOptions>>().Value;
        client.BaseAddress = new Uri(options.PaymentsUrl);
        client.Timeout = TimeSpan.FromSeconds(10);
    })
    .AddHttpMessageHandler<TraceContextPropagationHandler>();

builder.Services.AddTransient<TraceContextPropagationHandler>();

builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<ShopActionFilter>();
builder.Services.AddSingleton<BurstRunner>();
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ShopExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();

// Every response carries the trace it belongs to, so the shop window can show it and the console
// can be asked to filter on it.
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var traceId = Activity.Current?.TraceId.ToString();
        if (!string.IsNullOrEmpty(traceId))
        {
            context.Response.Headers["X-Trace-Id"] = traceId;
        }

        return Task.CompletedTask;
    });

    await next(context).ConfigureAwait(false);
});

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapShopMinimalApi();
app.MapControllers();

// Shared by both pipelines: a burst is a property of the process, not of one implementation.
app.MapPost("/api/burst", Results<Accepted<BurstAccepted>, Conflict<BurstBusy>> (BurstRunner burst) =>
{
    var accepted = burst.TryStart();

    return accepted is not null
        ? TypedResults.Accepted("/api/burst", accepted)
        : TypedResults.Conflict(new BurstBusy(burst.SecondsRemaining));
});

app.MapGet("/api/catalog", () => TypedResults.Ok(Catalog.Products));

app.MapGet("/health/live", () => TypedResults.Ok(new { status = "ok" }));

// The shop is a single page; its client-side state lives in the query string.
app.MapFallbackToFile("index.html");

await app.RunAsync();

namespace NebuShop.Orders
{
    /// <summary>Returned when a burst is already running.</summary>
    /// <param name="SecondsRemaining">How long until the running burst finishes.</param>
    public sealed record BurstBusy(int SecondsRemaining);

    /// <summary>Turns an unhandled exception into a ProblemDetails response, and logs it.</summary>
    /// <remarks>
    /// Scenario 04 depends on this: the visitor presses a button that fails on purpose, and the
    /// console is supposed to show one Error entry carrying the exception type, message and stack.
    /// </remarks>
    internal sealed class ShopExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<ShopExceptionHandler> _logger;

        public ShopExceptionHandler(ILogger<ShopExceptionHandler> logger) => _logger = logger;

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(httpContext);

            ShopExceptionLog.Unhandled(_logger, exception, httpContext.Request.Path);

            httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await httpContext.Response.WriteAsJsonAsync(
                new
                {
                    type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.6.1",
                    title = "The request failed.",
                    status = StatusCodes.Status500InternalServerError,
                    instance = httpContext.Request.Path.Value,
                    traceId = Activity.Current?.TraceId.ToString(),
                },
                cancellationToken).ConfigureAwait(false);

            return true;
        }
    }

    internal static partial class ShopExceptionLog
    {
        [LoggerMessage(EventId = 40, Level = LogLevel.Error, Message = "Unhandled exception for {Path}")]
        public static partial void Unhandled(ILogger logger, Exception exception, string path);
    }
}
