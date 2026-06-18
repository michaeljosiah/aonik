namespace Aonik.SharedKernel.Abstractions.Payments;

/// <summary>
/// Write-side contract for initiating funding of an order (Spec 042 §12) — the write mirror of the
/// ADR-006 read contracts. Implemented by <c>Aonik.Finance</c> and consumed by modules that must
/// fund an order (e.g. <c>Aonik.Commerce</c> at checkout) without referencing Finance. Creates a
/// guest <c>PaymentIntent</c> via the permission-free public payment path so anonymous storefront
/// checkout works; capture/settlement remain Finance-governed high-tier actions.
/// </summary>
public interface IPaymentInitiator
{
    Task<PaymentIntentRef> CreateGuestIntentForOrderAsync(CreateGuestPaymentIntentForOrderCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Create a guest payment intent that funds <paramref name="Amount"/> (the payable total, after any
/// discount/tax) for the given order through the named payment <paramref name="Provider"/>.
/// </summary>
public sealed record CreateGuestPaymentIntentForOrderCommand(
    Guid OrderId,
    decimal Amount,
    string Currency,
    string Provider,
    string PaymentMethodType,
    string? ReturnUrl = null,
    string? CancelUrl = null);

/// <summary>A reference to the created payment intent, including any client-side completion handles.</summary>
public sealed record PaymentIntentRef(
    Guid PaymentIntentId,
    string Status,
    string? ClientSecret = null,
    string? CheckoutUrl = null);
