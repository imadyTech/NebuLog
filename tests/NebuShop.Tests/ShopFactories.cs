using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NebuShop.Orders;
using NebuShop.Payments;

namespace NebuShop.Tests;

/// <summary>One log entry as the tests observed it.</summary>
/// <param name="Category">The logger category.</param>
/// <param name="Level">Severity.</param>
/// <param name="Message">The formatted message.</param>
/// <param name="Attributes">The structured state, which is what redaction operates on.</param>
/// <param name="TraceId">The trace the entry belongs to, if any.</param>
public sealed record CapturedEntry(
    string Category,
    LogLevel Level,
    string Message,
    IReadOnlyDictionary<string, string?> Attributes,
    string TraceId);

/// <summary>
/// Collects what the application logs, so a test can assert on it.
/// </summary>
/// <remarks>
/// This deliberately sits at the <see cref="ILogger"/> boundary rather than inside the exporter:
/// the tests here are about what the two shop services produce, and keeping the capture upstream
/// means they do not need a live NebuLog server.
/// </remarks>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<CapturedEntry> _entries = new();

    /// <summary>Everything logged so far.</summary>
    public IReadOnlyCollection<CapturedEntry> Entries => _entries;

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _entries);

    /// <inheritdoc />
    public void Dispose() => GC.SuppressFinalize(this);

    private sealed class CapturingLogger : ILogger
    {
        private readonly string _category;
        private readonly ConcurrentQueue<CapturedEntry> _entries;

        public CapturingLogger(string category, ConcurrentQueue<CapturedEntry> entries)
        {
            _category = category;
            _entries = entries;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            var attributes = new Dictionary<string, string?>(StringComparer.Ordinal);
            if (state is IReadOnlyList<KeyValuePair<string, object?>> pairs)
            {
                foreach (var pair in pairs)
                {
                    attributes[pair.Key] = pair.Value?.ToString();
                }
            }

            _entries.Enqueue(new CapturedEntry(
                _category,
                logLevel,
                formatter(state, exception),
                attributes,
                System.Diagnostics.Activity.Current?.TraceId.ToString() ?? string.Empty));
        }
    }
}

/// <summary>Hosts the payments service in memory.</summary>
public sealed class PaymentsFactory : WebApplicationFactory<PaymentOptions>
{
    /// <summary>What the payments service logged.</summary>
    public CapturingLoggerProvider Logs { get; } = new();

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("Production");
        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                // Fast and deterministic: the real range exists to make the demo interesting,
                // not to make the tests slow.
                ["NebuLog:ShopPayments:ApiKey"] = "test-key-not-used-no-server-is-running",
                ["NebuShop:MinLatencyMs"] = "1",
                ["NebuShop:MaxLatencyMs"] = "2",
                ["NebuShop:SlowThresholdMs"] = "0",
            }));

        builder.ConfigureServices(services => services.AddSingleton<ILoggerProvider>(Logs));
    }
}

/// <summary>
/// Hosts the orders service in memory, with its payments client pointed at a real in-memory
/// payments service.
/// </summary>
public sealed class OrdersFactory : WebApplicationFactory<OrderService>
{
    private readonly PaymentsFactory? _payments;
    private readonly Dictionary<string, string?> _settings;

    /// <summary>Creates the factory.</summary>
    /// <param name="payments">
    /// The payments service to call, or <see langword="null"/> when the test does not check out.
    /// </param>
    /// <param name="settings">Extra configuration for this host.</param>
    public OrdersFactory(PaymentsFactory? payments = null, Dictionary<string, string?>? settings = null)
    {
        _payments = payments;
        _settings = settings ?? new Dictionary<string, string?>(StringComparer.Ordinal);
    }

    /// <summary>What the orders service logged.</summary>
    public CapturingLoggerProvider Logs { get; } = new();

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("Production");
        _settings.TryAdd("NebuLog:ShopOrders:ApiKey", "test-key-not-used-no-server-is-running");
        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(_settings));
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<ILoggerProvider>(Logs);

            if (_payments is not null)
            {
                // The payments service runs on a TestServer, which has no socket: its handler is
                // injected so the outbound call is real HTTP all the way through the pipeline —
                // including the traceparent header, which is what the trace test depends on.
                services.AddHttpClient(OrderService.PaymentsClientName)
                    .ConfigurePrimaryHttpMessageHandler(() => _payments.Server.CreateHandler());
            }
        });
    }
}
