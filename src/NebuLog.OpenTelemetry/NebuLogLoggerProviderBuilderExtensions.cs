using Microsoft.Extensions.DependencyInjection;
using NebuLog.OpenTelemetry;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Microsoft.Extensions.Logging;

/// <summary>
/// Adds the NebuLog exporter and redaction processor to an OpenTelemetry logging pipeline.
/// </summary>
public static class NebuLogLoggerProviderBuilderExtensions
{
    /// <summary>
    /// Adds <see cref="RedactionProcessor"/> to the pipeline.
    /// </summary>
    /// <param name="builder">The OpenTelemetry logging builder.</param>
    /// <param name="patterns">
    /// Case-insensitive attribute-key fragments to treat as sensitive;
    /// <see langword="null"/> uses <see cref="RedactionProcessor.DefaultPatterns"/>.
    /// </param>
    /// <returns>The same builder, for chaining.</returns>
    /// <remarks>
    /// Call this before <see cref="AddNebuLogExporter"/>: OpenTelemetry runs processors in
    /// registration order, so redaction must be registered first to run before the export.
    /// </remarks>
    public static LoggerProviderBuilder AddNebuLogRedaction(
        this LoggerProviderBuilder builder,
        IReadOnlyList<string>? patterns = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddProcessor(
            patterns is null ? new RedactionProcessor() : new RedactionProcessor(patterns));
    }

    /// <summary>
    /// Adds <see cref="NebuLogExporter"/> to the pipeline, streaming log records to a NebuLog server.
    /// </summary>
    /// <param name="builder">The OpenTelemetry logging builder.</param>
    /// <param name="configure">Configures the endpoint, API key, queue and batching settings.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <remarks>
    /// Requires <c>AddNebuLogClient</c> on the service collection; the exporter and the
    /// <see cref="INebuLogStats"/> / <see cref="INebuLogCommands"/> APIs then share one connection.
    /// </remarks>
    public static LoggerProviderBuilder AddNebuLogExporter(
        this LoggerProviderBuilder builder,
        Action<NebuLogExporterOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        return builder.AddProcessor(serviceProvider =>
        {
            var registry = serviceProvider.GetService<NebuLogClientRegistry>()
                ?? throw new InvalidOperationException(
                    "AddNebuLogExporter requires services.AddNebuLogClient() to have been called.");

            registry.AddConfiguration(configure);

            NebuLogExporter? exporter = null;
            exporter = new NebuLogExporter(
                registry.BuildOptions(),
                identity =>
                {
                    registry.SetIdentity(identity);
                    return registry.GetTransport(identity);
                });

            // Lets the registry name this process even when a statistic or a command opens the
            // connection before the first log record is exported.
            registry.SetIdentitySource(() => exporter!.ReadIdentity());

            // A simple processor is correct here: Export() never performs I/O, it only enqueues.
            return new SimpleLogRecordExportProcessor(exporter);
        });
    }
}
