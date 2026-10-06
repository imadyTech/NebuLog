using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using NebuLog.Contracts;

namespace NebuLog.OpenTelemetry;

/// <summary>
/// The single SignalR connection a process keeps open to its NebuLog server. Log batches,
/// live statistics and inbound commands all travel over it.
/// </summary>
internal sealed class NebuLogConnection : INebuLogTransport, INebuLogStats, INebuLogCommands, IAsyncDisposable
{
    private readonly HubConnection _hub;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _startGate = new(1, 1);
    private readonly CancellationTokenSource _disposing = new();
    private bool _started;
    private bool _disposed;

    public NebuLogConnection(NebuLogExporterOptions options, NebuLogClientIdentity identity, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        if (options.Endpoint is null)
        {
            throw new ArgumentException("NebuLogExporterOptions.Endpoint must be set.", nameof(options));
        }

        _logger = loggerFactory.CreateLogger<NebuLogConnection>();
        _hub = new HubConnectionBuilder()
            .WithUrl(BuildHubUri(options.Endpoint, identity), (HttpConnectionOptions http) =>
            {
                http.Headers["X-Api-Key"] = options.ApiKey;
            })
            .AddMessagePackProtocol()
            .WithAutomaticReconnect(new NebuLogRetryPolicy())
            .Build();

        _hub.On<NebuLogCommand>(HubRoutes.ReceiveCommand, command => CommandReceived?.Invoke(this, command));
    }

    /// <inheritdoc />
    public event EventHandler<NebuLogCommand>? CommandReceived;

    /// <inheritdoc />
    public async Task PublishAsync(IReadOnlyList<NebuLogEntry> entries, CancellationToken cancellationToken)
    {
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        await _hub.InvokeAsync(HubRoutes.PublishLogs, entries, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DefineAsync(StatDefinition definition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        await _hub.InvokeAsync(HubRoutes.DefineStat, definition, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(string id, string value, CancellationToken cancellationToken = default)
    {
        var update = new StatUpdate
        {
            Id = id,
            Value = value,
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };

        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        await _hub.InvokeAsync(HubRoutes.UpdateStat, update, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Starts the connection if it is not running yet. The first caller pays the connection cost;
    /// because the exporter only calls this from its background sender, application startup is never blocked.
    /// </summary>
    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_started && _hub.State == HubConnectionState.Connected)
        {
            return;
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposing.Token);
        await _startGate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            if (_hub.State == HubConnectionState.Disconnected)
            {
                await _hub.StartAsync(linked.Token).ConfigureAwait(false);
                _started = true;
                _logger.LogDebug("NebuLog connection established.");
            }
        }
        finally
        {
            _startGate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _disposing.CancelAsync().ConfigureAwait(false);
        await _hub.DisposeAsync().ConfigureAwait(false);
        _disposing.Dispose();
        _startGate.Dispose();
    }

    private static Uri BuildHubUri(Uri endpoint, NebuLogClientIdentity identity)
    {
        var builder = new UriBuilder(new Uri(endpoint, HubRoutes.Path));
        var query = string.Create(
            CultureInfo.InvariantCulture,
            $"service={Uri.EscapeDataString(identity.ServiceName)}&instance={Uri.EscapeDataString(identity.ServiceInstanceId ?? string.Empty)}");
        builder.Query = query;
        return builder.Uri;
    }
}
