using Microsoft.Extensions.Logging;
using NebuLog.Contracts;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using Xunit;

namespace NebuLog.OpenTelemetry.Tests;

public sealed class ExporterMappingTests
{
    private static NebuLogExporterOptions Options => new()
    {
        Endpoint = new Uri("https://logs.example.test"),
        ApiKey = "k",
        FlushInterval = TimeSpan.FromMilliseconds(10),
    };

    private static ILoggerFactory CreatePipeline(NebuLogExporter exporter, params BaseProcessor<LogRecord>[] before) =>
        LoggerFactory.Create(builder => builder
            .SetMinimumLevel(LogLevel.Trace)
            .AddOpenTelemetry(options =>
            {
                options.IncludeFormattedMessage = true;
                options.SetResourceBuilder(ResourceBuilder.CreateEmpty()
                    .AddService("OrderApi", serviceInstanceId: "instance-7"));

                foreach (var processor in before)
                {
                    options.AddProcessor(processor);
                }

                options.AddProcessor(new SimpleLogRecordExportProcessor(exporter));
            }));

    private static async Task<IReadOnlyList<NebuLogEntry>> CaptureAsync(
        Action<ILogger> act,
        params BaseProcessor<LogRecord>[] before)
    {
        var transport = new FakeTransport();
        using var exporter = new NebuLogExporter(Options, _ => transport);

        using (var factory = CreatePipeline(exporter, before))
        {
            act(factory.CreateLogger("Orders.Checkout"));
        }

        await transport.FirstBatchReceived.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        return transport.Entries;
    }

    [Fact]
    public async Task MapsLevelCategoryMessageAndResource()
    {
        var entries = await CaptureAsync(logger =>
            logger.LogWarning("Order {OrderId} is late by {Minutes} minutes", 42, 7));

        var entry = Assert.Single(entries);
        Assert.Equal(Severity.Warn, entry.SeverityNumber);
        Assert.Equal("Warning", entry.SeverityText);
        Assert.Equal("Orders.Checkout", entry.ScopeName);
        Assert.Equal("Order 42 is late by 7 minutes", entry.Body);
        Assert.Equal("OrderApi", entry.ServiceName);
        Assert.Equal("instance-7", entry.ServiceInstanceId);
        Assert.Equal(IngestSource.SignalR, entry.Source);
        Assert.True(entry.TimestampUnixMs > 0);
        Assert.True(entry.ObservedUnixMs > 0);
    }

    [Fact]
    public async Task KeepsStructuredAttributesAndDropsOriginalFormat()
    {
        var entries = await CaptureAsync(logger =>
            logger.LogInformation("Order {OrderId} shipped", 42));

        var entry = Assert.Single(entries);
        Assert.Equal("42", entry.Attributes["OrderId"]);
        Assert.DoesNotContain("{OriginalFormat}", entry.Attributes.Keys);
    }

    [Fact]
    public async Task MapsException()
    {
        var entries = await CaptureAsync(logger =>
            logger.LogError(new InvalidOperationException("boom"), "Checkout failed"));

        var entry = Assert.Single(entries);
        Assert.Equal(Severity.Error, entry.SeverityNumber);
        Assert.NotNull(entry.Exception);
        Assert.Equal("System.InvalidOperationException", entry.Exception.Type);
        Assert.Equal("boom", entry.Exception.Message);
    }

    [Fact]
    public async Task MapsEventName()
    {
        var entries = await CaptureAsync(logger =>
            logger.Log(LogLevel.Information, new EventId(7, "OrderShipped"), "done"));

        var entry = Assert.Single(entries);
        Assert.Equal("OrderShipped", entry.EventName);
    }

    [Fact]
    public async Task RedactsSensitiveAttributesBeforeExport()
    {
        var entries = await CaptureAsync(
            logger => logger.LogInformation(
                "Auth {Password} {ApiKey} {UserName}", "hunter2", "live-key", "frank"),
            new RedactionProcessor());

        var entry = Assert.Single(entries);
        Assert.Equal(RedactionProcessor.Placeholder, entry.Attributes["Password"]);
        Assert.Equal(RedactionProcessor.Placeholder, entry.Attributes["ApiKey"]);
        Assert.Equal("frank", entry.Attributes["UserName"]);
    }

    [Fact]
    public async Task HonoursCustomRedactionPatterns()
    {
        var entries = await CaptureAsync(
            logger => logger.LogInformation("Pii {Iban} {Password}", "NZ12", "hunter2"),
            new RedactionProcessor(["iban"]));

        var entry = Assert.Single(entries);
        Assert.Equal(RedactionProcessor.Placeholder, entry.Attributes["Iban"]);
        Assert.Equal("hunter2", entry.Attributes["Password"]);
    }
}
