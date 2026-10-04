using Microsoft.AspNetCore.Http;

namespace NebuLog.Server.Infrastructure;

/// <summary>Adds the baseline security response headers to every response.</summary>
/// <remarks>
/// The dashboard is served same-origin and loads no third-party assets, so the policy can stay
/// tight. <c>/scalar</c> is the one exception: its bundle is loaded from a CDN and it applies
/// inline styles, so that path gets its own slightly wider policy rather than relaxing the default.
/// </remarks>
internal sealed class SecurityHeadersMiddleware
{
    private const string DefaultContentSecurityPolicy =
        "default-src 'self'; connect-src 'self'; img-src 'self' data:; style-src 'self'; " +
        "script-src 'self'; frame-ancestors 'none'; base-uri 'self'";

    // Scalar renders from a CDN bundle and injects inline styles and a configuration script.
    private const string ScalarContentSecurityPolicy =
        "default-src 'self'; connect-src 'self'; img-src 'self' data: https:; " +
        "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
        "script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
        "font-src 'self' data: https://cdn.jsdelivr.net; frame-ancestors 'none'; base-uri 'self'";

    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var isScalar = context.Request.Path.StartsWithSegments("/scalar", StringComparison.OrdinalIgnoreCase);
        var headers = context.Response.Headers;

        headers["Content-Security-Policy"] = isScalar ? ScalarContentSecurityPolicy : DefaultContentSecurityPolicy;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = "accelerometer=(), camera=(), geolocation=(), microphone=(), payment=(), usb=()";

        return _next(context);
    }
}
