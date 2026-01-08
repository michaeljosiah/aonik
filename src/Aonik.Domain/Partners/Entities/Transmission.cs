using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Partners.Entities;

public class Transmission : AuditableEntity, ITenantScoped
{
    public Guid TransmissionId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PayoutId { get; private set; }
    public Guid ConnectorId { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public int RetryCount { get; private set; }
    public string? LastError { get; private set; }

    private Transmission() { }

    public Transmission(Guid tenantId, Guid payoutId, Guid connectorId, string idempotencyKey)
    {
        TransmissionId = Id;
        TenantId = tenantId;
        PayoutId = payoutId;
        ConnectorId = connectorId;
        IdempotencyKey = idempotencyKey;
        Status = "Pending";
        RetryCount = 0;
    }

    public void UpdateStatus(string status)
    {
        Status = status;
    }

    public void MarkAsSent()
    {
        Status = "Sent";
    }

    public void MarkAsAcknowledged()
    {
        Status = "Acknowledged";
    }

    public void MarkAsFailed(string error)
    {
        Status = "Failed";
        LastError = error;
        RetryCount++;
    }

    public void IncrementRetryCount()
    {
        RetryCount++;
    }
}
