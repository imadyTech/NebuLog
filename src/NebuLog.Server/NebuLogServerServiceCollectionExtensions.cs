using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NebuLog.Server;
using NebuLog.Server.Api;
using NebuLog.Server.Diagnostics;
using NebuLog.Server.Hubs;
using NebuLog.Server.Infrastructure;
using NebuLog.Server.Ingestion;
using IPNetwork = System.Net.IPNetwork;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers everything an application needs to host NebuLog.</summary>
public static class NebuLogServerServiceCollectionExtensions
{
    /// <summary>
    /// Registers the NebuLog pipeline, hub, API, rate limiting, health checks, problem details and
    /// the middleware that must run before the application's own.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">
    /// Configuration root the <c>NebuLog:*</c> sections are bound from.
    /// </param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <remarks>
    /// Forwarded headers, the exception handler, security headers and the rate limiter are inserted
    /// through an <see cref="IStartupFilter"/> rather than a separate <c>UseNebuLog()</c> call, so
    /// that a host only has to call this method and <c>MapNebuLog()</c>, and so that forwarded
    /// headers are guaranteed to run before anything the application adds.
    /// </remarks>
    public static IServiceCollection AddNebuLogServer(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<NebuLogServerOptions>()
            .Bind(configuration.GetSection(NebuLogServerOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<NebuLogServerOptions>, NebuLogServerOptionsValidator>());

        services.AddOptions<NebuLogRateLimitOptions>()
            .Bind(configuration.GetSection(NebuLogRateLimitOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<NebuLogForwardedHeadersOptions>()
            .Bind(configuration.GetSection(NebuLogForwardedHeadersOptions.SectionName));

        services.TryAddSingleton(TimeProvider.System);
        services.AddMetrics();
        services.TryAddSingleton<ServerMetrics>();
        services.TryAddSingleton<LogRingBuffer>();
        services.TryAddSingleton<LiveSummary>();
        services.TryAddSingleton<ConnectedClientRegistry>();
        services.TryAddSingleton<StatRegistry>();
        services.TryAddSingleton<LogIngestor>();
        services.TryAddSingleton<ILogIngestor>(sp => sp.GetRequiredService<LogIngestor>());
        services.TryAddSingleton(sp => new ServerStartTime(
            sp.GetRequiredService<TimeProvider>().GetUtcNow().ToUnixTimeMilliseconds()));
        services.AddHostedService<LogPipelineService>();

        services
            .AddSignalR(hub => hub.MaximumReceiveMessageSize =
                configuration.GetSection(NebuLogServerOptions.SectionName)
                    .GetValue<int?>(nameof(NebuLogServerOptions.MaxHubMessageBytes))
                ?? new NebuLogServerOptions().MaxHubMessageBytes)
            .AddMessagePackProtocol();

        services.AddProblemDetails();
        services.AddExceptionHandler<NebuLogExceptionHandler>();

        services.AddHealthChecks()
            .AddCheck<IngestQueueHealthCheck>(
                IngestQueueHealthCheck.Name,
                failureStatus: HealthStatus.Unhealthy,
                tags: [IngestQueueHealthCheck.ReadyTag]);

        services.AddRateLimiter(ConfigureRateLimiter);

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardedForHeaderName = NebuLogForwardedHeadersOptions.ClientIpHeaderName;
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();

            var configured = configuration
                .GetSection(NebuLogForwardedHeadersOptions.SectionName)[nameof(NebuLogForwardedHeadersOptions.KnownNetworks)];

            foreach (var network in ParseNetworks(configured))
            {
                options.KnownIPNetworks.Add(network);
            }
        });

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupFilter, NebuLogStartupFilter>());

        return services;
    }

    /// <summary>Parses a comma-separated CIDR list; malformed entries are skipped.</summary>
    internal static IEnumerable<IPNetwork> ParseNetworks(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var slash = part.IndexOf('/', StringComparison.Ordinal);
            if (slash <= 0 ||
                !IPAddress.TryParse(part[..slash], out var address) ||
                !int.TryParse(part[(slash + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var prefix))
            {
                continue;
            }

            yield return new IPNetwork(address, prefix);
        }
    }

    private static void ConfigureRateLimiter(RateLimiterOptions limiter)
    {
        limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        limiter.OnRejected = static (context, cancellationToken) =>
        {
            var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var value)
                ? value
                : TimeSpan.FromSeconds(1);

            context.HttpContext.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);

            return ValueTask.CompletedTask;
        };

        limiter.AddPolicy(NebuLogRateLimitOptions.IngestPolicy, static context =>
        {
            var options = context.RequestServices.GetRequiredService<IOptions<NebuLogRateLimitOptions>>().Value;

            return RateLimitPartition.GetTokenBucketLimiter(PartitionKey(context), _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = options.IngestBucketCapacity,
                TokensPerPeriod = options.IngestTokensPerSecond,
                ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            });
        });

        limiter.AddPolicy(NebuLogRateLimitOptions.ApiPolicy, static context =>
        {
            var options = context.RequestServices.GetRequiredService<IOptions<NebuLogRateLimitOptions>>().Value;

            return RateLimitPartition.GetFixedWindowLimiter(PartitionKey(context), _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = options.ApiPermitsPerWindow,
                Window = options.ApiWindow,
                QueueLimit = 0,
            });
        });
    }

    /// <summary>
    /// Partitions by remote address, which the forwarded-headers middleware has already rewritten to
    /// the real client address when the request came through a trusted proxy.
    /// </summary>
    private static string PartitionKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
