using System.ComponentModel.DataAnnotations;

namespace NebuLog.Server.Otlp;

/// <summary>Settings for the OTLP/HTTP receiver, bound from configuration section <c>NebuLog:Otlp</c>.</summary>
public sealed class NebuLogOtlpOptions
{
    /// <summary>The configuration section these options are bound from.</summary>
    public const string SectionName = "NebuLog:Otlp";

    /// <summary>Largest accepted request body, in bytes. Larger requests get 413.</summary>
    [Range(1024, 128 * 1024 * 1024)]
    public int MaxRequestBodyBytes { get; set; } = 4 * 1024 * 1024;
}
