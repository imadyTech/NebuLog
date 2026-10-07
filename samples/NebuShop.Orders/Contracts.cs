using System.ComponentModel.DataAnnotations;

namespace NebuShop.Orders;

/// <summary>An order as the shop returns it.</summary>
/// <param name="Id">Order identifier.</param>
/// <param name="Sku">What was ordered.</param>
/// <param name="Name">Display name of the product.</param>
/// <param name="Quantity">How many.</param>
/// <param name="TotalNzd">Line total in New Zealand dollars.</param>
public sealed record OrderView(int Id, string Sku, string Name, int Quantity, decimal TotalNzd);

/// <summary>A request to place an order.</summary>
public sealed class PlaceOrderRequest
{
    /// <summary>Stock code of the product.</summary>
    [Required]
    public string Sku { get; set; } = string.Empty;

    /// <summary>
    /// How many to order. Validated by hand rather than by attribute so that both pipelines
    /// produce the same warning for the same reason; see <see cref="OrderService"/>.
    /// </summary>
    public int Quantity { get; set; }
}

/// <summary>One line of a basket being checked out.</summary>
public sealed class CheckoutLine
{
    /// <summary>Stock code of the product.</summary>
    [Required]
    public string Sku { get; set; } = string.Empty;

    /// <summary>How many.</summary>
    public int Quantity { get; set; } = 1;
}

/// <summary>A request to check out a basket.</summary>
public sealed class CheckoutRequest
{
    /// <summary>The basket's lines.</summary>
    public IList<CheckoutLine> Lines { get; init; } = [];
}

/// <summary>The result of a checkout.</summary>
/// <param name="OrderId">The order that was created.</param>
/// <param name="TotalNzd">What the basket came to.</param>
/// <param name="AuthorisationCode">The payment service's reference.</param>
/// <param name="PaymentMs">How long the payment service took.</param>
public sealed record CheckoutResult(int OrderId, decimal TotalNzd, string AuthorisationCode, int PaymentMs);

/// <summary>A sign-in request, used to demonstrate what redaction does and does not cover.</summary>
public sealed class SignInRequest
{
    /// <summary>The account name.</summary>
    [Required]
    public string Username { get; set; } = string.Empty;

    /// <summary>The password. It must never reach the log as readable text.</summary>
    [Required]
    public string Password { get; set; } = string.Empty;
}

/// <summary>The outcome of a burst request.</summary>
/// <param name="EntriesPerSecond">Rate the burst runs at.</param>
/// <param name="Seconds">How long it runs for.</param>
public sealed record BurstAccepted(int EntriesPerSecond, int Seconds);
