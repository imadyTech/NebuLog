namespace NebuShop.Orders;

/// <summary>One item the shop sells.</summary>
/// <param name="Sku">Stock code, shown in the console so a log line can be tied to a product.</param>
/// <param name="Name">Display name.</param>
/// <param name="PriceNzd">Price in New Zealand dollars.</param>
public sealed record Product(string Sku, string Name, decimal PriceNzd);

/// <summary>
/// The shop's three products. Invented names: nothing here should resemble a real brand.
/// </summary>
public static class Catalog
{
    /// <summary>Everything the shop sells.</summary>
    public static readonly IReadOnlyList<Product> Products =
    [
        new("MER-0101", "Merino throw", 189.00m),
        new("RIM-0207", "Rimu board", 94.50m),
        new("FLX-0312", "Flax basket", 62.00m),
    ];

    /// <summary>Finds a product by stock code, or null when there is no such code.</summary>
    /// <param name="sku">The stock code to look for.</param>
    /// <returns>The product, or <see langword="null"/>.</returns>
    public static Product? Find(string? sku) =>
        Products.FirstOrDefault(product =>
            string.Equals(product.Sku, sku, StringComparison.OrdinalIgnoreCase));
}
