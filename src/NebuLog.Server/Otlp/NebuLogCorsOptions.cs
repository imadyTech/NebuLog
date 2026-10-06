namespace NebuLog.Server.Otlp;

/// <summary>
/// Cross-origin settings for the OTLP endpoint, bound from configuration section <c>NebuLog:Cors</c>.
/// </summary>
/// <remarks>
/// Only browser-based OpenTelemetry SDKs need this. With no origins configured the policy allows
/// nothing, so a stray browser cannot post logs from an unexpected page.
/// </remarks>
public sealed class NebuLogCorsOptions
{
    /// <summary>The configuration section these options are bound from.</summary>
    public const string SectionName = "NebuLog:Cors";

    /// <summary>The CORS policy name applied to <c>POST /v1/logs</c>.</summary>
    public const string PolicyName = "otlp-browser";

    /// <summary>Comma-separated list of origins permitted to post OTLP from a browser.</summary>
    public string AllowedOrigins { get; set; } = string.Empty;

    /// <summary>Splits <see cref="AllowedOrigins"/> into individual origins.</summary>
    public string[] ParseOrigins() =>
        AllowedOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
