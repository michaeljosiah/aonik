namespace Aonik.Domain.Payments;

public enum PaymentStatus
{
    Pending,
    Authorized,
    Captured,
    Failed,
    Cancelled
}
