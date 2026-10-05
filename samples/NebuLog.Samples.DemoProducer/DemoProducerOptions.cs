using System.ComponentModel.DataAnnotations;

namespace NebuLog.Samples.DemoProducer;

/// <summary>How much traffic the demo producer generates, from section <c>DemoProducer</c>.</summary>
public sealed class DemoProducerOptions
{
    /// <summary>The configuration section these options are bound from.</summary>
    public const string SectionName = "DemoProducer";

    /// <summary>Lower bound of the idle rate. Kept gentle so the public demo wastes nothing.</summary>
    [Range(1, 10_000)]
    public int MinRatePerSecond { get; set; } = 5;

    /// <summary>Upper bound of the idle rate.</summary>
    [Range(1, 10_000)]
    public int MaxRatePerSecond { get; set; } = 20;

    /// <summary>Rate while the <c>burst</c> command is in effect, for the dashboard's load test.</summary>
    [Range(1, 100_000)]
    public int BurstRatePerSecond { get; set; } = 1_000;

    /// <summary>How long a burst lasts.</summary>
    [Range(1, 600)]
    public int BurstSeconds { get; set; } = 10;
}
