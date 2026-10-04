using OpenTelemetry;
using OpenTelemetry.Logs;

namespace NebuLog.OpenTelemetry;

/// <summary>
/// Replaces the values of log attributes whose key looks sensitive with <c>***</c>.
/// </summary>
/// <remarks>
/// Only attribute values are redacted. A message template parameter such as
/// <c>logger.LogInformation("token {Token}", token)</c> is redacted, but text that the application
/// already formatted into the message body — <c>logger.LogInformation($"token {token}")</c> — is not,
/// because by then the value is indistinguishable from the rest of the sentence.
/// Register this processor before the exporter so that redaction runs first.
/// </remarks>
public sealed class RedactionProcessor : BaseProcessor<LogRecord>
{
    /// <summary>The placeholder written in place of a sensitive value.</summary>
    public const string Placeholder = "***";

    /// <summary>
    /// The key fragments treated as sensitive when no custom patterns are supplied.
    /// Matching is case-insensitive and by substring.
    /// </summary>
    public static readonly IReadOnlyList<string> DefaultPatterns =
    [
        "password",
        "passwd",
        "secret",
        "token",
        "authorization",
        "apikey",
        "api_key",
        "cookie",
    ];

    private readonly string[] _patterns;

    /// <summary>Creates a processor that uses <see cref="DefaultPatterns"/>.</summary>
    public RedactionProcessor()
        : this(DefaultPatterns)
    {
    }

    /// <summary>Creates a processor that uses the supplied key patterns.</summary>
    /// <param name="patterns">Case-insensitive key fragments to treat as sensitive.</param>
    public RedactionProcessor(IReadOnlyList<string> patterns)
    {
        ArgumentNullException.ThrowIfNull(patterns);

        _patterns = [.. patterns];
    }

    /// <inheritdoc />
    public override void OnEnd(LogRecord data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (data.Attributes is not { Count: > 0 } attributes)
        {
            return;
        }

        List<KeyValuePair<string, object?>>? redacted = null;
        for (var i = 0; i < attributes.Count; i++)
        {
            if (!IsSensitive(attributes[i].Key))
            {
                continue;
            }

            redacted ??= [.. attributes];
            redacted[i] = new KeyValuePair<string, object?>(attributes[i].Key, Placeholder);
        }

        if (redacted is not null)
        {
            data.Attributes = redacted;
        }
    }

    private bool IsSensitive(string key)
    {
        foreach (var pattern in _patterns)
        {
            if (key.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
