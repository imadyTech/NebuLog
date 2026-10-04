using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using NebuLog.Server.Authentication;
using NebuLog.Server.Identity;
using Xunit;

namespace NebuLog.Server.Tests;

public sealed class AuthenticationTests
{
    private static Uri Relative(string path) => new(path, UriKind.Relative);

    [Fact]
    public async Task AnonymousCallersCannotReadLogs()
    {
        await using var factory = new NebuLogAppFactory();
        using var client = factory.CreateCookieClient();

        using var response = await client.GetAsync(Relative("/api/logs"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnauthenticatedApiCallsAreNotRedirectedToALoginPage()
    {
        await using var factory = new NebuLogAppFactory();
        using var client = factory.CreateCookieClient();

        using var response = await client.GetAsync(Relative("/api/summary"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task AViewerCanReadLogsButCannotManageKeys()
    {
        await using var factory = new NebuLogAppFactory();
        using var client = await factory.SignInAsAsync(NebuLogRoles.Viewer);
        var token = TestContext.Current.CancellationToken;

        using var logs = await client.GetAsync(Relative("/api/logs"), token);
        Assert.Equal(HttpStatusCode.OK, logs.StatusCode);

        using var keys = await client.GetAsync(Relative("/api/keys"), token);
        Assert.Equal(HttpStatusCode.Forbidden, keys.StatusCode);
    }

    [Fact]
    public async Task AnAdminCanManageKeys()
    {
        await using var factory = new NebuLogAppFactory();
        using var client = await factory.SignInAsAsync(NebuLogRoles.Admin);

        using var response = await client.GetAsync(Relative("/api/keys"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TheSessionCookieIsHttpOnlySecureAndStrict()
    {
        await using var factory = new NebuLogAppFactory();
        using var client = factory.CreateCookieClient();

        using var response = await NebuLogAppFactory.PostJsonAsync(
            client,
            "/api/auth/login",
            new { email = NebuLogAppFactory.AdminEmail, password = NebuLogAppFactory.AdminPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var setCookie = Assert.Single(
            response.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("nebulog.auth=", StringComparison.Ordinal));

        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StateChangingRequestsWithoutTheCsrfHeaderAreRefused()
    {
        await using var factory = new NebuLogAppFactory();
        using var client = factory.CreateCookieClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, Relative("/api/auth/login"))
        {
            Content = JsonContent.Create(new
            {
                email = NebuLogAppFactory.AdminEmail,
                password = NebuLogAppFactory.AdminPassword,
            }),
        };

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TheCsrfHeaderIsNotRequiredOfApiKeyCallers()
    {
        await using var factory = new NebuLogAppFactory();
        var (_, apiKey) = await factory.IssueApiKeyAsync();
        using var client = factory.CreateApiKeyClient(apiKey);

        using var request = new HttpRequestMessage(HttpMethod.Post, Relative("/v1/logs"))
        {
            Content = new ByteArrayContent([]),
        };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-protobuf");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TheDemoAccountCanSignInWhenEnabled()
    {
        await using var factory = new NebuLogAppFactory();
        using var client = factory.CreateCookieClient();

        using var response = await NebuLogAppFactory.PostJsonAsync(client, "/api/auth/demo");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var user = await response.Content.ReadFromJsonAsync<CurrentUserDto>(TestContext.Current.CancellationToken);
        Assert.Equal(NebuLogDemoOptions.DemoEmail, user!.Email);
        Assert.Equal([NebuLogRoles.Viewer], user.Roles);
    }

    [Fact]
    public async Task TheDemoEndpointIsAbsentWhenDisabled()
    {
        await using var factory = new NebuLogAppFactory(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["NebuLog:Demo:Enabled"] = "false",
        });
        using var client = factory.CreateCookieClient();

        using var response = await NebuLogAppFactory.PostJsonAsync(client, "/api/auth/demo");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TheDemoUserCannotSendCommands()
    {
        await using var factory = new NebuLogAppFactory();
        using var client = factory.CreateCookieClient();
        using (await NebuLogAppFactory.PostJsonAsync(client, "/api/auth/demo"))
        {
        }

        using var response = await client.GetAsync(Relative("/api/keys"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RepeatedBadPasswordsLockTheAccount()
    {
        await using var factory = new NebuLogAppFactory();
        using var client = factory.CreateCookieClient();

        HttpStatusCode? last = null;
        for (var attempt = 0; attempt < 6; attempt++)
        {
            using var response = await NebuLogAppFactory.PostJsonAsync(
                client,
                "/api/auth/login",
                new { email = NebuLogAppFactory.AdminEmail, password = "definitely-not-the-password" });
            last = response.StatusCode;
        }

        Assert.Equal(HttpStatusCode.Locked, last);

        // Even the correct password is refused while the lockout holds.
        using var afterLockout = await NebuLogAppFactory.PostJsonAsync(
            client,
            "/api/auth/login",
            new { email = NebuLogAppFactory.AdminEmail, password = NebuLogAppFactory.AdminPassword });

        Assert.Equal(HttpStatusCode.Locked, afterLockout.StatusCode);
    }

    [Fact]
    public async Task MeReportsTheSignedInUserAndLogoutEndsTheSession()
    {
        await using var factory = new NebuLogAppFactory();
        using var client = await factory.SignInAsAsync(NebuLogRoles.Operator);
        var token = TestContext.Current.CancellationToken;

        var me = await client.GetFromJsonAsync<CurrentUserDto>(Relative("/api/auth/me"), token);
        Assert.Equal("operator.test@nebulog.local", me!.Email);
        Assert.Contains(NebuLogRoles.Operator, me.Roles);

        using (var logout = await NebuLogAppFactory.PostJsonAsync(client, "/api/auth/logout"))
        {
            Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        }

        using var afterLogout = await client.GetAsync(Relative("/api/auth/me"), token);
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [Fact]
    public async Task ThereIsNoRegistrationEndpoint()
    {
        await using var factory = new NebuLogAppFactory();
        using var client = factory.CreateCookieClient();

        foreach (var path in new[] { "/api/auth/register", "/register", "/api/auth/forgotPassword" })
        {
            using var response = await NebuLogAppFactory.PostJsonAsync(
                client, path, new { email = "intruder@example.test", password = "Intruder!Password#1" });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    [Fact]
    public async Task TheSeededAdministratorIsNotOverwrittenOnRestart()
    {
        await using var factory = new NebuLogAppFactory();

        // Running the initializer twice must leave the existing account untouched.
        var initializer = factory.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>()
            .First(service => service.GetType().Name == "DatabaseInitializer");
        await initializer.StartAsync(TestContext.Current.CancellationToken);

        using var client = factory.CreateCookieClient();
        using var response = await NebuLogAppFactory.PostJsonAsync(
            client,
            "/api/auth/login",
            new { email = NebuLogAppFactory.AdminEmail, password = NebuLogAppFactory.AdminPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    [InlineData("/openapi/v1.json")]
    [InlineData("/scalar/v1")]
    [InlineData("/")]
    public async Task ThePublicSurfaceStaysReachableWithoutSigningIn(string path)
    {
        await using var factory = new NebuLogAppFactory();
        using var client = factory.CreateCookieClient();

        using var response = await client.GetAsync(Relative(path), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/logs")]
    [InlineData("/api/summary")]
    [InlineData("/api/clients")]
    [InlineData("/api/custom-stats")]
    [InlineData("/api/info")]
    [InlineData("/api/keys")]
    [InlineData("/api/auth/me")]
    public async Task NoDataEndpointIsReachableAnonymously(string path)
    {
        await using var factory = new NebuLogAppFactory();
        using var client = factory.CreateCookieClient();

        using var response = await client.GetAsync(Relative(path), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
