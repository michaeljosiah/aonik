using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Orders;

public class Order : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string OrderType { get; set; } = string.Empty;
    public string? IdempotencyKey { get; set; }
    public Guid? PayerPartyId { get; set; }
    public string? PurposeCode { get; set; }
    public string? OriginCountry { get; set; }
    public string? DestinationCountry { get; set; }
    public decimal AmountIn { get; set; }
    public string CurrencyIn { get; set; } = string.Empty;
    public decimal? AmountOut { get; set; }
    public string? CurrencyOut { get; set; }
    public string FeesJson { get; set; } = "[]";
    public Guid? FxQuoteId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ProvenanceJson { get; set; } = string.Empty;
    public List<OrderItem> Items { get; set; } = new();
    public List<OrderPartyRole> PartyRoles { get; set; } = new();
    public List<OrderHistoryEvent> HistoryEvents { get; set; } = new();
}
