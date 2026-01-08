using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Payments.Entities;

public class Chargeback : AuditableEntity
{
    public Guid ChargebackId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PaymentId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public string? ProviderReference { get; private set; }

    private Chargeback() { }

    public Chargeback(Guid tenantId, Guid paymentId, decimal amount, string currency)
    {
        ChargebackId = Id;
        TenantId = tenantId;
        PaymentId = paymentId;
        Amount = amount;
        Currency = currency;
        Status = "Open";
    }

    public void UpdateProviderReference(string providerReference)
    {
        ProviderReference = providerReference;
    }

    public void UpdateStatus(string status)
    {
        Status = status;
    }

    public void Accept()
    {
        Status = "Accepted";
    }

    public void Dispute()
    {
        Status = "Disputed";
    }

    public void Resolve(bool inFavorOfMerchant)
    {
        Status = inFavorOfMerchant ? "Won" : "Lost";
    }
}
