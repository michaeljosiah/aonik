using Aonik.SharedKernel.Primitives;

namespace Aonik.Commerce.Entities.Cart;

/// <summary>
/// A shopping cart — the unit of checkout (Spec 042 §11). May belong to a known party or be an
/// anonymous guest cart keyed by <see cref="AnonymousToken"/>. On checkout the resulting order id is
/// recorded so the payment-completed handler can find the cart to commit inventory and close it.
/// Anemic.
/// </summary>
public class Cart : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid? BuyerPartyId { get; set; }
    public string? AnonymousToken { get; set; }
    public string Status { get; set; } = CartStatuses.Open;
    public string Currency { get; set; } = string.Empty;

    /// <summary>Set at checkout — the ProductPurchase order this cart produced.</summary>
    public Guid? OrderId { get; set; }

    public List<CartItem> Items { get; set; } = new();
}

/// <summary>Known values for <see cref="Cart.Status"/>.</summary>
public static class CartStatuses
{
    public const string Open = "Open";
    public const string CheckedOut = "CheckedOut";
    public const string Abandoned = "Abandoned";
}
