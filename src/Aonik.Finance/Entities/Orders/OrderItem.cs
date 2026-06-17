using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Orders;

public class OrderItem : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid OrderId { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public int ItemIndex { get; set; }
    public string DetailsJson { get; set; } = "{}";
    public string Status { get; set; } = string.Empty;
    public Guid? ReceiverPartyId { get; set; }
    public decimal AmountIn { get; set; }
    public string CurrencyIn { get; set; } = string.Empty;
    public decimal AmountOut { get; set; }
    public string CurrencyOut { get; set; } = string.Empty;
    public decimal FeesTotal { get; set; }
    public Guid? PricingQuoteId { get; set; }

    // Spec 041 / ADR-011 - retail line shape, populated only for ProductPurchase order items.
    // Nullable so financial-service lines (bill payment, remittance, ...) are unaffected.
    // The line total is carried by the existing AmountIn (Quantity * UnitPrice); no separate
    // LineTotal column. ProductId is a soft reference (no FK), mirroring Order.PayerPartyId ->
    // Party: the Product table lives in the future Aonik.Commerce module.
    public decimal? Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public Guid? ProductId { get; set; }
    public string? Sku { get; set; }
}
