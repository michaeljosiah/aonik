using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Payments.Entities;

public class Payment : AuditableEntity
{
    public Guid PaymentId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PaymentIntentId { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public string? ProviderReference { get; private set; }
    public DateTime? CapturedAt { get; private set; }
    public string OutcomeStatus { get; private set; } = string.Empty;
    public string OutcomeJson { get; private set; } = string.Empty;

    private Payment() { }

    public Payment(Guid tenantId, Guid paymentIntentId, string provider)
    {
        PaymentId = Id;
        TenantId = tenantId;
        PaymentIntentId = paymentIntentId;
        Provider = provider;
        OutcomeStatus = "Pending";
        OutcomeJson = "{}";
    }

    public void UpdateProviderReference(string providerReference)
    {
        ProviderReference = providerReference;
    }

    public void MarkAsCaptured(string outcomeStatus, string outcomeJson)
    {
        CapturedAt = DateTime.UtcNow;
        OutcomeStatus = outcomeStatus;
        OutcomeJson = outcomeJson;
    }

    public void UpdateOutcome(string outcomeStatus, string outcomeJson)
    {
        OutcomeStatus = outcomeStatus;
        OutcomeJson = outcomeJson;
    }
}
