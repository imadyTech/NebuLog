using NebuLog.OpenTelemetry;
using NebuLog.Samples.DemoProducer;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOptions<DemoProducerOptions>()
    .Bind(builder.Configuration.GetSection(DemoProducerOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// The level switch is what the dashboard's set-min-level command drives.
var levelSwitch = new LogLevelSwitch();
builder.Services.AddSingleton(levelSwitch);
builder.Logging.AddFilter((_, level) => levelSwitch.IsEnabled(level));

var endpoint = builder.Configuration["NebuLog:Endpoint"] ?? "http://localhost:5080";
var apiKey = builder.Configuration["NebuLog:DemoProducer:ApiKey"] ?? string.Empty;

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(
        serviceName: "nebulog-demo-producer",
        serviceInstanceId: Environment.MachineName))
    .WithLogging(logging => logging
        .AddNebuLogRedaction()
        .AddNebuLogExporter(options =>
        {
            options.Endpoint = new Uri(endpoint);
            options.ApiKey = apiKey;
        }));

builder.Services.AddNebuLogClient();
builder.Services.AddHostedService<TrafficSimulator>();

var host = builder.Build();
await host.RunAsync();
