using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NebuLog.Contracts;
using NebuLog.Server.Authentication;
using NebuLog.Server.Identity;

namespace NebuLog.Server.Tests;

/// <summary>
/// Hosts the real application in memory, with settings each test can override.
/// </summary>
/// <remarks>
/// Each factory gets its own SQLite database on a private in-memory connection held open for the
/// factory's lifetime — closing it would discard the schema. Tests therefore never share identity
/// state and can still run in parallel.
/// </remarks>
internal sealed class NebuLogAppFactory : WebApplicationFactory<Program>
{
    /// <summary>The seeded administrator's address.</summary>
    public const string AdminEmail = "admin@nebulog.local";

    /// <summary>The seeded administrator's password, strong enough for the Identity policy.</summary>
    public const string AdminPassword = "Admin!Password#1";

    /// <summary>The password given to every user created by <see cref="SignInAsAsync"/>.</summary>
    public const string UserPassword = "User!Password#1";

    private readonly Dictionary<string, string?> _settings;
    private readonly bool _withThrowingEndpoint;
    private readonly SqliteConnection _connection;

    public NebuLogAppFactory(Dictionary<string, string?>? settings = null, bool withThrowingEndpoint = false)
    {
        _settings = settings ?? new Dictionary<string, string?>(StringComparer.Ordinal);
        _withThrowingEndpoint = withThrowingEndpoint;

        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _settings.TryAdd("NebuLog:Admin:Email", AdminEmail);
        _settings.TryAdd("NebuLog:Admin:Password", AdminPassword);
    }

    /// <summary>The cookie jar shared by clients from this factory.</summary>
    public CookieContainer Cookies { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("Production");
        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(_settings));

        // Inserted at the front so it wraps NebuLog's own startup filter and the peer address is
        // already set by the time forwarded headers are evaluated.
        builder.ConfigureServices(services => services.Insert(
            0,
            ServiceDescriptor.Singleton<IStartupFilter, TestPeerAddressStartupFilter>()));

        // This runs after the application's own ConfigureServices, so the file-backed registration
        // from AddNebuLogServer already exists and has to be replaced, not merely added to.
        builder.ConfigureServices(services =>
        {
            foreach (var descriptor in services
                .Where(d => d.ServiceType == typeof(DbContextOptions<NebuLogDbContext>)
                    || d.ServiceType == typeof(DbContextOptions))
                .ToList())
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<NebuLogDbContext>(options => options.UseSqlite(_connection));
        });

