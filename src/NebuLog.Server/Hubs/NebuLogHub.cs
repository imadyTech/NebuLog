using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using NebuLog.Contracts;
using NebuLog.Server.Diagnostics;
using NebuLog.Server.Authentication;
using NebuLog.Server.Identity;
using NebuLog.Server.Ingestion;

namespace NebuLog.Server.Hubs;

/// <summary>
/// The hub producers push logs into and dashboards read from.
/// </summary>
/// <remarks>
/// Every connection must be authenticated. The client kind comes from its identity, not from the
/// request: a principal in the Producer role (one that authenticated with an API key) is a
/// producer, and any other authenticated user is a dashboard.
/// </remarks>
[Authorize]
public sealed class NebuLogHub : Hub<INebuLogHubClient>
{
    private const string ServiceQueryKey = "service";
    private const string InstanceQueryKey = "instance";

    private readonly ILogIngestor _ingestor;
    private readonly LogRingBuffer _buffer;
    private readonly ConnectedClientRegistry _clients;
    private readonly StatRegistry _stats;
    private readonly LiveSummary _summary;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates the hub.</summary>
    /// <param name="ingestor">Where published logs are handed off.</param>
    /// <param name="buffer">The ring buffer history is replayed from.</param>
    /// <param name="clients">The connected-client registry.</param>
    /// <param name="stats">The live-statistics registry.</param>
    /// <param name="summary">Running totals, used to seed a dashboard on connect.</param>
    /// <param name="timeProvider">Clock used for connection timestamps.</param>
    public NebuLogHub(
        ILogIngestor ingestor,
        LogRingBuffer buffer,
        ConnectedClientRegistry clients,
        StatRegistry stats,
        LiveSummary summary,
        TimeProvider timeProvider)
    {
        _ingestor = ingestor;
        _buffer = buffer;
        _clients = clients;
        _stats = stats;
        _summary = summary;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public override async Task OnConnectedAsync()
    {
        var query = Context.GetHttpContext()?.Request.Query;
        var isProducer = Context.User?.IsInRole(NebuLogRoles.Producer) == true;

        _clients.Add(new ConnectedClientInfo
        {
            ConnectionId = Context.ConnectionId,
            Kind = isProducer ? ConnectedClientRegistry.ProducerKind : ConnectedClientRegistry.ViewerKind,
            ServiceName = Trimmed(Context.User?.FindFirst(ApiKeyAuthenticationOptions.ServiceNameClaim)?.Value)
                ?? Trimmed(query?[ServiceQueryKey].ToString()),
            ServiceInstanceId = Trimmed(query?[InstanceQueryKey].ToString()),
            ConnectedUnixMs = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds(),
        });

        if (!isProducer)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, NebuLogGroups.Viewers).ConfigureAwait(false);
            await Clients.Caller.SummaryUpdated(_summary.Snapshot(_buffer.Count)).ConfigureAwait(false);
        }

        await BroadcastClientsAsync().ConfigureAwait(false);
        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_clients.Remove(Context.ConnectionId))
        {
            await BroadcastClientsAsync().ConfigureAwait(false);
        }

        await base.OnDisconnectedAsync(exception).ConfigureAwait(false);
    }

    /// <summary>Accepts a batch of log entries from a producer.</summary>
    /// <param name="entries">The entries to ingest.</param>
    /// <exception cref="HubException">The ingest queue is full.</exception>
    [Authorize(NebuLogPolicies.Producer)]
    public void PublishLogs(IReadOnlyList<NebuLogEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var result = _ingestor.TryIngest(entries, IngestSource.SignalR);
        if (result.IsRejected)
        {
            throw new HubException("ingest queue full");
        }
    }

    /// <summary>Declares a live statistic.</summary>
    /// <param name="definition">The statistic to declare.</param>
    [Authorize(NebuLogPolicies.Producer)]
    public async Task DefineStat(StatDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        _stats.Define(definition);
        await Clients.Group(NebuLogGroups.Viewers).StatDefined(definition).ConfigureAwait(false);
    }

    /// <summary>Publishes a new value for a live statistic.</summary>
    /// <param name="update">The value to publish.</param>
    [Authorize(NebuLogPolicies.Producer)]
    public async Task UpdateStat(StatUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);

        _stats.Update(update);
        await Clients.Group(NebuLogGroups.Viewers).StatUpdated(update).ConfigureAwait(false);
    }

    /// <summary>Replays buffered history to a dashboard.</summary>
    /// <param name="query">The history filter.</param>
    /// <param name="cancellationToken">Cancels the stream when the dashboard goes away.</param>
    /// <returns>The matching entries, oldest first.</returns>
    [Authorize(NebuLogPolicies.Viewer)]
    public async IAsyncEnumerable<NebuLogEntry> StreamHistory(
        HistoryQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        foreach (var entry in _buffer.Query(query))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return entry;
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>Returns every declared statistic.</summary>
    [Authorize(NebuLogPolicies.Viewer)]
    public IReadOnlyList<StatSnapshot> GetStats() => _stats.Snapshot();

    /// <summary>Forwards a command to one producer.</summary>
    /// <param name="connectionId">The target producer's connection id.</param>
    /// <param name="command">The command to deliver.</param>
    /// <exception cref="HubException">The target is not a connected producer.</exception>
    [Authorize(NebuLogPolicies.Operator)]
    public async Task SendCommand(string connectionId, NebuLogCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_clients.IsProducer(connectionId))
        {
            throw new HubException($"no connected producer with id '{connectionId}'");
        }

        await Clients.Client(connectionId).ReceiveCommand(command).ConfigureAwait(false);
    }

    private Task BroadcastClientsAsync() =>
        Clients.Group(NebuLogGroups.Viewers).ClientsChanged(_clients.Snapshot());

    private static string? Trimmed(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
