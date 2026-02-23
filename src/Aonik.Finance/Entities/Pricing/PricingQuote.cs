using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Pricing;

public class PricingQuote : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string QuoteType { get; set; } = string.Empty;
    public string OriginCurrency { get; set; } = string.Empty;
    public string DestinationCurrency { get; set; } = string.Empty;
    public string OriginCountry { get; set; } = string.Empty;
    public string DestinationCountry { get; set; } = string.Empty;
    public string ServiceCode { get; set; } = string.Empty;
    public decimal OriginAmount { get; set; }
    public decimal DestinationAmount { get; set; }
    public decimal ExchangeRate { get; set; }
    public decimal RateMarkup { get; set; }
    public decimal FeesTotal { get; set; }
    public decimal TotalAmount { get; set; }
    public Guid FxRateId { get; set; }
    public DateTime RateTimestamp { get; set; }
    public string? FxRateProvider { get; set; }
    public Guid PricingPolicyId { get; set; }
    public string PricingPolicyVersion { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string FeeBreakdownJson { get; set; } = "[]";
    public Guid? CustomerId { get; set; }
    public string? CustomerTier { get; set; }
    public string? QuoteContext { get; set; }
}
