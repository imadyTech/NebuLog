using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NebuLog.Server.Otlp.Json;

/// <summary>
/// Reads a 64-bit unsigned value that OTLP/JSON may encode either as a JSON number or, to survive
/// JavaScript's 53-bit integers, as a decimal string.
/// </summary>
internal sealed class OtlpJsonUInt64Converter : JsonConverter<ulong>
{
    public override ulong Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetUInt64(),
            JsonTokenType.String => ulong.TryParse(
                reader.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : throw new JsonException("Expected a decimal 64-bit unsigned integer."),
            JsonTokenType.Null => 0UL,
            _ => throw new JsonException($"Unexpected token {reader.TokenType} for a 64-bit unsigned integer."),
        };

    public override void Write(Utf8JsonWriter writer, ulong value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
    }
}

/// <summary>Reads an optional 64-bit signed value encoded as a JSON number or a decimal string.</summary>
internal sealed class OtlpJsonNullableInt64Converter : JsonConverter<long?>
{
    public override long? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetInt64(),
            JsonTokenType.String => long.TryParse(
                reader.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : throw new JsonException("Expected a decimal 64-bit signed integer."),
            JsonTokenType.Null => null,
            _ => throw new JsonException($"Unexpected token {reader.TokenType} for a 64-bit signed integer."),
        };

    public override void Write(Utf8JsonWriter writer, long? value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(value.Value.ToString(CultureInfo.InvariantCulture));
        }
    }
}

/// <summary>
/// Reads an OTLP severity number, which may be a plain integer or the enum name
/// (<c>SEVERITY_NUMBER_INFO</c>, <c>SEVERITY_NUMBER_ERROR2</c>, …).
/// </summary>
internal sealed class OtlpJsonSeverityNumberConverter : JsonConverter<int>
{
    private const string Prefix = "SEVERITY_NUMBER_";

    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetInt32(),
            JsonTokenType.Null => 0,
            JsonTokenType.String => FromName(reader.GetString()),
            _ => throw new JsonException($"Unexpected token {reader.TokenType} for a severity number."),
        };

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteNumberValue(value);
    }

    private static int FromName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return 0;
        }

        if (int.TryParse(name, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
        {
            return numeric;
        }

        var text = name.Trim().ToUpperInvariant();
        if (!text.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return 0;
        }

        // Names are the band plus an optional 1-4 suffix, e.g. SEVERITY_NUMBER_ERROR2 is 18.
        var remainder = text[Prefix.Length..];
        var offset = 0;
        if (remainder.Length > 0 && char.IsAsciiDigit(remainder[^1]))
        {
            offset = remainder[^1] - '1';
            remainder = remainder[..^1];
        }

        var band = remainder switch
        {
            "TRACE" => Contracts.Severity.Trace,
            "DEBUG" => Contracts.Severity.Debug,
            "INFO" => Contracts.Severity.Info,
            "WARN" => Contracts.Severity.Warn,
            "ERROR" => Contracts.Severity.Error,
            "FATAL" => Contracts.Severity.Fatal,
            _ => Contracts.Severity.Unspecified,
        };

        return band == Contracts.Severity.Unspecified ? 0 : band + offset;
    }
}
