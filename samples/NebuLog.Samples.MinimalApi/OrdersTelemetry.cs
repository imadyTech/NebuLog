using NebuLog.Contracts;
using NebuLog.OpenTelemetry;

namespace NebuLog.Samples.MinimalApi;

/// <summary>
/// Publishes a requests-per-second statistic and handles commands sent from the dashboard.
/// </summary>
/// <remarks>
/// Both ride the same SignalR connection the exporter uses, which is why this is only registered
/// when the <c>Exporter</c> transport is enabled — OTLP is one-way.
/// </remarks>
internal sealed partial class OrdersTelemetry : BackgroundService
{
    private readonly INebuLogStats _stats;
    private readonly INebuLogCommands _commands;
    private readonly RequestCounter _counter;
    private readonly LogLevelSwitch _levelSwitch;
    private readonly ILogger<OrdersTelemetry> _logger;

    public OrdersTelemetry(
        INebuLogStats stats,
        INebuLogCommands commands,
        RequestCounter counter,
        LogLevelSwitch levelSwitch,
        ILogger<OrdersTelemetry> logger)
    {
        _stats = stats;
        _commands = commands;
        _counter = counter;
        _levelSwitch = levelSwitch;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _commands.CommandReceived += OnCommand;

        try
        {
            await _stats.DefineAsync(
                new StatDefinition { Id = "orders.rps", Title = "Requests/s", Color = "teal" },
                stoppingToken).ConfigureAwait(false);

            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                await _stats.UpdateAsync("orders.rps", _counter.Drain().ToString(), stoppingToken)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        catch (Exception exception)
        {
            StatsUnavailable(exception);
        }
        finally
        {
            _commands.CommandReceived -= OnCommand;
        }
    }

    private void OnCommand(object? sender, NebuLogCommand command)
    {
        switch (command.Name)
        {
            case "ping":
                Pong(command.IssuedBy);
                break;

            case "set-min-level":
                if (command.Arguments.TryGetValue("level", out var raw) && int.TryParse(raw, out var severity))
                {
                    var level = severity switch
                    {
                        >= Severity.Fatal => LogLevel.Critical,
                        >= Severity.Error => LogLevel.Error,
                        >= Severity.Warn => LogLevel.Warning,
                        >= Severity.Info => LogLevel.Information,
                        >= Severity.Debug => LogLevel.Debug,
                        _ => LogLevel.Trace,
                    };

                    _levelSwitch.MinimumLevel = level;
                    MinimumLevelChanged(level, command.IssuedBy);
                }

                break;

            default:
                UnknownCommand(command.Name);
                break;
        }
    }

    [LoggerMessage(EventId = 100, Level = LogLevel.Information, Message = "pong (requested by {IssuedBy})")]
    private partial void Pong(string issuedBy);

    [LoggerMessage(EventId = 101, Level = LogLevel.Information,
        Message = "Minimum level set to {Level} by {IssuedBy}")]
    private partial void MinimumLevelChanged(LogLevel level, string issuedBy);

    [LoggerMessage(EventId = 102, Level = LogLevel.Warning, Message = "Ignored unknown command {Name}")]
    private partial void UnknownCommand(string name);

    [LoggerMessage(EventId = 103, Level = LogLevel.Warning, Message = "Statistics could not be published")]
    private partial void StatsUnavailable(Exception exception);
}
