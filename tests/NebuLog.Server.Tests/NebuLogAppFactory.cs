using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NebuLog.Contracts;

namespace NebuLog.Server.Tests;

/// <summary>Hosts the real application in memory, with settings each test can override.</summary>
internal sealed class NebuLogAppFactory : WebApplicationFactory<Program>
{
    private readonly Dictionary<string, string?> _settings;

    private readonly bool _withThrowingEndpoint;

    public NebuLogAppFactory(Dictionary<string, string?>? settings = null, bool withThrowingEndpoint = false)
    {
        _settings = settings ?? new Dictionary<string, string?>(StringComparer.Ordinal);
        _withThrowingEndpoint = withThrowingEndpoint;
    }

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

        if (_withThrowingEndpoint)
        {
            // Appended, so it sits inside NebuLog's exception handler.
            builder.ConfigureServices(services =>
                services.AddSingleton<IStartupFilter, ThrowingEndpointStartupFilter>());
        }
    }

    /// <summary>Opens a hub connection over the in-memory test server.</summary>
    /// <param name="role">The <c>role</c> query value: <c>producer</c> or <c>viewer</c>.</param>
    /// <param name="service">Optional <c>service</c> query value.</param>
    public async Task<HubConnection> ConnectAsync(string role, string? service = null)
    {
        var query = $"?role={role}" + (service is null ? string.Empty : $"&service={service}");
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(Server.BaseAddress, HubRoutes.Path + query), options =>
            {
                options.HttpMessageHandlerFactory = _ => Server.CreateHandler();
                options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
            })
            .Build();

        await connection.StartAsync();
        return connection;
    }
}
