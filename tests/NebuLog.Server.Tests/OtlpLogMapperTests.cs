using NebuLog.Contracts;
using NebuLog.Server.Otlp;
using Xunit;

namespace NebuLog.Server.Tests;

public sealed class OtlpLogMapperTests
{
    private const long Received = 1_700_000_000_000;

    private static OtlpPayload PayloadOf(
        OtlpLogRecord record,
        IReadOnlyList<OtlpKeyValue>? resourceAttributes = null,
        string scopeName = "Orders.Checkout") =>
        new([new OtlpResourceLogs(resourceAttributes ?? [], [new OtlpScopeLogs(scopeName, [record])])]);

    private static NebuLogEntry MapOne(
        OtlpLogRecord record,
        IReadOnlyList<OtlpKeyValue>? resourceAttributes = null,
        string scopeName = "Orders.Checkout") =>
        Assert.Single(OtlpLogMapper.Map(PayloadOf(record, resourceAttributes, scopeName), Received));

    [Fact]
    public void UsesTimeUnixNanoWhenPresent()
    {
        var entry = MapOne(new OtlpLogRecord
        {
            TimeUnixNano = 1_600_000_000_123_456_789,
            ObservedTimeUnixNano = 1_600_000_009_000_000_000,
        });

        Assert.Equal(1_600_000_000_123, entry.TimestampUnixMs);
        Assert.Equal(Received, entry.ObservedUnixMs);
    }

    [Fact]
    public void FallsBackToObservedTimeThenToServerTime()
    {
        var observedOnly = MapOne(new OtlpLogRecord { ObservedTimeUnixNano = 1_600_000_009_000_000_000 });
        Assert.Equal(1_600_000_009_000, observedOnly.TimestampUnixMs);

        var neither = MapOne(new OtlpLogRecord());
        Assert.Equal(Received, neither.TimestampUnixMs);
    }

    [Fact]
    public void KeepsAnExplicitSeverityNumber()
    {
        var entry = MapOne(new OtlpLogRecord { SeverityNumber = 18, SeverityText = "ERROR2" });

        Assert.Equal(18, entry.SeverityNumber);
        Assert.Equal("ERROR2", entry.SeverityText);
    }

    [Theory]
    [InlineData("TRACE", 1)]
    [InlineData("debug", 5)]
    [InlineData("Info", 9)]
    [InlineData("INFORMATION", 9)]
    [InlineData("warn", 13)]
    [InlineData("WARNING", 13)]
    [InlineData("Error", 17)]
    [InlineData("FATAL", 21)]
    [InlineData("critical", 21)]
    [InlineData("whatever", 0)]
    [InlineData("", 0)]
    [InlineData(null, 0)]
    public void InfersSeverityFromTextWhenTheNumberIsMissing(string? severityText, int expected)
    {
        Assert.Equal(expected, OtlpLogMapper.InferSeverity(severityText));
        Assert.Equal(expected, MapOne(new OtlpLogRecord { SeverityText = severityText }).SeverityNumber);
    }

    [Fact]
    public void PassesAStringBodyThroughUnchanged() =>
        Assert.Equal(
            "order 42 shipped",
            MapOne(new OtlpLogRecord { Body = OtlpAnyValue.FromString("order 42 shipped") }).Body);

    // The payload types are internal to the server library, so the cases are named here and built
    // inside the test rather than passed through MemberData, which xUnit requires to be public.
    [Theory]
    [InlineData("bool", "true")]
    [InlineData("int", "-17")]
    [InlineData("double", "1.5")]
    [InlineData("bytes", "\"AQID\"")]
    [InlineData("array", "[\"a\",2]")]
    [InlineData("kvlist", "{\"k\":\"v\",\"n\":1}")]
    public void SerialisesNonStringBodiesAsCompactJson(string kind, string expected) =>
        Assert.Equal(expected, MapOne(new OtlpLogRecord { Body = BodyOf(kind) }).Body);

    private static OtlpAnyValue BodyOf(string kind) => kind switch
    {
        "bool" => new OtlpAnyValue { BoolValue = true },
        "int" => new OtlpAnyValue { IntValue = -17 },
        "double" => new OtlpAnyValue { DoubleValue = 1.5 },
        "bytes" => new OtlpAnyValue { BytesValue = [1, 2, 3] },
        "array" => new OtlpAnyValue
        {
            ArrayValue = [OtlpAnyValue.FromString("a"), new OtlpAnyValue { IntValue = 2 }],
        },
        "kvlist" => new OtlpAnyValue
        {
            KvListValue =
            [
                new OtlpKeyValue("k", OtlpAnyValue.FromString("v")),
                new OtlpKeyValue("n", new OtlpAnyValue { IntValue = 1 }),
            ],
        },
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown body kind."),
    };

    [Fact]
    public void EscapesControlCharactersAndQuotesInsideJsonBodies()
    {
        var entry = MapOne(new OtlpLogRecord
        {
            Body = new OtlpAnyValue { ArrayValue = [OtlpAnyValue.FromString("a\"b\\c\nd\u0001")] },
        });

        Assert.Equal("[\"a\\\"b\\\\c\\nd\\u0001\"]", entry.Body);
    }

    [Fact]
    public void EmptyBodyBecomesAnEmptyString() => Assert.Equal(string.Empty, MapOne(new OtlpLogRecord()).Body);

