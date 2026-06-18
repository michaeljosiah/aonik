using Aonik.SharedKernel.Primitives;

namespace Aonik.Commerce.Entities.Promotions;

/// <summary>
/// The durable charge breakdown recorded for a checkout (Spec 042 §5 follow-up): goods subtotal, any
/// discount, tax, and the payable total that the PaymentIntent funds. Soft-linked to the order. The
/// order lines remain the goods (subtotal); discount/tax are payment-side concerns — Order, Payment,
/// and Ledger stay distinct. Anemic.
/// </summary>
public class OrderChargeSummary : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid OrderId { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public string? DiscountCode { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal Total { get; set; }
}
