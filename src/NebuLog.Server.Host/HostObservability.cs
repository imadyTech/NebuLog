using NebuLog.Server.Diagnostics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace NebuLog.Server.Host;

/// <summary>Wires the host's own OpenTelemetry metrics and tracing.</summary>
internal static class HostObservability
{
    private const string OtlpEndpointVariable = "OTEL_EXPORTER_OTLP_ENDPOINT";

    /// <summary>
    /// Collects metrics and traces for the server. An OTLP exporter is added only when
    /// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set; with no collector deployed the default is to
    /// collect in-process and export nothing.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static WebApplicationBuilder AddHostObservability(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var exportEnabled = !string.IsNullOrWhiteSpace(builder.Configuration[OtlpEndpointVariable]);

        var otel = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                serviceName: "nebulog-server",
                serviceVersion: typeof(HostObservability).Assembly.GetName().Version?.ToString()))
            .WithMetrics(metrics =>
            {
                metrics.AddMeter(ServerMetrics.Name)
                    .AddAspNetCoreInstrumentation()
                    .AddRuntimeInstrumentation();

                if (exportEnabled)
                {
                    metrics.AddOtlpExporter();
                }
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(ServerMetrics.Name)
                    .AddAspNetCoreInstrumentation();

                if (exportEnabled)
                {
                    tracing.AddOtlpExporter();
                }
            });

        _ = otel;
        return builder;
    }
}
