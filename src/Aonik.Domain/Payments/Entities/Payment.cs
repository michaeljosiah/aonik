using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Payments.Entities;

public class Payment : AuditableEntity, ITenantScoped
{
    public Guid PaymentId { get; set; }
    public Guid TenantId { get; set; }
    public Guid PaymentIntentId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string? ProviderReference { get; set; }
    public DateTime? CapturedAt { get; set; }
    public string OutcomeStatus { get; set; } = string.Empty;
    public string OutcomeJson { get; set; } = string.Empty;
}