    [Fact]
    public void ReadsServiceIdentityFromTheResource()
    {
        var entry = MapOne(
            new OtlpLogRecord(),
            [
                new OtlpKeyValue("service.name", OtlpAnyValue.FromString("OrderApi")),
                new OtlpKeyValue("service.instance.id", OtlpAnyValue.FromString("instance-7")),
                new OtlpKeyValue("host.name", OtlpAnyValue.FromString("w1")),
            ]);

        Assert.Equal("OrderApi", entry.ServiceName);
        Assert.Equal("instance-7", entry.ServiceInstanceId);

        // Resource attributes other than the service identity are not copied onto the entry.
        Assert.Empty(entry.Attributes);
    }

    [Fact]
    public void FallsBackToUnknownServiceName()
    {
        var entry = MapOne(new OtlpLogRecord());

        Assert.Equal(OtlpLogMapper.UnknownServiceName, entry.ServiceName);
        Assert.Null(entry.ServiceInstanceId);
    }

    [Fact]
    public void TakesTheScopeNameAsTheCategory() =>
        Assert.Equal("Orders.Checkout", MapOne(new OtlpLogRecord()).ScopeName);

    [Fact]
    public void KeepsNonZeroTraceAndSpanIds()
    {
        var entry = MapOne(new OtlpLogRecord
        {
            TraceId = "4bf92f3577b34da6a3ce929d0e0e4736",
            SpanId = "00f067aa0ba902b7",
        });

        Assert.Equal("4bf92f3577b34da6a3ce929d0e0e4736", entry.TraceId);
        Assert.Equal("00f067aa0ba902b7", entry.SpanId);
    }

    [Fact]
    public void TreatsAbsentIdsAsNull()
    {
        var entry = MapOne(new OtlpLogRecord());

        Assert.Null(entry.TraceId);
        Assert.Null(entry.SpanId);
    }

    [Fact]
    public void CarriesTheEventName()
    {
        Assert.Equal("OrderShipped", MapOne(new OtlpLogRecord { EventName = "OrderShipped" }).EventName);
        Assert.Null(MapOne(new OtlpLogRecord { EventName = string.Empty }).EventName);
    }

    [Fact]
    public void LiftsExceptionAttributesOutOfTheAttributeBag()
    {
        var entry = MapOne(new OtlpLogRecord
        {
            Attributes =
            [
                new OtlpKeyValue("exception.type", OtlpAnyValue.FromString("System.InvalidOperationException")),
                new OtlpKeyValue("exception.message", OtlpAnyValue.FromString("boom")),
                new OtlpKeyValue("exception.stacktrace", OtlpAnyValue.FromString("   at Thing()")),
                new OtlpKeyValue("order.id", new OtlpAnyValue { IntValue = 42 }),
            ],
        });

        Assert.NotNull(entry.Exception);
        Assert.Equal("System.InvalidOperationException", entry.Exception.Type);
        Assert.Equal("boom", entry.Exception.Message);
        Assert.Equal("   at Thing()", entry.Exception.StackTrace);

        Assert.Equal("42", Assert.Single(entry.Attributes).Value);
        Assert.DoesNotContain("exception.type", entry.Attributes.Keys);
    }

    [Fact]
    public void BuildsAnExceptionFromAPartialSet()
    {
        var entry = MapOne(new OtlpLogRecord
        {
            Attributes = [new OtlpKeyValue("exception.message", OtlpAnyValue.FromString("only a message"))],
        });

        Assert.NotNull(entry.Exception);
        Assert.Equal(string.Empty, entry.Exception.Type);
        Assert.Equal("only a message", entry.Exception.Message);
        Assert.Null(entry.Exception.StackTrace);
    }

    [Fact]
    public void LeavesExceptionNullWhenNoExceptionAttributesArePresent() =>
        Assert.Null(MapOne(new OtlpLogRecord
        {
            Attributes = [new OtlpKeyValue("order.id", new OtlpAnyValue { IntValue = 1 })],
        }).Exception);

    [Fact]
    public void StampsTheOtlpSource() => Assert.Equal(IngestSource.Otlp, MapOne(new OtlpLogRecord()).Source);

    [Fact]
    public void MapsEveryRecordAcrossResourcesAndScopes()
    {
        var payload = new OtlpPayload(
        [
            new OtlpResourceLogs(
                [new OtlpKeyValue("service.name", OtlpAnyValue.FromString("a"))],
                [
                    new OtlpScopeLogs("scope-1", [new OtlpLogRecord(), new OtlpLogRecord()]),
                    new OtlpScopeLogs("scope-2", [new OtlpLogRecord()]),
                ]),
            new OtlpResourceLogs(
                [new OtlpKeyValue("service.name", OtlpAnyValue.FromString("b"))],
                [new OtlpScopeLogs("scope-3", [new OtlpLogRecord()])]),
        ]);

        var entries = OtlpLogMapper.Map(payload, Received);

        Assert.Equal(4, entries.Count);
        Assert.Equal(["a", "a", "a", "b"], entries.Select(entry => entry.ServiceName));
        Assert.Equal(["scope-1", "scope-1", "scope-2", "scope-3"], entries.Select(entry => entry.ScopeName));
    }

    [Fact]
    public void AnEmptyPayloadMapsToNothing() => Assert.Empty(OtlpLogMapper.Map(new OtlpPayload([]), Received));
}
