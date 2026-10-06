using NebuLog.Samples.DemoProducer;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOptions<DemoProducerOptions>()
    .Bind(builder.Configuration.GetSection(DemoProducerOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// The level switch is what the dashboard's set-min-level command drives, across all services.
var levelSwitch = new LogLevelSwitch();
builder.Services.AddSingleton(levelSwitch);

var endpoint = new Uri(builder.Configuration["NebuLog:Endpoint"] ?? "http://localhost:5080");
var apiKey = builder.Configuration["NebuLog:DemoProducer:ApiKey"] ?? string.Empty;

// Three simulated applications, each with its own service.name, so the dashboard's service filter
// has something to filter. See SimulatedService for why each needs its own provider.
var services = builder.Configuration
    .GetSection("DemoProducer:Services")
    .Get<string[]>() ?? ["orders-api", "payments-worker", "inventory-sync"];

builder.Services.AddSingleton<IReadOnlyList<SimulatedService>>(
    _ => [.. services.Select(name => SimulatedService.Create(name, endpoint, apiKey, levelSwitch))]);

builder.Services.AddHostedService<TrafficSimulator>();

var host = builder.Build();
await host.RunAsync();
