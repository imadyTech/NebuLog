using Google.Protobuf;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Common.V1;

namespace NebuLog.Server.Otlp;

/// <summary>Decodes an <c>ExportLogsServiceRequest</c> into the shared intermediate model.</summary>
internal static class OtlpProtobufDecoder
{
    /// <summary>Converts a parsed protobuf request.</summary>
    /// <param name="request">The parsed request.</param>
    public static OtlpPayload Decode(ExportLogsServiceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var resourceLogs = new List<OtlpResourceLogs>(request.ResourceLogs.Count);

        foreach (var resource in request.ResourceLogs)
        {
            var scopeLogs = new List<OtlpScopeLogs>(resource.ScopeLogs.Count);

            foreach (var scope in resource.ScopeLogs)
            {
                var records = new List<OtlpLogRecord>(scope.LogRecords.Count);

                foreach (var record in scope.LogRecords)
                {
                    records.Add(new OtlpLogRecord
                    {
                        TimeUnixNano = record.TimeUnixNano,
                        ObservedTimeUnixNano = record.ObservedTimeUnixNano,
                        SeverityNumber = (int)record.SeverityNumber,
                        SeverityText = record.SeverityText,
                        Body = Convert(record.Body),
                        Attributes = Convert(record.Attributes),
                        TraceId = ToHex(record.TraceId),
                        SpanId = ToHex(record.SpanId),
                        EventName = record.EventName,
                    });
                }

                scopeLogs.Add(new OtlpScopeLogs(scope.Scope?.Name ?? string.Empty, records));
            }

            resourceLogs.Add(new OtlpResourceLogs(
                resource.Resource is null ? [] : Convert(resource.Resource.Attributes),
                scopeLogs));
        }

        return new OtlpPayload(resourceLogs);
    }

    /// <summary>Renders an id as lowercase hex, treating an absent or all-zero id as unset.</summary>
    internal static string? ToHex(ByteString? id)
    {
        if (id is null || id.Length == 0)
        {
            return null;
        }

        var span = id.Span;
        var allZero = true;
        foreach (var value in span)
        {
            if (value != 0)
            {
                allZero = false;
                break;
            }
        }

        return allZero ? null : System.Convert.ToHexStringLower(span);
    }

    private static List<OtlpKeyValue> Convert(IEnumerable<KeyValue> attributes)
    {
        var converted = new List<OtlpKeyValue>();
        foreach (var attribute in attributes)
        {
            converted.Add(new OtlpKeyValue(attribute.Key, Convert(attribute.Value)));
        }

        return converted;
    }

    private static OtlpAnyValue? Convert(AnyValue? value) => value?.ValueCase switch
    {
        AnyValue.ValueOneofCase.StringValue => new OtlpAnyValue { StringValue = value.StringValue },
        AnyValue.ValueOneofCase.BoolValue => new OtlpAnyValue { BoolValue = value.BoolValue },
        AnyValue.ValueOneofCase.IntValue => new OtlpAnyValue { IntValue = value.IntValue },
        AnyValue.ValueOneofCase.DoubleValue => new OtlpAnyValue { DoubleValue = value.DoubleValue },
        AnyValue.ValueOneofCase.BytesValue => new OtlpAnyValue { BytesValue = value.BytesValue.ToByteArray() },
        AnyValue.ValueOneofCase.ArrayValue => new OtlpAnyValue
        {
            ArrayValue = [.. value.ArrayValue.Values.Select(Convert).Select(item => item ?? new OtlpAnyValue())],
        },
        AnyValue.ValueOneofCase.KvlistValue => new OtlpAnyValue { KvListValue = Convert(value.KvlistValue.Values) },
        _ => null,
    };
}
