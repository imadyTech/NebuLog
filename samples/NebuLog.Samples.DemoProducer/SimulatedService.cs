using Microsoft.Extensions.DependencyInjection;
using NebuLog.OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

namespace NebuLog.Samples.DemoProducer;

/// <summary>
/// One simulated application: its own OpenTelemetry resource, its own loggers, its own connection.
/// </summary>
/// <remarks>
/// A <c>service.name</c> lives on the OpenTelemetry <see cref="Resource"/>, and a resource belongs
/// to a <c>LoggerProvider</c> — so several service names need several providers. The NebuLog
/// connection is created by the registry that <c>AddNebuLogClient</c> puts in the same container
/// as the exporter, which means one provider per connection as the design stands today. Three
/// simulated services therefore open three connections, and the dashboard shows three producers,
/// which is what makes its service filter worth demonstrating. Sharing one socket across resources
/// would need the registry to key connections by identity; that is noted in WO-0008 §9 rather than
/// built here, because nothing in the product needs it yet.
/// </remarks>
internal sealed class SimulatedService : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    private SimulatedService(string name, ServiceProvider provider, ILoggerFactory loggerFactory)
    {
        Name = name;
        _provider = provider;

        Orders = loggerFactory.CreateLogger($"{name}.Orders");
        Shipping = loggerFactory.CreateLogger($"{name}.Shipping");
        Billing = loggerFactory.CreateLogger($"{name}.Billing");
    }

    /// <summary>The <c>service.name</c> this simulated service reports.</summary>
    public string Name { get; }

    /// <summary>Statistics API, available on every simulated service's connection.</summary>
    public INebuLogStats Stats => _provider.GetRequiredService<INebuLogStats>();

    /// <summary>Command API, available on every simulated service's connection.</summary>
    public INebuLogCommands Commands => _provider.GetRequiredService<INebuLogCommands>();

    public ILogger Orders { get; }

    public ILogger Shipping { get; }

    public ILogger Billing { get; }

    /// <summary>Builds a simulated service with its own resource, exporter and connection.</summary>
    /// <param name="name">The <c>service.name</c> to report.</param>
    /// <param name="endpoint">The NebuLog server's root address.</param>
    /// <param name="apiKey">The producer API key.</param>
    /// <param name="levelSwitch">Shared minimum-level switch, driven by the dashboard's command.</param>
    public static SimulatedService Create(
        string name,
        Uri endpoint,
        string apiKey,
        LogLevelSwitch levelSwitch)
    {
        var services = new ServiceCollection();
        services.AddLogging(logging => logging
            .SetMinimumLevel(LogLevel.Trace)
            .AddFilter((_, level) => levelSwitch.IsEnabled(level)));

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                serviceName: name,
                serviceInstanceId: $"{Environment.MachineName}-{name}"))
            .WithLogging(logging => logging
                .AddNebuLogRedaction()
                .AddNebuLogExporter(options =>
                {
                    options.Endpoint = endpoint;
                    options.ApiKey = apiKey;
                }));

        services.AddNebuLogClient();

        var provider = services.BuildServiceProvider();
        return new SimulatedService(name, provider, provider.GetRequiredService<ILoggerFactory>());
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _provider.DisposeAsync();
}