        if (_withThrowingEndpoint)
        {
            // Appended, so it sits inside NebuLog's exception handler.
            builder.ConfigureServices(services =>
                services.AddSingleton<IStartupFilter, ThrowingEndpointStartupFilter>());
        }
    }

    /// <summary>An anonymous client that records and replays cookies.</summary>
    public HttpClient CreateCookieClient()
    {
        var client = new HttpClient(new CookieForwardingHandler(Cookies, Server.CreateHandler()))
        {
            BaseAddress = Server.BaseAddress,
        };

        return client;
    }

    /// <summary>Creates a user in the given role, signs in, and returns a client holding the session.</summary>
    /// <param name="role">One of the names on <see cref="NebuLogRoles"/>.</param>
    /// <param name="email">Optional address; defaults to one derived from the role.</param>
    public async Task<HttpClient> SignInAsAsync(string role, string? email = null)
    {
        // Distinct from the seeded administrator, whose password is AdminPassword.
        email ??= $"{role.ToLowerInvariant()}.test@nebulog.local";
        await EnsureUserAsync(email, role);

        var client = CreateCookieClient();
        using var response = await PostJsonAsync(client, "/api/auth/login", new { email, password = UserPassword });

        if (response.StatusCode != HttpStatusCode.OK)
        {
            client.Dispose();
            throw new InvalidOperationException(
                $"Could not sign in as {email}: {response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        }

        return client;
    }

    /// <summary>Sends a JSON POST carrying the CSRF header the server requires.</summary>
    /// <param name="client">The client to send with.</param>
    /// <param name="path">A relative path.</param>
    /// <param name="body">The body to serialise, or <see langword="null"/> for an empty POST.</param>
    public static async Task<HttpResponseMessage> PostJsonAsync(HttpClient client, string path, object? body = null)
    {
        ArgumentNullException.ThrowIfNull(client);

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(path, UriKind.Relative));
        request.Headers.Add(CsrfHeaderFilter.HeaderName, CsrfHeaderFilter.HeaderValue);

        if (body is not null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        }

        return await client.SendAsync(request);
    }

    /// <summary>A client that authenticates every request with the given API key.</summary>
    /// <param name="apiKey">The clear-text key.</param>
    public HttpClient CreateApiKeyClient(string apiKey)
    {
        var client = CreateCookieClient();
        client.DefaultRequestHeaders.Add(ApiKeyAuthenticationOptions.HeaderName, apiKey);
        return client;
    }

    /// <summary>Issues a fresh key and returns a client that uses it.</summary>
    /// <param name="serviceName">Optional pinned service name.</param>
    public async Task<HttpClient> CreateProducerClientAsync(string? serviceName = null)
    {
        var (_, clearText) = await IssueApiKeyAsync($"producer-{Guid.NewGuid():N}", serviceName);
        return CreateApiKeyClient(clearText);
    }

    /// <summary>Issues an API key directly, bypassing the admin endpoint.</summary>
    /// <param name="name">The key's name.</param>
    /// <param name="serviceName">Optional pinned service name.</param>
    public async Task<(Guid Id, string ClearText)> IssueApiKeyAsync(string name = "test", string? serviceName = null)
    {
        var keys = Services.GetRequiredService<ApiKeyService>();
        var (key, clearText) = await keys.CreateAsync(name, serviceName, "tests", CancellationToken.None);
        return (key.Id, clearText);
    }

    /// <summary>Opens a hub connection authenticated by API key — that is, as a producer.</summary>
    /// <param name="apiKey">The clear-text key.</param>
    public Task<HubConnection> ConnectAsProducerAsync(string apiKey) =>
        ConnectAsync(options => options.Headers[ApiKeyAuthenticationOptions.HeaderName] = apiKey);

    /// <summary>Opens a hub connection carrying the session cookie of an already signed-in client.</summary>
    public Task<HubConnection> ConnectAsUserAsync()
    {
        var header = Cookies.GetCookieHeader(CookieForwardingHandler.CookieScope);
        if (string.IsNullOrEmpty(header))
        {
            throw new InvalidOperationException("Sign in with SignInAsAsync before opening a hub connection.");
        }

        return ConnectAsync(options => options.Headers["Cookie"] = header);
    }

    /// <summary>Signs in with the given role and opens a dashboard hub connection.</summary>
    /// <param name="role">The role to sign in with; defaults to <see cref="NebuLogRoles.Viewer"/>.</param>
    public async Task<HubConnection> ConnectViewerAsync(string role = NebuLogRoles.Viewer)
    {
        using var client = await SignInAsAsync(role);
        return await ConnectAsUserAsync();
    }

    /// <summary>Issues a key and opens a producer hub connection with it.</summary>
    /// <param name="serviceName">Optional pinned service name, reported to the client registry.</param>
    public async Task<HubConnection> ConnectProducerAsync(string? serviceName = null)
    {
        var (_, clearText) = await IssueApiKeyAsync($"producer-{Guid.NewGuid():N}", serviceName);
        return await ConnectAsProducerAsync(clearText);
    }

    /// <summary>Opens an unauthenticated hub connection.</summary>
    public Task<HubConnection> ConnectAnonymouslyAsync() => ConnectAsync(_ => { });

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();
        }
    }

    private async Task EnsureUserAsync(string email, string role)
    {
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<NebuLogUser>>();

        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            user = new NebuLogUser { UserName = email, Email = email, EmailConfirmed = true };
            var created = await users.CreateAsync(user, UserPassword);
            if (!created.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not create {email}: {string.Join("; ", created.Errors.Select(e => e.Description))}");
            }
        }

        if (!await users.IsInRoleAsync(user, role))
        {
            await users.AddToRoleAsync(user, role);
        }
    }

    private async Task<HubConnection> ConnectAsync(Action<HttpConnectionOptions> configure)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(Server.BaseAddress, HubRoutes.Path), options =>
            {
                options.HttpMessageHandlerFactory = _ => Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                configure(options);
            })
            .Build();

        await connection.StartAsync();
        return connection;
    }
}
