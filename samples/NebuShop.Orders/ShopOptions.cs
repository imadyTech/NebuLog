using System.ComponentModel.DataAnnotations;

namespace NebuShop.Orders;

/// <summary>Shop settings, bound from configuration section <c>NebuShop</c>.</summary>
public sealed class ShopOptions
{
    /// <summary>The configuration section these options are bound from.</summary>
    public const string SectionName = "NebuShop";

    /// <summary>Base address of the payments service.</summary>
    [Required]
    public string PaymentsUrl { get; set; } = "http://localhost:5082";

    /// <summary>Entries per second produced while a burst is running.</summary>
    [Range(1, 100_000)]
    public int BurstEntriesPerSecond { get; set; } = 1000;

    /// <summary>How long a burst runs, in seconds.</summary>
    [Range(1, 300)]
    public int BurstSeconds { get; set; } = 10;
}
