using System.Diagnostics;
using Microsoft.Extensions.Options;
using NebuLog.Contracts;

namespace NebuLog.Samples.DemoProducer;

/// <summary>
/// Produces plausible traffic for the public demo: three services, a realistic mix of severities,
/// occasional exceptions with stack traces, nested activities and a few live statistics.
/// </summary>
internal sealed partial class TrafficSimulator : BackgroundService
{
    private static readonly ActivitySource Activity = new("NebuLog.DemoProducer");

    /// <summary>How often the statistic definitions are re-sent. See the call site for why.</summary>
    private static readonly TimeSpan RedeclareInterval = TimeSpan.FromSeconds(30);

    private static readonly string[] Products =
        ["flat white", "long black", "cortado", "filter roast", "cold brew"];

    private static readonly string[] Regions = ["auckland", "wellington", "christchurch"];

    private readonly IReadOnlyList<SimulatedService> _services;
    private readonly DemoProducerOptions _options;
    private readonly LogLevelSwitch _levelSwitch;
    private readonly ILogger<TrafficSimulator> _logger;

    private int _queueDepth;
    private int _ordersPlaced;
    private DateTimeOffset _burstUntil = DateTimeOffset.MinValue;

    public TrafficSimulator(
        IReadOnlyList<SimulatedService> services,
        IOptions<DemoProducerOptions> options,
        LogLevelSwitch levelSwitch,
        ILogger<TrafficSimulator> logger)
    {
        _services = services;
        _options = options.Value;
        _levelSwitch = levelSwitch;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        foreach (var service in _services)
        {
            service.Commands.CommandReceived += OnCommand;
        }

        try
        {
            await DeclareStatsAsync(stoppingToken).ConfigureAwait(false);

            var random = new Random(Environment.TickCount);
            var statsTimer = Stopwatch.StartNew();
            var declareTimer = Stopwatch.StartNew();

            while (!stoppingToken.IsCancellationRequested)
            {
                var bursting = DateTimeOffset.UtcNow < _burstUntil;
                var perSecond = bursting
                    ? _options.BurstRatePerSecond
                    : random.Next(_options.MinRatePerSecond, _options.MaxRatePerSecond + 1);

                // One slice of a second's worth of traffic, so the rate stays smooth rather than
                // arriving as one spike per second.
                var slice = Math.Max(1, perSecond / 10);
                for (var i = 0; i < slice && !stoppingToken.IsCancellationRequested; i++)
                {
                    EmitOne(random);
                }

                // Statistic definitions live in the server's memory, so they are lost whenever the
                // server restarts — and the very first declaration can race a server that is not
                // listening yet. Re-declaring periodically is idempotent (the server replaces by id)
                // and is what makes the dashboard's panel repopulate on its own.
                if (declareTimer.Elapsed >= RedeclareInterval)
                {
                    declareTimer.Restart();
                    await DeclareStatsAsync(stoppingToken).ConfigureAwait(false);
                }

                if (statsTimer.ElapsedMilliseconds >= 1000)
                {
                    statsTimer.Restart();
                    await PublishStatsAsync(random, stoppingToken).ConfigureAwait(false);
                }

                await Task.Delay(100, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        finally
        {
            foreach (var service in _services)
            {
                service.Commands.CommandReceived -= OnCommand;
            }
        }
    }

    /// <summary>Emits one unit of business traffic, attributed to one of the simulated services.</summary>
    private void EmitOne(Random random)
    {
        var orderId = Interlocked.Increment(ref _ordersPlaced);
        var product = Products[random.Next(Products.Length)];
        var region = Regions[random.Next(Regions.Length)];
        var service = _services[random.Next(_services.Count)];

        // A root activity so every log below carries the same TraceId, as a real request would.
        using var root = Activity.StartActivity("checkout", ActivityKind.Server);
        root?.SetTag("order.id", orderId);
        root?.SetTag("region", region);

        OrderReceived(service.Orders, orderId, product, region);

        var roll = random.Next(100);
        if (roll < 55)
        {
            using var _ = Activity.StartActivity("billing.authorise", ActivityKind.Internal);
            PaymentAuthorised(service.Billing, orderId, Math.Round((random.NextDouble() * 40) + 4, 2));
        }
        else if (roll < 75)
        {
            using var _ = Activity.StartActivity("shipping.label", ActivityKind.Internal);
            LabelPrinted(service.Shipping, orderId, region, random.Next(1, 40));
        }
        else if (roll < 88)
        {
            SlowDownstream(service.Shipping, orderId, random.Next(900, 4000));
        }
        else if (roll < 96)
        {
            PaymentDeclined(service.Billing, orderId, "insufficient_funds");
        }
        else
        {
            // A real exception, so the dashboard's exception panel has a genuine stack trace.
            try
            {
                throw new TimeoutException($"The payment gateway did not respond for order {orderId}.");
            }
            catch (TimeoutException exception)
            {
                CheckoutFailed(service.Billing, exception, orderId);
            }
        }

        // Queue depth wanders so the statistic visibly moves.
        var delta = random.Next(-3, 5);
        Interlocked.Exchange(ref _queueDepth, Math.Clamp(_queueDepth + delta, 0, 500));
    }

    private async Task DeclareStatsAsync(CancellationToken cancellationToken)
    {
        // Declared on the first service's connection; statistics are server-wide, not per service.
        var stats = _services[0].Stats;

        try
        {
            await stats.DefineAsync(
                new StatDefinition { Id = "queue.depth", Title = "Queue depth", Color = "blue" },
                cancellationToken).ConfigureAwait(false);
            await stats.DefineAsync(
                new StatDefinition { Id = "cpu.percent", Title = "CPU %", Color = "amber" },
                cancellationToken).ConfigureAwait(false);
            await stats.DefineAsync(
                new StatDefinition { Id = "orders.total", Title = "Orders placed", Color = "green" },
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The server may not be up yet; the next pass re-declares, so losing this once is fine.
            StatsUnavailable(_logger, exception);
        }
    }

    private async Task PublishStatsAsync(Random random, CancellationToken cancellationToken)
    {
        var stats = _services[0].Stats;

        try
        {
            await stats.UpdateAsync("queue.depth", Volatile.Read(ref _queueDepth).ToString(), cancellationToken)
                .ConfigureAwait(false);
            await stats.UpdateAsync("cpu.percent", random.Next(8, 72).ToString(), cancellationToken)
                .ConfigureAwait(false);
            await stats.UpdateAsync("orders.total", Volatile.Read(ref _ordersPlaced).ToString(), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            StatsUnavailable(_logger, exception);
        }
    }

    /// <summary>Handles the commands an operator can send from the dashboard.</summary>
    private void OnCommand(object? sender, NebuLogCommand command)
    {
        // Every simulated service subscribes, so a command addressed to any of them is honoured.
        switch (command.Name)
        {
            case "ping":
                Pong(_services[0].Orders, command.IssuedBy);
                break;

            case "set-min-level":
                if (command.Arguments.TryGetValue("level", out var raw) &&
                    int.TryParse(raw, out var severity))
                {
                    var level = ToLogLevel(severity);
                    _levelSwitch.MinimumLevel = level;
                    MinimumLevelChanged(_services[0].Orders, level, command.IssuedBy);
                }

                break;

            case "burst":
                _burstUntil = DateTimeOffset.UtcNow.AddSeconds(_options.BurstSeconds);
                BurstStarted(_services[0].Orders, _options.BurstRatePerSecond, _options.BurstSeconds);
                break;

            default:
                UnknownCommand(_services[0].Orders, command.Name);
                break;
        }
    }

    private static LogLevel ToLogLevel(int severityNumber) => severityNumber switch
    {
        >= Severity.Fatal => LogLevel.Critical,
        >= Severity.Error => LogLevel.Error,
        >= Severity.Warn => LogLevel.Warning,
        >= Severity.Info => LogLevel.Information,
        >= Severity.Debug => LogLevel.Debug,
        _ => LogLevel.Trace,
    };

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Order {OrderId} received: {Product} to {Region}")]
    private static partial void OrderReceived(ILogger logger, int orderId, string product, string region);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug,
        Message = "Order {OrderId} authorised for {Amount:F2} NZD")]
    private static partial void PaymentAuthorised(ILogger logger, int orderId, double amount);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information,
        Message = "Order {OrderId} label printed for {Region}, {Grams} g")]
    private static partial void LabelPrinted(ILogger logger, int orderId, string region, int grams);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning,
        Message = "Order {OrderId} waited {ElapsedMs} ms on a downstream service")]
    private static partial void SlowDownstream(ILogger logger, int orderId, int elapsedMs);

    [LoggerMessage(EventId = 5, Level = LogLevel.Error,
        Message = "Order {OrderId} payment declined: {Reason}")]
    private static partial void PaymentDeclined(ILogger logger, int orderId, string reason);

    [LoggerMessage(EventId = 6, Level = LogLevel.Critical, Message = "Order {OrderId} checkout failed")]
    private static partial void CheckoutFailed(ILogger logger, Exception exception, int orderId);

    [LoggerMessage(EventId = 7, Level = LogLevel.Information, Message = "pong (requested by {IssuedBy})")]
    private static partial void Pong(ILogger logger, string issuedBy);

    [LoggerMessage(EventId = 8, Level = LogLevel.Information,
        Message = "Minimum level set to {Level} by {IssuedBy}")]
    private static partial void MinimumLevelChanged(ILogger logger, LogLevel level, string issuedBy);

    [LoggerMessage(EventId = 9, Level = LogLevel.Information,
        Message = "Burst mode: {RatePerSecond} entries/s for {Seconds} s")]
    private static partial void BurstStarted(ILogger logger, int ratePerSecond, int seconds);

    [LoggerMessage(EventId = 10, Level = LogLevel.Warning, Message = "Ignored unknown command {Name}")]
    private static partial void UnknownCommand(ILogger logger, string name);

    [LoggerMessage(EventId = 11, Level = LogLevel.Debug, Message = "Statistics could not be published")]
    private static partial void StatsUnavailable(ILogger logger, Exception exception);
}
