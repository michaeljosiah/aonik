using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Payments.Entities;

public class PaymentIntent : Entity
{
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public PaymentStatus Status { get; private set; }
    public string? Reference { get; private set; }
    public DateTime CreatedUtc { get; private set; }

    private PaymentIntent() { }

    public PaymentIntent(decimal amount, string currency, string? reference = null)
    {
        Amount = amount;
        Currency = currency;
        Status = PaymentStatus.Pending;
        Reference = reference;
        CreatedUtc = DateTime.UtcNow;
    }

    public void Authorize()
    {
        if (Status != PaymentStatus.Pending)
            throw new InvalidOperationException("Only pending payments can be authorized");

        Status = PaymentStatus.Authorized;
    }

    public void Capture()
    {
        if (Status != PaymentStatus.Authorized)
            throw new InvalidOperationException("Only authorized payments can be captured");

        Status = PaymentStatus.Captured;
    }

    public void Fail()
    {
        if (Status == PaymentStatus.Captured)
            throw new InvalidOperationException("Captured payments cannot be marked as failed");

        Status = PaymentStatus.Failed;
    }

    public void Cancel()
    {
        if (Status == PaymentStatus.Captured)
            throw new InvalidOperationException("Captured payments cannot be cancelled");

        Status = PaymentStatus.Cancelled;
    }
}
