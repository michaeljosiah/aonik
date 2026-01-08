using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Payments.Entities;

public class Refund : AuditableEntity
{
    public Guid RefundId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PaymentId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public string? Reason { get; private set; }

    private Refund() { }

    public Refund(Guid tenantId, Guid paymentId, decimal amount, string currency, string? reason = null)
    {
        RefundId = Id;
        TenantId = tenantId;
        PaymentId = paymentId;
        Amount = amount;
        Currency = currency;
        Status = "Pending";
        Reason = reason;
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
