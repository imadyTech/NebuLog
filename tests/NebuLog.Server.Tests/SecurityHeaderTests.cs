using NebuLog.Server.Infrastructure;
using Xunit;

namespace NebuLog.Server.Tests;

/// <summary>
/// The content-security-policy, including the configurable extras.
/// </summary>
/// <remarks>
/// This policy is what makes it safe to render log content that arrived from anywhere, so the
/// tests here are mostly about what it still refuses after the extras are added.
/// </remarks>
public sealed class SecurityHeaderTests
{
    [Fact]
    public void TheDefaultPolicyAllowsThisOriginAndNothingElse()
    {
        var policy = SecurityHeadersMiddleware.BuildPolicy(new NebuLogCspOptions());

        Assert.Contains("default-src 'self'", policy, StringComparison.Ordinal);
        Assert.Contains("script-src 'self';", policy, StringComparison.Ordinal);
        Assert.Contains("connect-src 'self';", policy, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'none'", policy, StringComparison.Ordinal);
        Assert.DoesNotContain("unsafe-inline", policy, StringComparison.Ordinal);
        Assert.DoesNotContain("https://", policy, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfiguredSourcesAreAddedToScriptAndConnectOnly()
    {
        // The deployment case this exists for: Cloudflare's beacon, injected at the edge.
        var policy = SecurityHeadersMiddleware.BuildPolicy(new NebuLogCspOptions
        {
            ExtraScriptSrc = "https://static.cloudflareinsights.com",
            ExtraConnectSrc = "https://cloudflareinsights.com",
        });

        Assert.Contains("script-src 'self' https://static.cloudflareinsights.com;", policy, StringComparison.Ordinal);
        Assert.Contains("connect-src 'self' https://cloudflareinsights.com;", policy, StringComparison.Ordinal);

        // Additive only: the extras must not leak into the directives that keep untrusted log
        // content inert, and must not loosen the default.
        Assert.Contains("default-src 'self';", policy, StringComparison.Ordinal);
        Assert.Contains("style-src 'self';", policy, StringComparison.Ordinal);
        Assert.Contains("img-src 'self' data:;", policy, StringComparison.Ordinal);
        Assert.DoesNotContain("unsafe-inline", policy, StringComparison.Ordinal);
        Assert.DoesNotContain("unsafe-eval", policy, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankConfigurationLeavesThePolicyUnchanged(string blank)
    {
        var baseline = SecurityHeadersMiddleware.BuildPolicy(new NebuLogCspOptions());
        var configured = SecurityHeadersMiddleware.BuildPolicy(new NebuLogCspOptions
        {
            ExtraScriptSrc = blank,
            ExtraConnectSrc = blank,
        });

        Assert.Equal(baseline, configured);
    }

    [Fact]
    public async Task EveryResponseCarriesTheSecurityHeaders()
    {
        using var factory = new NebuLogAppFactory(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["NebuLog:Security:Csp:ExtraScriptSrc"] = "https://static.cloudflareinsights.com",
        });
        using var client = factory.CreateCookieClient();

        using var response = await client.GetAsync(new Uri("/", UriKind.Relative), TestContext.Current.CancellationToken);

        var csp = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("https://static.cloudflareinsights.com", csp, StringComparison.Ordinal);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
    }
}
