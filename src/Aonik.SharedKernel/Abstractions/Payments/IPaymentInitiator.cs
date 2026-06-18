namespace Aonik.SharedKernel.Abstractions.Payments;

/// <summary>
/// Write-side contract for initiating funding of an order (Spec 042 §12) — the write mirror of the
/// ADR-006 read contracts. Implemented by <c>Aonik.Finance</c> and consumed by modules that must
/// fund an order (e.g. <c>Aonik.Commerce</c> at checkout) without referencing Finance. Creates a
/// <c>PaymentIntent</c> only — capture/settlement remain Finance-governed high-tier actions.
/// </summary>
public interface IPaymentInitiator
{
    Task<PaymentIntentRef> CreateIntentForOrderAsync(CreatePaymentIntentForOrderCommand command, CancellationToken cancellationToken = default);
}

/// <summary>Create a (draft) payment intent that funds the given order.</summary>
public sealed record CreatePaymentIntentForOrderCommand(
    Guid OrderId,
    decimal Amount,
    string Currency,
    Guid? InvoiceId = null,
    string? Reference = null,
    string? PaymentMethodType = null);

/// <summary>A lightweight reference to the created payment intent.</summary>
public sealed record PaymentIntentRef(Guid PaymentIntentId, string Status);
