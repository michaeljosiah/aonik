using Aonik.SharedKernel.Abstractions.Payments;

namespace Aonik.Finance.Contracts.Services.Payments;

/// <summary>Machine-only provider reconciliation; not an HTTP input or an agent mutation tool.</summary>
public interface ICheckoutPaymentReconciler
{
    /// <summary>Completes a fully gift-funded checkout with real internal funding evidence and no provider call.</summary>
    Task<PaymentIntentStateRef> CompleteGiftOnlyAsync(Guid paymentIntentId, CancellationToken cancellationToken = default);

    Task<PaymentIntentStateRef> ApplyAsync(Guid paymentIntentId, PaymentProviderCheckoutSnapshot snapshot,
        Guid? webhookEventId = null, CancellationToken cancellationToken = default);

    Task<PaymentIntentStateRef> ReconcileAsync(Guid paymentIntentId, bool expire = false,
        Guid? webhookEventId = null, string? candidateSessionId = null, CancellationToken cancellationToken = default);
}
