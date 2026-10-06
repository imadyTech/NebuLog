using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NebuLog.Contracts;
using NebuLog.Server.Diagnostics;
using NebuLog.Server.Hubs;

namespace NebuLog.Server.Ingestion;

/// <summary>
/// Drains the ingest queue: assigns ids, buffers entries, folds them into the summary and pushes
/// one batched broadcast per interval to the <c>viewers</c> group.
/// </summary>
internal sealed partial class LogPipelineService : BackgroundService
{
    private readonly LogIngestor _ingestor;
    private readonly LogRingBuffer _buffer;
    private readonly LiveSummary _summary;
    private readonly ServerMetrics _metrics;
    private readonly IHubContext<NebuLogHub, INebuLogHubClient> _hub;
    private readonly ILogger<LogPipelineService> _logger;
    private readonly NebuLogServerOptions _options;
    private readonly TimeProvider _timeProvider;
    private long _lastId;

    public LogPipelineService(
        ILogIngestor ingestor,
        LogRingBuffer buffer,
        LiveSummary summary,
        ServerMetrics metrics,
        IHubContext<NebuLogHub, INebuLogHubClient> hub,
        IOptions<NebuLogServerOptions> options,
        TimeProvider timeProvider,
        ILogger<LogPipelineService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _ingestor = (LogIngestor)ingestor;
        _buffer = buffer;
        _summary = summary;
        _metrics = metrics;
        _hub = hub;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reader = _ingestor.Reader;
        var batch = new List<NebuLogEntry>(_options.MaxBroadcastBatch);
        using var summaryTimer = new PeriodicTimer(TimeSpan.FromSeconds(1), _timeProvider);
        var summaryPump = PushSummaryAsync(summaryTimer, stoppingToken);

        try
        {
            while (!stoppingToken.IsCancellationRequested && await reader.WaitToReadAsync(stoppingToken).ConfigureAwait(false))
            {
                // Let the batch fill for one interval, then flush whatever arrived.
                await Task.Delay(_options.BroadcastInterval, _timeProvider, stoppingToken).ConfigureAwait(false);

                while (batch.Count < _options.MaxBroadcastBatch && reader.TryRead(out var entry))
                {
                    batch.Add(entry with { Id = Interlocked.Increment(ref _lastId) });
                }

                if (batch.Count == 0)
                {
                    continue;
                }

                await FlushAsync(batch, stoppingToken).ConfigureAwait(false);
                batch.Clear();
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        finally
        {
            await summaryPump.ConfigureAwait(false);
        }
    }

    private async Task FlushAsync(List<NebuLogEntry> batch, CancellationToken cancellationToken)
    {
        using var activity = ServerMetrics.ActivitySource.StartActivity("nebulog.ingest.batch", ActivityKind.Internal);
        activity?.SetTag("nebulog.batch.size", batch.Count);
        activity?.SetTag("nebulog.batch.first_id", batch[0].Id);

        var entries = batch.ToArray();
        _buffer.Append(entries);
        _summary.Record(entries);
        _metrics.RecordBroadcast(entries.Length);

        try
        {
            await _hub.Clients.Group(NebuLogGroups.Viewers).ReceiveLogs(entries).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A dashboard failing to receive must not stop the pipeline; the entries are already buffered.
            BroadcastFailed(exception, entries.Length);
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task PushSummaryAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                await _hub.Clients.Group(NebuLogGroups.Viewers)
                    .SummaryUpdated(_summary.Snapshot(_buffer.Count))
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        catch (Exception exception)
        {
            SummaryPushFailed(exception);
        }
    }

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Warning,
        Message = "Failed to broadcast {Count} log entries to dashboards.")]
    private partial void BroadcastFailed(Exception exception, int count);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Warning,
        Message = "Failed to push the activity summary to dashboards.")]
    private partial void SummaryPushFailed(Exception exception);
}
