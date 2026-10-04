using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace NebuLog.Server.Infrastructure;

/// <summary>
/// Inserts NebuLog's middleware ahead of whatever the hosting application configures, so a host
/// only needs <c>AddNebuLogServer()</c> and <c>MapNebuLog()</c>.
/// </summary>
/// <remarks>
/// Order matters and is fixed here: forwarded headers must run first so that every later component —
/// rate limiting, logging, the client address a health probe sees — observes the real client
/// address; the exception handler wraps everything after it; security headers are set before any
/// endpoint can start writing a response. The rate limiter is <em>not</em> added here — it has to
/// run after routing so that each endpoint's policy metadata is visible, so <c>MapNebuLog()</c>
/// adds it instead.
/// </remarks>
internal sealed class NebuLogStartupFilter : IStartupFilter
{
    private readonly IOptions<NebuLogForwardedHeadersOptions> _forwardedHeaders;
    private readonly IHostEnvironment _environment;

    public NebuLogStartupFilter(
        IOptions<NebuLogForwardedHeadersOptions> forwardedHeaders,
        IHostEnvironment environment)
    {
        _forwardedHeaders = forwardedHeaders;
        _environment = environment;
    }

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        ArgumentNullException.ThrowIfNull(next);

        return app =>
        {
            // Only enable forwarded headers when a trusted network is configured. Trusting them
            // unconditionally would let any caller spoof its own address.
            if (!string.IsNullOrWhiteSpace(_forwardedHeaders.Value.KnownNetworks))
            {
                app.UseForwardedHeaders();
            }

            app.UseExceptionHandler();

            if (!_environment.IsDevelopment())
            {
                app.UseHsts();
            }

            app.UseMiddleware<SecurityHeadersMiddleware>();

            next(app);
        };
    }
}
