using System.Net;

namespace NebuLog.Server.Tests;

/// <summary>Replays and records cookies, which the in-memory test server does not do by itself.</summary>
/// <remarks>
/// Tests run the host as Production, where the session cookie is marked <c>Secure</c>, but the
/// in-memory server speaks plain HTTP. The jar is therefore keyed on an https URI: that is what
/// lets it accept the cookie at all. The flags themselves are asserted directly in
/// <c>AuthenticationTests</c>.
/// </remarks>
internal sealed class CookieForwardingHandler : DelegatingHandler
{
    /// <summary>The URI the cookie jar is keyed on.</summary>
    public static readonly Uri CookieScope = new("https://localhost/");

    private readonly CookieContainer _cookies;

    public CookieForwardingHandler(CookieContainer cookies, HttpMessageHandler inner)
        : base(inner) => _cookies = cookies;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var header = _cookies.GetCookieHeader(CookieScope);
        if (!string.IsNullOrEmpty(header))
        {
            request.Headers.Remove("Cookie");
            request.Headers.Add("Cookie", header);
        }

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            foreach (var cookie in setCookies)
            {
                _cookies.SetCookies(CookieScope, cookie);
            }
        }

        return response;
    }
}
