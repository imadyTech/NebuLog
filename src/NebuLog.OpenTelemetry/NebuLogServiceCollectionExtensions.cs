using Microsoft.Extensions.DependencyInjection.Extensions;
using NebuLog.OpenTelemetry;
using OpenTelemetry.Logs;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers the NebuLog client services.</summary>
public static class NebuLogServiceCollectionExtensions
{
    /// <summary>
    /// Registers the shared NebuLog connection together with <see cref="INebuLogStats"/> and
    /// <see cref="INebuLogCommands"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">
    /// Optionally configures the connection. Settings supplied to <c>AddNebuLogExporter</c> are applied
    /// on top of these, so a process that only uses the exporter needs no arguments here.
    /// </param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <remarks>
    /// The connection is opened lazily on first use, on a background thread, so application startup
    /// is never blocked by an unreachable NebuLog server.
    /// </remarks>
    public static IServiceCollection AddNebuLogClient(
        this IServiceCollection services,
        Action<NebuLogExporterOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = services.AddOptions<NebuLogExporterOptions>();
        if (configure is not null)
        {
            // Only fail fast when this call owns the settings. When the endpoint comes from
            // AddNebuLogExporter instead, the options are incomplete here on purpose and the merged
            // result is validated instead, as the connection opens.
            options.Configure(configure).ValidateDataAnnotations().ValidateOnStart();
        }

        // NebuLog shows people their logs, and OpenTelemetry does not render the message by
        // default — without this the dashboard displays "Order {OrderId} shipped" rather than
        // "Order 42 shipped". Set here rather than in the exporter because only the service
        // collection can reach OpenTelemetryLoggerOptions. An application that wants the raw
        // template can turn it back off after calling this method.
        services.Configure<OpenTelemetryLoggerOptions>(options => options.IncludeFormattedMessage = true);

        services.TryAddSingleton<NebuLogClientRegistry>();
        services.TryAddSingleton<INebuLogStats>(sp => sp.GetRequiredService<NebuLogClientRegistry>().GetConnection());
        services.TryAddSingleton<INebuLogCommands>(sp => sp.GetRequiredService<NebuLogClientRegistry>().GetConnection());
        services.TryAddSingleton<INebuLogTransport>(sp => sp.GetRequiredService<NebuLogClientRegistry>().GetConnection());

        return services;
    }
}
