using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.SignalR;
using NebuLog.Contracts;
using NebuLog.Server.Diagnostics;
using NebuLog.Server.Ingestion;

namespace NebuLog.Server.Hubs;

/// <summary>
/// The hub producers push logs into and dashboards read from.
/// </summary>
/// <remarks>
/// Roles come from the <c>role</c> query parameter for now. WO-0005 replaces that with the
/// authenticated identity and activates the <see cref="NebuLogPolicies"/> names noted on each member.
/// </remarks>
public sealed class NebuLogHub : Hub<INebuLogHubClient>
{
    private const string RoleQueryKey = "role";
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
        var role = query?[RoleQueryKey].ToString();
        var isProducer = string.Equals(role, ConnectedClientRegistry.ProducerKind, StringComparison.OrdinalIgnoreCase);

        _clients.Add(new ConnectedClientInfo
        {
            ConnectionId = Context.ConnectionId,
            Kind = isProducer ? ConnectedClientRegistry.ProducerKind : ConnectedClientRegistry.ViewerKind,
            ServiceName = Trimmed(query?[ServiceQueryKey].ToString()),
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

    /// <summary>Accepts a batch of log entries from a producer. Policy: <see cref="NebuLogPolicies.Producer"/>.</summary>
    /// <param name="entries">The entries to ingest.</param>
    /// <exception cref="HubException">The ingest queue is full.</exception>
    public void PublishLogs(IReadOnlyList<NebuLogEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var result = _ingestor.TryIngest(entries, IngestSource.SignalR);
        if (result.IsRejected)
        {
            throw new HubException("ingest queue full");
        }
    }

    /// <summary>Declares a live statistic. Policy: <see cref="NebuLogPolicies.Producer"/>.</summary>
    /// <param name="definition">The statistic to declare.</param>
    public async Task DefineStat(StatDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        _stats.Define(definition);
        await Clients.Group(NebuLogGroups.Viewers).StatDefined(definition).ConfigureAwait(false);
    }

    /// <summary>Publishes a new value for a live statistic. Policy: <see cref="NebuLogPolicies.Producer"/>.</summary>
    /// <param name="update">The value to publish.</param>
    public async Task UpdateStat(StatUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);

        _stats.Update(update);
        await Clients.Group(NebuLogGroups.Viewers).StatUpdated(update).ConfigureAwait(false);
    }

    /// <summary>Replays buffered history to a dashboard. Policy: <see cref="NebuLogPolicies.Viewer"/>.</summary>
    /// <param name="query">The history filter.</param>
    /// <param name="cancellationToken">Cancels the stream when the dashboard goes away.</param>
    /// <returns>The matching entries, oldest first.</returns>
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

    /// <summary>Returns every declared statistic. Policy: <see cref="NebuLogPolicies.Viewer"/>.</summary>
    public IReadOnlyList<StatSnapshot> GetStats() => _stats.Snapshot();

    /// <summary>Forwards a command to one producer. Policy: <see cref="NebuLogPolicies.Operator"/>.</summary>
    /// <param name="connectionId">The target producer's connection id.</param>
    /// <param name="command">The command to deliver.</param>
    /// <exception cref="HubException">The target is not a connected producer.</exception>
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
