using System.ComponentModel.DataAnnotations;

namespace NebuShop.Payments;

/// <summary>Simulated gateway behaviour, bound from configuration section <c>NebuShop</c>.</summary>
public sealed class PaymentOptions
{
    /// <summary>The configuration section these options are bound from.</summary>
    public const string SectionName = "NebuShop";

    /// <summary>Lower bound of the simulated authorisation latency, in milliseconds.</summary>
    [Range(0, 60_000)]
    public int MinLatencyMs { get; set; } = 300;

    /// <summary>Upper bound of the simulated authorisation latency, in milliseconds.</summary>
    [Range(0, 60_000)]
    public int MaxLatencyMs { get; set; } = 900;

    /// <summary>Latency above which the authorisation is logged as slow, in milliseconds.</summary>
    [Range(0, 60_000)]
    public int SlowThresholdMs { get; set; } = 500;
}
