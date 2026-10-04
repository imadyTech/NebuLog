using System.Globalization;
using System.Text;

namespace NebuLog.Server.Otlp;

/// <summary>Renders an OTLP <c>AnyValue</c> as the string NebuLog stores.</summary>
/// <remarks>
/// Strings pass through unchanged; everything else becomes compact JSON, so a structured attribute
/// stays machine-readable in the dashboard instead of collapsing to a type name.
/// </remarks>
internal static class OtlpAnyValueFormatter
{
    /// <summary>Formats a value, returning the empty string when there is nothing to render.</summary>
    /// <param name="value">The value to format.</param>
    public static string Format(OtlpAnyValue? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (value.StringValue is { } text)
        {
            return text;
        }

        var builder = new StringBuilder();
        Write(builder, value);
        return builder.ToString();
    }

    private static void Write(StringBuilder builder, OtlpAnyValue? value)
    {
        switch (value)
        {
            case null:
                builder.Append("null");
                break;
            case { StringValue: { } text }:
                WriteQuoted(builder, text);
                break;
            case { BoolValue: { } flag }:
                builder.Append(flag ? "true" : "false");
                break;
            case { IntValue: { } number }:
                builder.Append(number.ToString(CultureInfo.InvariantCulture));
                break;
            case { DoubleValue: { } number }:
                builder.Append(number.ToString("R", CultureInfo.InvariantCulture));
                break;
            case { BytesValue: { } bytes }:
                WriteQuoted(builder, Convert.ToBase64String(bytes));
                break;
            case { ArrayValue: { } items }:
                WriteArray(builder, items);
                break;
            case { KvListValue: { } pairs }:
                WriteObject(builder, pairs);
                break;
            default:
                builder.Append("null");
                break;
        }
    }

    private static void WriteArray(StringBuilder builder, IReadOnlyList<OtlpAnyValue> items)
    {
        builder.Append('[');
        for (var i = 0; i < items.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            Write(builder, items[i]);
        }

        builder.Append(']');
    }

    private static void WriteObject(StringBuilder builder, IReadOnlyList<OtlpKeyValue> pairs)
    {
        builder.Append('{');
        for (var i = 0; i < pairs.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            WriteQuoted(builder, pairs[i].Key);
            builder.Append(':');
            Write(builder, pairs[i].Value);
        }

        builder.Append('}');
    }

    private static void WriteQuoted(StringBuilder builder, string text)
    {
        builder.Append('"');
        foreach (var character in text)
        {
            switch (character)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (char.IsControl(character))
                    {
                        builder.Append(CultureInfo.InvariantCulture, $"\\u{(int)character:x4}");
                    }
                    else
                    {
                        builder.Append(character);
                    }

                    break;
            }
        }

        builder.Append('"');
    }
}
