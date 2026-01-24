using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Orders.Entities;

public class Order : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string OrderType { get; set; } = string.Empty;
    public string ServiceCode { get; set; } = string.Empty;
    public decimal AmountIn { get; set; }
    public string CurrencyIn { get; set; } = string.Empty;
    public decimal? AmountOut { get; set; }
    public string? CurrencyOut { get; set; }
    public string FeesJson { get; set; } = string.Empty;
    public Guid? FxQuoteId { get; set; }
    public Guid? InvoiceId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ProvenanceJson { get; set; } = string.Empty;
    public string OrderDetailsJson { get; set; } = string.Empty;
    public string MetadataJson { get; set; } = string.Empty;
    public List<OrderPartyRole> PartyRoles { get; set; } = new();
    public List<OrderHistoryEvent> HistoryEvents { get; set; } = new();
    public List<OrderItem> Items { get; set; } = new();
}
