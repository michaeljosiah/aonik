using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Payments.Entities;

public class PaymentIntent : AuditableEntity, ITenantScoped
{
    public Guid PaymentIntentId { get; private set; }
    public Guid TenantId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public Guid PayerPartyId { get; private set; }
    public Guid? PayeePartyId { get; private set; }
    public string PurposeType { get; private set; } = string.Empty;
    public Guid PurposeId { get; private set; }
    public string PaymentMethodType { get; private set; } = string.Empty;
    public string? PaymentMethodRef { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public string? FailureReason { get; private set; }

    private PaymentIntent() { }

    public PaymentIntent(Guid tenantId, decimal amount, string currency, Guid payerPartyId, string purposeType, Guid purposeId, string paymentMethodType, Guid? payeePartyId = null)
    {
        PaymentIntentId = Id;
        TenantId = tenantId;
        Amount = amount;
        Currency = currency;
        PayerPartyId = payerPartyId;
        PayeePartyId = payeePartyId;
        PurposeType = purposeType;
        PurposeId = purposeId;
        PaymentMethodType = paymentMethodType;
        Status = "Pending";
    }

    public void UpdatePaymentMethodRef(string paymentMethodRef)
    {
        PaymentMethodRef = paymentMethodRef;
    }

    public void Authorize()
    {
        if (Status != "Pending")
            throw new InvalidOperationException("Only pending payment intents can be authorized");

        Status = "Authorized";
    }

    public void Capture()
    {
        if (Status != "Authorized")
            throw new InvalidOperationException("Only authorized payment intents can be captured");

        Status = "Captured";
    }

    public void Fail(string failureReason)
    {
        if (Status == "Captured")
            throw new InvalidOperationException("Captured payment intents cannot be marked as failed");

        Status = "Failed";
        FailureReason = failureReason;
    }

    public void Cancel()
    {
        if (Status == "Captured")
            throw new InvalidOperationException("Captured payment intents cannot be cancelled");

        Status = "Cancelled";
    }
}
