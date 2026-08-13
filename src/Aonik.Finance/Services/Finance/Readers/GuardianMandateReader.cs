using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Consent;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.Finance.Readers;

/// <summary>
/// Finance-side implementation of <see cref="IGuardianMandateReader"/> (Spec 095 §8).
///
/// <para>
/// The consent path needs to know one thing — does this adult hold a live standing authorisation —
/// and this reader is deliberately the narrowest surface that answers it. No payment method, no
/// vault token, no amount: none of that is the consent path's business, and exposing it through a
/// SharedKernel contract would make it everyone's.
/// </para>
/// </summary>
internal sealed class GuardianMandateReader : IGuardianMandateReader
{
    private readonly FinanceDbContext _dbContext;
    private readonly IClock _clock;

    public GuardianMandateReader(FinanceDbContext dbContext, IClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public async Task<GuardianMandateInfo?> GetActiveMandateAsync(
        Guid tenantId,
        Guid partyId,
        CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;

        // Expiry is checked here as well as status, because PaymentMandate treats a passed ExpiresAt
        // "exactly as revoked" and nothing guarantees a job has swept the status by the time we ask.
        // A guardian verified against a lapsed authorisation would be verified against nothing.
        var mandate = await _dbContext.PaymentMandates
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId
                && m.PartyId == partyId
                && m.Status == PaymentMandateStatuses.Active
                && m.RevokedAt == null
                && (m.ExpiresAt == null || m.ExpiresAt > now))
            .OrderByDescending(m => m.AuthorisedAt)
            .Select(m => new { m.Id, m.AuthorisedAt, m.Provider })
            .FirstOrDefaultAsync(cancellationToken);

        return mandate is null
            ? null
            : new GuardianMandateInfo(mandate.Id, mandate.AuthorisedAt, mandate.Provider);
    }
}
