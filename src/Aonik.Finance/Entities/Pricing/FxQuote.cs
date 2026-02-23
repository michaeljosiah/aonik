using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Pricing;

public class FxQuote : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string BaseCurrency { get; set; } = string.Empty;
    public string TargetCurrency { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string? Provider { get; set; }
    public string MetadataJson { get; set; } = string.Empty;
}
