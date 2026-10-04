using System.Diagnostics.Tracing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using NebuLog.Contracts;
using NebuLog.Server.Authentication;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using Xunit;

namespace NebuLog.Server.Tests;

/// <summary>
/// The point of the whole OTLP endpoint: a stock OpenTelemetry SDK, configured only with a URL,
/// must be able to ship logs to NebuLog. Nothing here is NebuLog-specific except the endpoint.
/// </summary>
public sealed class OfficialExporterCompatibilityTests
{
    [Fact]
    public async Task OfficialDotNetExporterOverHttpProtobufIsReceivedAndForwardedToViewers()
    {
        var token = TestContext.Current.CancellationToken;

        // The OTLP exporter swallows transport failures, so without this a broken export would look
        // like a plain timeout. The listener turns it into a readable assertion message.
        using var diagnostics = new ExporterDiagnostics();

        await using var factory = new NebuLogAppFactory();
        await using var viewer = await factory.ConnectViewerAsync();

        var received = new TaskCompletionSource<IReadOnlyList<NebuLogEntry>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        viewer.On<IReadOnlyList<NebuLogEntry>>(HubRoutes.ReceiveLogs, batch => received.TrySetResult(batch));

        // The exporter authenticates like any other producer: an API key in X-Api-Key.
        var (_, apiKey) = await factory.IssueApiKeyAsync("official-exporter");
        using var recorder = new RecordingHandler(factory.Server.CreateHandler());

        using (var loggerFactory = LoggerFactory.Create(builder => builder
            .SetMinimumLevel(LogLevel.Trace)
            .AddOpenTelemetry(options =>
            {
                options.IncludeFormattedMessage = true;
                options.IncludeScopes = false;
                options.SetResourceBuilder(ResourceBuilder.CreateEmpty()
                    .AddService("PaymentsApi", serviceInstanceId: "pay-1"));
                options.AddOtlpExporter(otlp =>
                {
                    otlp.Protocol = OtlpExportProtocol.HttpProtobuf;
                    otlp.Endpoint = new Uri(factory.Server.BaseAddress, "v1/logs");
                    otlp.Headers = $"{ApiKeyAuthenticationOptions.HeaderName}={apiKey}";
                    otlp.HttpClientFactory = () => new HttpClient(recorder, disposeHandler: false);
                    otlp.ExportProcessorType = global::OpenTelemetry.ExportProcessorType.Simple;
                });
            })))
        {
            var logger = loggerFactory.CreateLogger("Payments.Gateway");
            logger.LogError(
                new TimeoutException("gateway did not respond"),
                "Payment {PaymentId} failed after {Attempts} attempts",
                "pay-99",
                3);
        }

        var exchanges = recorder.Exchanges;
        Assert.True(exchanges.Count > 0, $"The exporter made no HTTP request. {diagnostics.Describe()}");
        Assert.All(exchanges, exchange => Assert.Equal("application/x-protobuf", exchange.ContentType));
        Assert.All(exchanges, exchange => Assert.Equal(200, exchange.StatusCode));

        var batch = await received.Task.WaitAsync(TimeSpan.FromSeconds(15), token);
        var entry = Assert.Single(batch);

        Assert.Equal("Payment pay-99 failed after 3 attempts", entry.Body);
        Assert.Equal("PaymentsApi", entry.ServiceName);
        Assert.Equal("pay-1", entry.ServiceInstanceId);
        Assert.Equal("Payments.Gateway", entry.ScopeName);
        Assert.Equal(Severity.Error, entry.SeverityNumber);
        Assert.Equal(IngestSource.Otlp, entry.Source);
        Assert.Equal("pay-99", entry.Attributes["PaymentId"]);
        Assert.Equal("3", entry.Attributes["Attempts"]);

        Assert.NotNull(entry.Exception);

        // The official SDK writes the short CLR type name into exception.type; the OTLP path is a
        // faithful pass-through, so NebuLog stores whatever the producer sent. (NebuLog's own
        // exporter sends the full name — the difference is the producer's, not the server's.)
        Assert.Equal("TimeoutException", entry.Exception.Type);
        Assert.Equal("gateway did not respond", entry.Exception.Message);

        Assert.True(entry.TimestampUnixMs > 0);
        Assert.True(entry.ObservedUnixMs > 0);
    }

    /// <summary>
    /// Records what the exporter sent and bridges its synchronous <c>Send</c> onto the async path.
    /// </summary>
    /// <remarks>
    /// The OTLP HTTP exporter exports synchronously, but <c>TestServer</c>'s handler refuses
    /// synchronous calls outright. The bridge is a property of the in-memory test host, not of the
    /// NebuLog endpoint: over a real socket the exporter's own <c>Send</c> works unchanged.
    /// </remarks>
    private sealed class RecordingHandler : DelegatingHandler
    {
        private readonly List<Exchange> _exchanges = [];

        public RecordingHandler(HttpMessageHandler inner)
            : base(inner)
        {
        }

        public IReadOnlyList<Exchange> Exchanges
        {
            get
            {
                lock (_exchanges)
                {
                    return [.. _exchanges];
                }
            }
        }

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.Run(() => SendAsync(request, cancellationToken), cancellationToken).GetAwaiter().GetResult();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var contentType = request.Content?.Headers.ContentType?.MediaType ?? string.Empty;
            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            lock (_exchanges)
            {
                _exchanges.Add(new Exchange(
                    request.RequestUri?.ToString() ?? string.Empty,
                    contentType,
                    (int)response.StatusCode));
            }

            return response;
        }

        internal sealed record Exchange(string Url, string ContentType, int StatusCode);
    }

    /// <summary>Collects the OTLP exporter's own warnings, which are otherwise silent.</summary>
    private sealed class ExporterDiagnostics : EventListener
    {
        private readonly List<string> _events = [];

        public string Describe()
        {
            lock (_events)
            {
                return _events.Count == 0
                    ? "The exporter reported no diagnostics."
                    : "Exporter diagnostics: " + string.Join(" | ", _events);
            }
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            ArgumentNullException.ThrowIfNull(eventSource);

            if (eventSource.Name.StartsWith("OpenTelemetry-Exporter", StringComparison.Ordinal))
            {
                EnableEvents(eventSource, EventLevel.Warning);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            ArgumentNullException.ThrowIfNull(eventData);

            lock (_events)
            {
                _events.Add($"{eventData.EventName}: {string.Join(",", eventData.Payload ?? [])}");
            }
        }
    }
}
