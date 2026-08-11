using Aonik.Finance.Contracts.Services.Payments;
using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Payments;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.Payments;

/// <summary>
/// Spec 088 §6 — funds an order from a stored mandate, with nobody present.
/// </summary>
internal sealed class RecurringPaymentInitiator : IRecurringPaymentInitiator
{
    private readonly FinanceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IEnumerable<IPaymentProviderGateway> _providerGateways;
    private readonly IClock _clock;

    public RecurringPaymentInitiator(
        FinanceDbContext dbContext,
        ITenantProvider tenantProvider,
        IEnumerable<IPaymentProviderGateway> providerGateways,
        IClock clock)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _providerGateways = providerGateways;
        _clock = clock;
    }

    public async Task<PaymentIntentRef> CreateIntentForMandateAsync(
        Guid mandateId,
        Guid orderId,
        decimal amount,
        string currency,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new InvalidStateException(
                "An idempotency key is required. Without one a retried job charges the customer twice.");
        }

        // Idempotency first, before any provider call — a retry must never reach the provider.
        var existing = await _dbContext.PaymentIntents.AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.TenantId == tenantId && p.IdempotencyKey == idempotencyKey,
                cancellationToken);

        if (existing is not null)
            return new PaymentIntentRef(existing.Id, existing.Status);

        var mandate = await _dbContext.PaymentMandates.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == mandateId && m.TenantId == tenantId, cancellationToken);

        // Every unusable case is the SAME distinct, non-retryable failure. A caller must be able to
        // tell "this authorisation is gone, ask the customer again" apart from "the bank said no
        // this time, try later" — retrying the former forever is how a subscription silently dies.
        if (mandate is null)
            throw new MandateUnavailableException(mandateId, "it was not found in this tenant");

        if (mandate.Status == PaymentMandateStatuses.Revoked)
            throw new MandateUnavailableException(mandateId, "it has been revoked");

        if (mandate.Status == PaymentMandateStatuses.Expired)
            throw new MandateUnavailableException(mandateId, "it has expired");

        if (mandate.ExpiresAt is { } expiry && expiry <= _clock.UtcNow)
            throw new MandateUnavailableException(mandateId, $"it expired on {expiry:yyyy-MM-dd}");

        if (mandate.Status != PaymentMandateStatuses.Active)
            throw new MandateUnavailableException(mandateId, $"its status is '{mandate.Status}'");

        var method = await _dbContext.PaymentMethods.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == mandate.PaymentMethodId && m.TenantId == tenantId, cancellationToken)
            ?? throw new MandateUnavailableException(mandateId, "its payment method no longer exists");

        var gateway = _providerGateways.FirstOrDefault(g =>
            string.Equals(g.ProviderCode, mandate.Provider, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidStateException($"Payment provider '{mandate.Provider}' is not configured.");

        // The mandate supplies the provider and instrument the job cannot. Note that this uses the
        // ordinary intent path: true off-session semantics — merchant-initiated flags, suppressing
        // the interactive challenge — are per-provider adapter work for whichever real gateway is
        // wired, and cannot be expressed against the simulated one. See Spec 088 O6.
        var providerResult = await gateway.CreateIntentAsync(
            new PaymentProviderIntentRequest(
                orderId,
                amount,
                currency,
                method.Type,
                ReturnUrl: null,
                CancelUrl: null,
                Reference: $"MND-{mandate.Id:N}"),
            cancellationToken);

        var intent = new PaymentIntent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Amount = amount,
            Currency = currency,
            PayerPartyId = mandate.PartyId,
            OrderId = orderId,
            PurposeType = "Order",
            PurposeId = orderId,
            PaymentMethodType = method.Type,
            PaymentMethodRef = providerResult.ProviderReference,
            IdempotencyKey = idempotencyKey,
            Status = providerResult.Status
        };

        _dbContext.PaymentIntents.Add(intent);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new PaymentIntentRef(intent.Id, intent.Status);
    }
}
