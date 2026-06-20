using Aonik.Finance.Contracts.Models.Payments;

namespace Aonik.Finance.Contracts.Services.Payments;

/// <summary>
/// The customer card vault (Spec 007). Token-only: persists a gateway vault token plus masked
/// display metadata, never PCI data. Every operation is scoped to the authenticated customer's
/// party (resolved server-side) and the current tenant.
/// </summary>
public interface IPaymentMethodService
{
    /// <summary>Starts a provider setup intent; returns the client secret + accepted method types.</summary>
    Task<SetupIntentResponse> CreateSetupIntentAsync(CancellationToken cancellationToken = default);

    /// <summary>All of the current customer's saved methods (masked).</summary>
    Task<IReadOnlyList<PaymentMethodResponse>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>A single owned method, or null when it does not exist for this customer.</summary>
    Task<PaymentMethodResponse?> GetAsync(Guid paymentMethodId, CancellationToken cancellationToken = default);

    /// <summary>Links an already-tokenised instrument to the customer. Idempotent on (provider, token).</summary>
    Task<PaymentMethodResponse> SaveAsync(SavePaymentMethodRequest request, CancellationToken cancellationToken = default);

    /// <summary>Removes an owned method. Returns false when it does not exist for this customer.</summary>
    Task<bool> DeleteAsync(Guid paymentMethodId, CancellationToken cancellationToken = default);

    /// <summary>Saved methods whose vaulting provider is still an available gateway.</summary>
    Task<IReadOnlyList<PaymentMethodResponse>> ListActiveAsync(CancellationToken cancellationToken = default);
}
