namespace NebuLog.Server.Otlp;

/// <summary>
/// The decoded shape of one OTLP log export, independent of whether it arrived as protobuf or JSON.
/// </summary>
/// <remarks>
/// Both wire formats decode into this model and then go through <see cref="OtlpLogMapper"/>, so the
/// mapping rules exist in exactly one place and cannot drift between the two encodings.
/// </remarks>
/// <param name="ResourceLogs">The resource groups in the export.</param>
internal sealed record OtlpPayload(IReadOnlyList<OtlpResourceLogs> ResourceLogs);

/// <summary>Log records sharing one resource.</summary>
/// <param name="ResourceAttributes">Attributes describing the producing entity.</param>
/// <param name="ScopeLogs">The instrumentation scopes under this resource.</param>
internal sealed record OtlpResourceLogs(
    IReadOnlyList<OtlpKeyValue> ResourceAttributes,
    IReadOnlyList<OtlpScopeLogs> ScopeLogs);

/// <summary>Log records sharing one instrumentation scope.</summary>
/// <param name="ScopeName">The scope name, which becomes the logger category.</param>
/// <param name="LogRecords">The records in this scope.</param>
internal sealed record OtlpScopeLogs(string ScopeName, IReadOnlyList<OtlpLogRecord> LogRecords);

/// <summary>One OTLP log record.</summary>
internal sealed record OtlpLogRecord
{
    /// <summary>When the event occurred, in Unix nanoseconds; 0 when unset.</summary>
    public ulong TimeUnixNano { get; init; }

    /// <summary>When the SDK observed the event, in Unix nanoseconds; 0 when unset.</summary>
    public ulong ObservedTimeUnixNano { get; init; }

    /// <summary>The OTLP severity number; 0 when unset.</summary>
    public int SeverityNumber { get; init; }

    /// <summary>The producer's severity label, when supplied.</summary>
    public string? SeverityText { get; init; }

    /// <summary>The record body.</summary>
    public OtlpAnyValue? Body { get; init; }

    /// <summary>The record's own attributes.</summary>
    public IReadOnlyList<OtlpKeyValue> Attributes { get; init; } = [];

    /// <summary>The trace id as lowercase hexadecimal, or <see langword="null"/> when unset or all-zero.</summary>
    public string? TraceId { get; init; }

    /// <summary>The span id as lowercase hexadecimal, or <see langword="null"/> when unset or all-zero.</summary>
    public string? SpanId { get; init; }

    /// <summary>The event name, when the record carries one.</summary>
    public string? EventName { get; init; }
}

/// <summary>One OTLP attribute.</summary>
/// <param name="Key">The attribute key.</param>
/// <param name="Value">The attribute value, or <see langword="null"/> when the value was absent.</param>
internal sealed record OtlpKeyValue(string Key, OtlpAnyValue? Value);

/// <summary>
/// An OTLP <c>AnyValue</c>. Exactly one property is set; everything else is <see langword="null"/>.
/// </summary>
internal sealed record OtlpAnyValue
{
    /// <summary>A string value.</summary>
    public string? StringValue { get; init; }

    /// <summary>A boolean value.</summary>
    public bool? BoolValue { get; init; }

    /// <summary>A 64-bit integer value.</summary>
    public long? IntValue { get; init; }

    /// <summary>A double value.</summary>
    public double? DoubleValue { get; init; }

    /// <summary>A byte-array value.</summary>
    public byte[]? BytesValue { get; init; }

    /// <summary>An array value.</summary>
    public IReadOnlyList<OtlpAnyValue>? ArrayValue { get; init; }

    /// <summary>A key-value-list value.</summary>
    public IReadOnlyList<OtlpKeyValue>? KvListValue { get; init; }

    /// <summary>Wraps a string.</summary>
    /// <param name="value">The string to wrap.</param>
    public static OtlpAnyValue FromString(string value) => new() { StringValue = value };
}
