using NebuLog.Contracts;

namespace NebuLog.Server.Otlp;

/// <summary>Turns a decoded OTLP export into <see cref="NebuLogEntry"/> values.</summary>
internal static class OtlpLogMapper
{
    /// <summary>The service name used when the resource carries none.</summary>
    public const string UnknownServiceName = "unknown_service";

    private const string ServiceNameKey = "service.name";
    private const string ServiceInstanceKey = "service.instance.id";
    private const string ExceptionTypeKey = "exception.type";
    private const string ExceptionMessageKey = "exception.message";
    private const string ExceptionStackTraceKey = "exception.stacktrace";

    /// <summary>Maps every record in the payload.</summary>
    /// <param name="payload">The decoded export.</param>
    /// <param name="receivedUnixMs">Server receive time, used for <c>ObservedUnixMs</c> and as a timestamp fallback.</param>
    /// <returns>The mapped entries, in payload order.</returns>
    public static IReadOnlyList<NebuLogEntry> Map(OtlpPayload payload, long receivedUnixMs)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var entries = new List<NebuLogEntry>();

        foreach (var resourceLogs in payload.ResourceLogs)
        {
            var (serviceName, serviceInstanceId) = ReadService(resourceLogs.ResourceAttributes);

            foreach (var scopeLogs in resourceLogs.ScopeLogs)
            {
                foreach (var record in scopeLogs.LogRecords)
                {
                    entries.Add(MapRecord(record, scopeLogs.ScopeName, serviceName, serviceInstanceId, receivedUnixMs));
                }
            }
        }

        return entries;
    }

    private static NebuLogEntry MapRecord(
        OtlpLogRecord record,
        string scopeName,
        string serviceName,
        string? serviceInstanceId,
        long receivedUnixMs)
    {
        var timestamp = record.TimeUnixNano != 0
            ? ToUnixMs(record.TimeUnixNano)
            : record.ObservedTimeUnixNano != 0
                ? ToUnixMs(record.ObservedTimeUnixNano)
                : receivedUnixMs;

        var severityNumber = record.SeverityNumber != 0
            ? record.SeverityNumber
            : InferSeverity(record.SeverityText);

        var (attributes, exception) = SplitAttributes(record.Attributes);

        return new NebuLogEntry
        {
            TimestampUnixMs = timestamp,
            ObservedUnixMs = receivedUnixMs,
            SeverityNumber = severityNumber,
            SeverityText = string.IsNullOrEmpty(record.SeverityText) ? null : record.SeverityText,
            Body = OtlpAnyValueFormatter.Format(record.Body),
            ServiceName = serviceName,
            ServiceInstanceId = serviceInstanceId,
            ScopeName = scopeName,
            TraceId = record.TraceId,
            SpanId = record.SpanId,
            EventName = string.IsNullOrEmpty(record.EventName) ? null : record.EventName,
            Attributes = attributes,
            Exception = exception,
            Source = IngestSource.Otlp,
        };
    }

    /// <summary>Maps a severity label to a severity number, for producers that send only text.</summary>
    /// <param name="severityText">The label, in any casing.</param>
    /// <returns>The severity number, or <see cref="Severity.Unspecified"/> when unrecognised.</returns>
    public static int InferSeverity(string? severityText) => severityText?.Trim().ToUpperInvariant() switch
    {
        "TRACE" => Severity.Trace,
        "DEBUG" => Severity.Debug,
        "INFO" or "INFORMATION" => Severity.Info,
        "WARN" or "WARNING" => Severity.Warn,
        "ERROR" => Severity.Error,
        "FATAL" or "CRITICAL" => Severity.Fatal,
        _ => Severity.Unspecified,
    };

    private static (string ServiceName, string? InstanceId) ReadService(IReadOnlyList<OtlpKeyValue> attributes)
    {
        string? name = null;
        string? instance = null;

        foreach (var attribute in attributes)
        {
            if (attribute.Key == ServiceNameKey)
            {
                name = OtlpAnyValueFormatter.Format(attribute.Value);
            }
            else if (attribute.Key == ServiceInstanceKey)
            {
                instance = OtlpAnyValueFormatter.Format(attribute.Value);
            }
        }

        return (
            string.IsNullOrWhiteSpace(name) ? UnknownServiceName : name,
            string.IsNullOrWhiteSpace(instance) ? null : instance);
    }

    /// <summary>
    /// Splits the exception attributes out of the attribute bag, so they land on
    /// <see cref="NebuLogEntry.Exception"/> instead of being repeated as loose attributes.
    /// </summary>
    private static (IReadOnlyDictionary<string, string> Attributes, ExceptionInfo? Exception) SplitAttributes(
        IReadOnlyList<OtlpKeyValue> source)
    {
        var attributes = new Dictionary<string, string>(source.Count, StringComparer.Ordinal);
        string? type = null;
        string? message = null;
        string? stackTrace = null;

        foreach (var attribute in source)
        {
            switch (attribute.Key)
            {
                case ExceptionTypeKey:
                    type = OtlpAnyValueFormatter.Format(attribute.Value);
                    break;
                case ExceptionMessageKey:
                    message = OtlpAnyValueFormatter.Format(attribute.Value);
                    break;
                case ExceptionStackTraceKey:
                    stackTrace = OtlpAnyValueFormatter.Format(attribute.Value);
                    break;
                default:
                    attributes[attribute.Key] = OtlpAnyValueFormatter.Format(attribute.Value);
                    break;
            }
        }

        var exception = type is null && message is null && stackTrace is null
            ? null
            : new ExceptionInfo
            {
                Type = type ?? string.Empty,
                Message = message ?? string.Empty,
                StackTrace = string.IsNullOrEmpty(stackTrace) ? null : stackTrace,
            };

        return (attributes, exception);
    }

    private static long ToUnixMs(ulong unixNano) => (long)(unixNano / 1_000_000);
}
