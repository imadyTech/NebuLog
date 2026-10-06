using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace NebuLog.Server.Infrastructure;

/// <summary>Adds the baseline security response headers to every response.</summary>
/// <remarks>
/// The dashboard is served same-origin and loads no third-party assets, so the policy can stay
/// tight. <c>/scalar</c> is the one exception: its bundle is loaded from a CDN and it applies
/// inline styles, so that path gets its own slightly wider policy rather than relaxing the default.
/// A deployment may add further sources through <see cref="NebuLogCspOptions"/>.
/// </remarks>
internal sealed class SecurityHeadersMiddleware
{
    private const string BaseScriptSrc = "'self'";
    private const string BaseConnectSrc = "'self'";

    // Scalar renders from a CDN bundle and injects inline styles and a configuration script.
    private const string ScalarContentSecurityPolicy =
        "default-src 'self'; connect-src 'self'; img-src 'self' data: https:; " +
        "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
        "script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
        "font-src 'self' data: https://cdn.jsdelivr.net; frame-ancestors 'none'; base-uri 'self'";

    private readonly RequestDelegate _next;
    private readonly IOptionsMonitor<NebuLogCspOptions> _options;

    // The policy is a pure function of the options, so it is built once and rebuilt only when they
    // change — rather than reassembled on every response.
    private string _policy;
    private NebuLogCspOptions? _builtFrom;

    public SecurityHeadersMiddleware(RequestDelegate next, IOptionsMonitor<NebuLogCspOptions> options)
    {
        _next = next;
        _options = options;
        _builtFrom = options.CurrentValue;
        _policy = BuildPolicy(_builtFrom);
    }

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var isScalar = context.Request.Path.StartsWithSegments("/scalar", StringComparison.OrdinalIgnoreCase);
        var headers = context.Response.Headers;

        headers["Content-Security-Policy"] = isScalar ? ScalarContentSecurityPolicy : CurrentPolicy();
        headers["X-Content-Type-Options"] = "nosniff";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = "accelerometer=(), camera=(), geolocation=(), microphone=(), payment=(), usb=()";

        return _next(context);
    }

    /// <summary>Builds the default policy, adding any configured sources.</summary>
    /// <param name="options">The extra sources to admit.</param>
    /// <returns>The complete policy string.</returns>
    internal static string BuildPolicy(NebuLogCspOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var scriptSrc = Append(BaseScriptSrc, options.ExtraScriptSrc);
        var connectSrc = Append(BaseConnectSrc, options.ExtraConnectSrc);

        return $"default-src 'self'; connect-src {connectSrc}; img-src 'self' data:; style-src 'self'; " +
            $"script-src {scriptSrc}; frame-ancestors 'none'; base-uri 'self'";
    }

    private static string Append(string baseSource, string? extra) =>
        string.IsNullOrWhiteSpace(extra) ? baseSource : $"{baseSource} {extra.Trim()}";

    private string CurrentPolicy()
    {
        var current = _options.CurrentValue;
        if (!ReferenceEquals(current, _builtFrom))
        {
            _policy = BuildPolicy(current);
            _builtFrom = current;
        }

        return _policy;
    }
}
