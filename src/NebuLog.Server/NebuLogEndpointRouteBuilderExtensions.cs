using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NebuLog.Contracts;
using NebuLog.Server.Api;
using NebuLog.Server.Authentication;
using NebuLog.Server.Diagnostics;
using NebuLog.Server.Hubs;
using NebuLog.Server.Infrastructure;
using NebuLog.Server.Ingestion;
using NebuLog.Server.Otlp;

namespace Microsoft.AspNetCore.Builder;

/// <summary>Maps the NebuLog hub, REST API, health checks and dashboard fallback.</summary>
public static class NebuLogEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Paths owned by the server. A request under one of these that matches no endpoint returns
    /// 404 rather than falling back to the dashboard's <c>index.html</c>.
    /// </summary>
    private static readonly string[] ReservedPrefixes =
        ["/api", "/hubs", "/v1", "/health", "/openapi", "/scalar"];

    /// <summary>Maps every NebuLog endpoint onto the application.</summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapNebuLog(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        // These must sit after routing (so endpoint metadata is visible) and before the endpoint
        // middleware that acts on it. Registering them here puts them in exactly that window,
        // which a startup filter — which runs before routing — cannot do.
        if (endpoints is IApplicationBuilder app)
        {
            app.UseCors();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseRateLimiter();
        }

        endpoints.MapHub<NebuLogHub>(HubRoutes.Path);
        endpoints.MapNebuLogApi();
        endpoints.MapNebuLogOtlp();
        endpoints.MapNebuLogAuth();
        endpoints.MapNebuLogApiKeys();

        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = static _ => false,
            ResponseWriter = HealthCheckResponse.WriteAsync,
        });

        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = static registration => registration.Tags.Contains(IngestQueueHealthCheck.ReadyTag),
            ResponseWriter = HealthCheckResponse.WriteAsync,
        });

        var metrics = endpoints.ServiceProvider.GetRequiredService<ServerMetrics>();
        var buffer = endpoints.ServiceProvider.GetRequiredService<LogRingBuffer>();
        var clients = endpoints.ServiceProvider.GetRequiredService<ConnectedClientRegistry>();
        metrics.RegisterGauges(() => buffer.Count, () => clients.ViewerCount, () => clients.ProducerCount);

        return endpoints;
    }

    /// <summary>
    /// Serves the dashboard's client-side routes from <c>index.html</c>, except under the paths the
    /// server owns.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <param name="filePath">The SPA entry document.</param>
    /// <returns>The same application, for chaining.</returns>
    public static WebApplication MapNebuLogDashboardFallback(this WebApplication app, string filePath = "index.html")
    {
        ArgumentNullException.ThrowIfNull(app);

        // GET and HEAD only: the dashboard's client-side routes are navigations. Answering a POST
        // to an unknown path with index.html would make endpoints that do not exist look like they
        // succeeded.
        app.MapFallback(async context =>
        {
            if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            foreach (var prefix in ReservedPrefixes)
            {
                if (context.Request.Path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }
            }

            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.SendFileAsync(
                app.Environment.WebRootFileProvider.GetFileInfo(filePath),
                context.RequestAborted).ConfigureAwait(false);
        });

        return app;
    }
}
