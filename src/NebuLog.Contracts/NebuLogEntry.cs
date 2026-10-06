using System.Collections.Generic;

namespace NebuLog.Contracts;

/// <summary>
/// A single log entry as it travels between NebuLog clients and the server.
/// </summary>
/// <remarks>
/// All timestamps are Unix epoch milliseconds so that JSON, MessagePack and JavaScript
/// agree on both precision and format. OTLP's nanosecond timestamps are converted on the server.
/// </remarks>
public sealed record NebuLogEntry
{
    /// <summary>Monotonic sequence number assigned by the server; clients send <c>0</c>.</summary>
    public long Id { get; init; }

    /// <summary>When the event occurred, in Unix epoch milliseconds.</summary>
    public long TimestampUnixMs { get; init; }

    /// <summary>When the event was observed by the SDK, in Unix epoch milliseconds.</summary>
    public long ObservedUnixMs { get; init; }

    /// <summary>OpenTelemetry severity number (1–24); <c>0</c> means unspecified.</summary>
    public int SeverityNumber { get; init; }

    /// <summary>The original severity label, when the producer supplied one.</summary>
    public string? SeverityText { get; init; }

    /// <summary>The rendered log message.</summary>
    public string Body { get; init; } = string.Empty;

    /// <summary>The producing service's <c>service.name</c>.</summary>
    public string ServiceName { get; init; } = string.Empty;

    /// <summary>The producing service's <c>service.instance.id</c>, when known.</summary>
    public string? ServiceInstanceId { get; init; }

    /// <summary>The logger category (OpenTelemetry instrumentation scope) name.</summary>
    public string ScopeName { get; init; } = string.Empty;

    /// <summary>The correlated trace id as lowercase hexadecimal, when the entry was recorded inside a trace.</summary>
    public string? TraceId { get; init; }

    /// <summary>The correlated span id as lowercase hexadecimal, when the entry was recorded inside a span.</summary>
    public string? SpanId { get; init; }

    /// <summary>The event name, when the producer named the event.</summary>
    public string? EventName { get; init; }

    /// <summary>Structured attributes attached to the entry.</summary>
    public IReadOnlyDictionary<string, string> Attributes { get; init; } = EmptyAttributes;

    /// <summary>The exception attached to the entry, when there is one.</summary>
    public ExceptionInfo? Exception { get; init; }

    /// <summary>The ingestion path the entry arrived through.</summary>
    public IngestSource Source { get; init; }

    private static readonly IReadOnlyDictionary<string, string> EmptyAttributes =
        new Dictionary<string, string>(0);
}
