using Aonik.Finance.Contracts.Models.Payments;
using Aonik.Finance.Contracts.Services.Payments;
using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.Payments;

/// <summary>
/// Spec 088 §6 — recording and withdrawing a customer's standing authorisation.
///
/// Every transition is audit-logged. A standing permission to take someone's money is exactly the
/// kind of thing that must be answerable after the fact: who granted it, when, and who took it
/// away.
/// </summary>
internal sealed class PaymentMandateService : IPaymentMandateService
{
    private const string ResourceType = "PaymentMandate";

    private readonly FinanceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IAuditLogWriter _auditLog;
    private readonly IClock _clock;

    public PaymentMandateService(
        FinanceDbContext dbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        IAuditLogWriter auditLog,
        IClock clock)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _auditLog = auditLog;
        _clock = clock;
    }

    public async Task<PaymentMandateResponse> CreateAsync(
        CreatePaymentMandateRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        // The interactivity rule, enforced rather than documented. A background job has no current
        // user, so this is what actually stops a mandate being minted for an authorisation the
        // customer never gave.
        if (!_currentUserProvider.TryGetCurrentUserId(out var actorId) || actorId == Guid.Empty)
        {
            throw new InvalidStateException(
                "A payment mandate records a customer's authorisation and can only be created by an "
                + "interactive caller. There is no current user on this request.");
        }

        var method = await _dbContext.PaymentMethods
            .FirstOrDefaultAsync(m => m.Id == request.PaymentMethodId && m.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException($"Payment method '{request.PaymentMethodId}' was not found.");

        // The mandate authorises charging THIS party's instrument. Letting them diverge would mean
        // one customer's consent charging another's card.
        if (method.CustomerPartyId != request.PartyId)
        {
            throw new InvalidStateException(
                "The payment method belongs to a different party than the one authorising the mandate.");
        }

        var existing = await FindActiveAsync(tenantId, request.PartyId, cancellationToken);
        if (existing is not null)
        {
            // Re-authorising supersedes rather than accumulates, so "the party's mandate" stays a
            // single answer and a stale instrument cannot be charged after the customer moved on.
            await RevokeInternalAsync(existing, "Superseded by a new authorisation", actorId, cancellationToken);
        }

        var mandate = new PaymentMandate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PartyId = request.PartyId,
            Provider = method.Provider,
            PaymentMethodId = method.Id,
            ProviderMandateRef = request.ProviderMandateRef,
            Status = PaymentMandateStatuses.Active,
            AuthorisedAt = _clock.UtcNow,
            ExpiresAt = request.ExpiresAt ?? DeriveExpiryFromCard(method)
        };

        _dbContext.PaymentMandates.Add(mandate);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            "PaymentMandate.Created", ResourceType, mandate.Id, tenantId, actorId, correlationId: null,
            detailsJson: $"{{\"partyId\":\"{mandate.PartyId}\",\"provider\":\"{mandate.Provider}\"}}",
            cancellationToken);

        return Map(mandate);
    }

    public async Task<PaymentMandateResponse> RevokeAsync(
        Guid mandateId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        _currentUserProvider.TryGetCurrentUserId(out var actorId);

        var mandate = await _dbContext.PaymentMandates
            .FirstOrDefaultAsync(m => m.Id == mandateId && m.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException($"Payment mandate '{mandateId}' was not found.");

        // Idempotent: the customer wanted it gone, and it is gone.
        if (mandate.Status == PaymentMandateStatuses.Active)
            await RevokeInternalAsync(mandate, reason, actorId, cancellationToken);

        return Map(mandate);
    }

    public async Task<PaymentMandateResponse?> GetActiveForPartyAsync(
        Guid partyId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var mandate = await FindActiveAsync(tenantId, partyId, cancellationToken);
        return mandate is null ? null : Map(mandate);
    }

    public async Task<PaymentMandateResponse?> GetAsync(Guid mandateId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var mandate = await _dbContext.PaymentMandates.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == mandateId && m.TenantId == tenantId, cancellationToken);

        return mandate is null ? null : Map(mandate);
    }

    public async Task<int> RevokeForPaymentMethodAsync(
        Guid paymentMethodId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        _currentUserProvider.TryGetCurrentUserId(out var actorId);

        var mandates = await _dbContext.PaymentMandates
            .Where(m => m.TenantId == tenantId
                        && m.PaymentMethodId == paymentMethodId
                        && m.Status == PaymentMandateStatuses.Active)
            .ToListAsync(cancellationToken);

        foreach (var mandate in mandates)
            await RevokeInternalAsync(mandate, reason, actorId, cancellationToken);

        return mandates.Count;
    }

    private async Task RevokeInternalAsync(
        PaymentMandate mandate,
        string reason,
        Guid? actorId,
        CancellationToken cancellationToken)
    {
        mandate.Status = PaymentMandateStatuses.Revoked;
        mandate.RevokedAt = _clock.UtcNow;
        mandate.RevocationReason = reason;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            "PaymentMandate.Revoked", ResourceType, mandate.Id, mandate.TenantId, actorId, correlationId: null,
            detailsJson: $"{{\"reason\":{System.Text.Json.JsonSerializer.Serialize(reason)}}}",
            cancellationToken);
    }

    private async Task<PaymentMandate?> FindActiveAsync(Guid tenantId, Guid partyId, CancellationToken cancellationToken)
    {
        var mandate = await _dbContext.PaymentMandates
            .Where(m => m.TenantId == tenantId
                        && m.PartyId == partyId
                        && m.Status == PaymentMandateStatuses.Active)
            .OrderByDescending(m => m.AuthorisedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (mandate is null)
            return null;

        // Expiry is a fact about time, not a state someone remembered to write. Checking on read
        // means a lapsed mandate can never be handed out as chargeable, even if no sweep has run.
        if (mandate.ExpiresAt is { } expiry && expiry <= _clock.UtcNow)
        {
            mandate.Status = PaymentMandateStatuses.Expired;
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _auditLog.LogAsync(
                "PaymentMandate.Expired", ResourceType, mandate.Id, tenantId, actorId: null, correlationId: null,
                detailsJson: $"{{\"expiresAt\":\"{expiry:O}\"}}", cancellationToken);

            return null;
        }

        return mandate;
    }

    /// <summary>
    /// A card mandate cannot outlive the card. Expiry is end-of-month, matching how card expiry
    /// dates are actually read.
    /// </summary>
    private static DateTime? DeriveExpiryFromCard(PaymentMethod method)
    {
        if (method.ExpiryYear is not { } year || method.ExpiryMonth is not { } month)
            return null;

        if (month is < 1 or > 12)
            return null;

        return new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
    }

    private static PaymentMandateResponse Map(PaymentMandate m)
        => new(m.Id, m.PartyId, m.Provider, m.PaymentMethodId, m.ProviderMandateRef, m.Status,
            m.AuthorisedAt, m.ExpiresAt, m.RevokedAt, m.RevocationReason);
}
