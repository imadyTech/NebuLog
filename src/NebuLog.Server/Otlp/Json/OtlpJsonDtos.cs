using System.Text.Json.Serialization;

namespace NebuLog.Server.Otlp.Json;

/// <summary>
/// The minimal OTLP/JSON shape NebuLog accepts for <c>POST /v1/logs</c>.
/// </summary>
/// <remarks>
/// OTLP/JSON is not protobuf's canonical JSON mapping, so <c>Google.Protobuf.JsonParser</c> cannot
/// read it: trace and span ids are hex rather than base64, enums may be numbers or names, and
/// 64-bit integers may arrive as strings. These DTOs model what the specification actually puts on
/// the wire, and are serialized through a source-generated context so no runtime reflection is used.
/// </remarks>
internal sealed class OtlpJsonExportRequest
{
    [JsonPropertyName("resourceLogs")]
    public List<OtlpJsonResourceLogs>? ResourceLogs { get; set; }
}

/// <summary>Log records sharing one resource.</summary>
internal sealed class OtlpJsonResourceLogs
{
    [JsonPropertyName("resource")]
    public OtlpJsonResource? Resource { get; set; }

    [JsonPropertyName("scopeLogs")]
    public List<OtlpJsonScopeLogs>? ScopeLogs { get; set; }
}

/// <summary>The entity producing the records.</summary>
internal sealed class OtlpJsonResource
{
    [JsonPropertyName("attributes")]
    public List<OtlpJsonKeyValue>? Attributes { get; set; }
}

/// <summary>Log records sharing one instrumentation scope.</summary>
internal sealed class OtlpJsonScopeLogs
{
    [JsonPropertyName("scope")]
    public OtlpJsonScope? Scope { get; set; }

    [JsonPropertyName("logRecords")]
    public List<OtlpJsonLogRecord>? LogRecords { get; set; }
}

/// <summary>The instrumentation scope.</summary>
internal sealed class OtlpJsonScope
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

/// <summary>One OTLP/JSON log record.</summary>
internal sealed class OtlpJsonLogRecord
{
    [JsonPropertyName("timeUnixNano")]
    [JsonConverter(typeof(OtlpJsonUInt64Converter))]
    public ulong TimeUnixNano { get; set; }

    [JsonPropertyName("observedTimeUnixNano")]
    [JsonConverter(typeof(OtlpJsonUInt64Converter))]
    public ulong ObservedTimeUnixNano { get; set; }

    [JsonPropertyName("severityNumber")]
    [JsonConverter(typeof(OtlpJsonSeverityNumberConverter))]
    public int SeverityNumber { get; set; }

    [JsonPropertyName("severityText")]
    public string? SeverityText { get; set; }

    [JsonPropertyName("body")]
    public OtlpJsonAnyValue? Body { get; set; }

    [JsonPropertyName("attributes")]
    public List<OtlpJsonKeyValue>? Attributes { get; set; }

    [JsonPropertyName("traceId")]
    public string? TraceId { get; set; }

    [JsonPropertyName("spanId")]
    public string? SpanId { get; set; }

    [JsonPropertyName("eventName")]
    public string? EventName { get; set; }
}

/// <summary>One OTLP/JSON attribute.</summary>
internal sealed class OtlpJsonKeyValue
{
    [JsonPropertyName("key")]
    public string? Key { get; set; }

    [JsonPropertyName("value")]
    public OtlpJsonAnyValue? Value { get; set; }
}

/// <summary>An OTLP/JSON <c>AnyValue</c>; exactly one property is populated.</summary>
internal sealed class OtlpJsonAnyValue
{
    [JsonPropertyName("stringValue")]
    public string? StringValue { get; set; }

    [JsonPropertyName("boolValue")]
    public bool? BoolValue { get; set; }

    [JsonPropertyName("intValue")]
    [JsonConverter(typeof(OtlpJsonNullableInt64Converter))]
    public long? IntValue { get; set; }

    [JsonPropertyName("doubleValue")]
    public double? DoubleValue { get; set; }

    [JsonPropertyName("bytesValue")]
    public string? BytesValue { get; set; }

    [JsonPropertyName("arrayValue")]
    public OtlpJsonArrayValue? ArrayValue { get; set; }

    [JsonPropertyName("kvlistValue")]
    public OtlpJsonKvListValue? KvListValue { get; set; }
}

/// <summary>An OTLP/JSON array value.</summary>
internal sealed class OtlpJsonArrayValue
{
    [JsonPropertyName("values")]
    public List<OtlpJsonAnyValue>? Values { get; set; }
}

/// <summary>An OTLP/JSON key-value-list value.</summary>
internal sealed class OtlpJsonKvListValue
{
    [JsonPropertyName("values")]
    public List<OtlpJsonKeyValue>? Values { get; set; }
}

/// <summary>What NebuLog returns from <c>POST /v1/logs</c> for a JSON request.</summary>
internal sealed class OtlpJsonExportResponse
{
    [JsonPropertyName("partialSuccess")]
    public OtlpJsonPartialSuccess? PartialSuccess { get; set; }
}

/// <summary>The <c>partial_success</c> block of an OTLP response.</summary>
internal sealed class OtlpJsonPartialSuccess
{
    [JsonPropertyName("rejectedLogRecords")]
    public long RejectedLogRecords { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }
}

/// <summary>The JSON form of <c>google.rpc.Status</c>, returned for a malformed request.</summary>
internal sealed class OtlpJsonStatus
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

/// <summary>Source-generated serialization for the OTLP/JSON surface.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(OtlpJsonExportRequest))]
[JsonSerializable(typeof(OtlpJsonExportResponse))]
[JsonSerializable(typeof(OtlpJsonStatus))]
internal sealed partial class OtlpJsonContext : JsonSerializerContext
{
}
