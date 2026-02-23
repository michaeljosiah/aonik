using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Payments;

public class Payment : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid PaymentIntentId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string? ProviderReference { get; set; }
    public DateTime? CapturedAt { get; set; }
    public string OutcomeStatus { get; set; } = string.Empty;
    public string OutcomeJson { get; set; } = string.Empty;
}
