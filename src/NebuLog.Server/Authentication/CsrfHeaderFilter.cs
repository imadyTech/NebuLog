using Microsoft.AspNetCore.Http;
using NebuLog.Server.Identity;

namespace NebuLog.Server.Authentication;

/// <summary>
/// Requires a custom request header on state-changing requests made with the session cookie.
/// </summary>
/// <remarks>
/// A browser cannot attach a custom header to a cross-site request without first passing a CORS
/// preflight, so requiring one is sufficient to stop cross-site form posts. This sits alongside
/// <c>SameSite=Strict</c> rather than replacing it. Requests authenticated with an API key are
/// exempt: they carry no ambient credential for a third-party site to abuse.
/// </remarks>
internal sealed class CsrfHeaderFilter : IEndpointFilter
{
    /// <summary>The header a browser client must send.</summary>
    public const string HeaderName = "X-NebuLog-Csrf";

    /// <summary>The value the header must carry.</summary>
    public const string HeaderValue = "1";

    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var request = context.HttpContext.Request;

        if (HttpMethods.IsGet(request.Method) ||
            HttpMethods.IsHead(request.Method) ||
            HttpMethods.IsOptions(request.Method))
        {
            return next(context);
        }

        if (!string.IsNullOrEmpty(request.Headers[ApiKeyAuthenticationOptions.HeaderName]))
        {
            return next(context);
        }

        if (string.Equals(request.Headers[HeaderName], HeaderValue, StringComparison.Ordinal))
        {
            return next(context);
        }

        return ValueTask.FromResult<object?>(TypedResults.Problem(
            title: "Missing CSRF header.",
            detail: $"Cookie-authenticated requests that change state must send '{HeaderName}: {HeaderValue}'.",
            statusCode: StatusCodes.Status403Forbidden));
    }
}
