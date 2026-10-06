namespace NebuLog.Server.Otlp.Json;

/// <summary>Converts the OTLP/JSON DTOs into the shared intermediate model.</summary>
internal static class OtlpJsonDecoder
{
    /// <summary>Converts a deserialized OTLP/JSON request.</summary>
    /// <param name="request">The deserialized request.</param>
    public static OtlpPayload Decode(OtlpJsonExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var resourceLogs = new List<OtlpResourceLogs>(request.ResourceLogs?.Count ?? 0);

        foreach (var resource in request.ResourceLogs ?? [])
        {
            var scopeLogs = new List<OtlpScopeLogs>(resource.ScopeLogs?.Count ?? 0);

            foreach (var scope in resource.ScopeLogs ?? [])
            {
                var records = new List<OtlpLogRecord>(scope.LogRecords?.Count ?? 0);

                foreach (var record in scope.LogRecords ?? [])
                {
                    records.Add(new OtlpLogRecord
                    {
                        TimeUnixNano = record.TimeUnixNano,
                        ObservedTimeUnixNano = record.ObservedTimeUnixNano,
                        SeverityNumber = record.SeverityNumber,
                        SeverityText = record.SeverityText,
                        Body = Convert(record.Body),
                        Attributes = Convert(record.Attributes),
                        TraceId = NormaliseHexId(record.TraceId),
                        SpanId = NormaliseHexId(record.SpanId),
                        EventName = record.EventName,
                    });
                }

                scopeLogs.Add(new OtlpScopeLogs(scope.Scope?.Name ?? string.Empty, records));
            }

            resourceLogs.Add(new OtlpResourceLogs(Convert(resource.Resource?.Attributes), scopeLogs));
        }

        return new OtlpPayload(resourceLogs);
    }

    /// <summary>
    /// Lower-cases a hex id and treats an empty or all-zero id as unset, matching the protobuf path.
    /// </summary>
    internal static string? NormaliseHexId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var trimmed = id.Trim();
        foreach (var character in trimmed)
        {
            if (character != '0')
            {
                return trimmed.ToLowerInvariant();
            }
        }

        return null;
    }

    private static List<OtlpKeyValue> Convert(List<OtlpJsonKeyValue>? attributes)
    {
        var converted = new List<OtlpKeyValue>(attributes?.Count ?? 0);

        foreach (var attribute in attributes ?? [])
        {
            if (attribute.Key is { Length: > 0 } key)
            {
                converted.Add(new OtlpKeyValue(key, Convert(attribute.Value)));
            }
        }

        return converted;
    }

    private static OtlpAnyValue? Convert(OtlpJsonAnyValue? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value.StringValue is { } text)
        {
            return new OtlpAnyValue { StringValue = text };
        }

        if (value.BoolValue is { } flag)
        {
            return new OtlpAnyValue { BoolValue = flag };
        }

        if (value.IntValue is { } number)
        {
            return new OtlpAnyValue { IntValue = number };
        }

        if (value.DoubleValue is { } real)
        {
            return new OtlpAnyValue { DoubleValue = real };
        }

        if (value.BytesValue is { } base64)
        {
            // OTLP/JSON encodes bytes as base64, the one place it agrees with canonical protobuf JSON.
            // A value that is not valid base64 is kept as the literal string rather than discarded.
            var decoded = new byte[((base64.Length * 3) + 3) / 4];
            return System.Convert.TryFromBase64String(base64, decoded, out var written)
                ? new OtlpAnyValue { BytesValue = decoded[..written] }
                : new OtlpAnyValue { StringValue = base64 };
        }

        if (value.ArrayValue is { Values: { } items })
        {
            var array = new List<OtlpAnyValue>(items.Count);
            foreach (var item in items)
            {
                array.Add(Convert(item) ?? new OtlpAnyValue());
            }

            return new OtlpAnyValue { ArrayValue = array };
        }

        if (value.KvListValue is { Values: { } pairs })
        {
            return new OtlpAnyValue { KvListValue = Convert(pairs) };
        }

        return null;
    }
}
