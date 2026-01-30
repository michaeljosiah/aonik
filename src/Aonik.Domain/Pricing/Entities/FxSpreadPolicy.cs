using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Pricing.Entities;

public class FxSpreadPolicy : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string BaseCurrency { get; set; } = string.Empty;
    public string TargetCurrency { get; set; } = string.Empty;
    public string CustomerTier { get; set; } = string.Empty;
    public decimal MarkupBps { get; set; }
    public decimal MinSpreadPercent { get; set; }
    public decimal MaxSpreadPercent { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string Status { get; set; } = string.Empty;
}
