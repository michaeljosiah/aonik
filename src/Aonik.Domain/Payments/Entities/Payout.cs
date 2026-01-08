using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Payments.Entities;

public class Payout : AuditableEntity, ITenantScoped
{
    public Guid PayoutId { get; private set; }
    public Guid TenantId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public Guid? DestinationExternalAccountId { get; private set; }
    public Guid? PartnerId { get; private set; }
    public string Status { get; private set; } = string.Empty;

    private Payout() { }

    public Payout(Guid tenantId, decimal amount, string currency, Guid? destinationExternalAccountId = null, Guid? partnerId = null)
    {
        PayoutId = Id;
        TenantId = tenantId;
        Amount = amount;
        Currency = currency;
        DestinationExternalAccountId = destinationExternalAccountId;
        PartnerId = partnerId;
        Status = "Pending";
    }

    public void UpdateStatus(string status)
    {
        Status = status;
    }

    public void MarkAsCompleted()
    {
        Status = "Completed";
    }

    public void MarkAsFailed()
    {
        Status = "Failed";
    }
}
