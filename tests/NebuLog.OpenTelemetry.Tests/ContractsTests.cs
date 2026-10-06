using NebuLog.Contracts;
using Xunit;

namespace NebuLog.OpenTelemetry.Tests;

public sealed class ContractsTests
{
    [Fact]
    public void DefaultsAreEmptyRatherThanNull()
    {
        var entry = new NebuLogEntry();
        Assert.Equal(string.Empty, entry.Body);
        Assert.Equal(string.Empty, entry.ServiceName);
        Assert.Equal(string.Empty, entry.ScopeName);
        Assert.Empty(entry.Attributes);
        Assert.Null(entry.Exception);
        Assert.Equal(IngestSource.Otlp, entry.Source);

        var command = new NebuLogCommand();
        Assert.Equal(string.Empty, command.Name);
        Assert.Equal(string.Empty, command.IssuedBy);
        Assert.Empty(command.Arguments);

        var client = new ConnectedClientInfo();
        Assert.Equal(string.Empty, client.ConnectionId);
        Assert.Equal(string.Empty, client.Kind);
        Assert.Null(client.ServiceName);
        Assert.Null(client.ServiceInstanceId);
        Assert.Equal(0, client.ConnectedUnixMs);

        var definition = new StatDefinition();
        Assert.Equal(string.Empty, definition.Id);
        Assert.Equal(string.Empty, definition.Title);
        Assert.Null(definition.Color);

        var update = new StatUpdate();
        Assert.Equal(string.Empty, update.Id);
        Assert.Equal(string.Empty, update.Value);

        var exception = new ExceptionInfo();
        Assert.Equal(string.Empty, exception.Type);
        Assert.Equal(string.Empty, exception.Message);
        Assert.Null(exception.StackTrace);
    }

    [Fact]
    public void HistoryQueryDefaultsToTheDocumentedLimit()
    {
        var query = new HistoryQuery();

        Assert.Equal(HistoryQuery.DefaultLimit, query.Limit);
        Assert.Null(query.AfterId);
        Assert.Null(query.MinSeverity);
        Assert.Null(query.Service);
        Assert.Equal(10_000, HistoryQuery.MaxLimit);
    }

    [Fact]
    public void RecordsCompareByValue()
    {
        var left = new StatUpdate { Id = "rps", Value = "12", TimestampUnixMs = 7 };
        var right = new StatUpdate { Id = "rps", Value = "12", TimestampUnixMs = 7 };

        Assert.Equal(left, right);
        Assert.NotEqual(left, right with { Value = "13" });
    }

    [Fact]
    public void HubRoutesExposeTheSharedPathAndMethodNames()
    {
        Assert.Equal("/hubs/nebulog", HubRoutes.Path);
        Assert.Equal(nameof(HubRoutes.PublishLogs), HubRoutes.PublishLogs);
        Assert.Equal(nameof(HubRoutes.ReceiveLogs), HubRoutes.ReceiveLogs);
        Assert.Equal(nameof(HubRoutes.ReceiveCommand), HubRoutes.ReceiveCommand);
    }
}
