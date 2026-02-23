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
}
