using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Consent;
using Aonik.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Platform.Services.Consent;

/// <summary>
/// Spec 095 §12.1. Fails closed: an absent, expired or revoked grant is a refusal.
///
/// <para>
/// Deliberately version-agnostic — it asks about subject and purpose only. Keeping the terms version
/// out of the question is what lets a material change invalidate consent centrally rather than
/// requiring every caller to know which version is current. The single-active-grant index is what
/// makes that safe.
/// </para>
/// </summary>
internal sealed class ConsentReader : IConsentReader
{
    private readonly PlatformDbContext _dbContext;
    private readonly IClock _clock;

    public ConsentReader(PlatformDbContext dbContext, IClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public async Task<bool> HasConsentAsync(
        Guid tenantId, Guid subjectPartyId, string purpose, CancellationToken cancellationToken = default)
    {
        if (subjectPartyId == Guid.Empty || string.IsNullOrWhiteSpace(purpose))
        {
            return false;
        }

        return await ActiveGrants(tenantId, subjectPartyId)
            .AnyAsync(g => g.Purpose == purpose, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetGrantedPurposesAsync(
        Guid tenantId, Guid subjectPartyId, CancellationToken cancellationToken = default)
        => await ActiveGrants(tenantId, subjectPartyId)
            .Select(g => g.Purpose)
            .Distinct()
            .ToListAsync(cancellationToken);

    private IQueryable<Entities.Party.ConsentGrant> ActiveGrants(Guid tenantId, Guid subjectPartyId)
    {
        var now = _clock.UtcNow;

        // AnkLegacyConsents is NOT consulted here, and that is the point of it being a separate
        // table: consent obtained before any verification existed authorises nothing.
        return _dbContext.ConsentGrants
            .AsNoTracking()
            .Where(g => g.TenantId == tenantId
                && g.SubjectPartyId == subjectPartyId
                && g.RevokedAt == null
                && (g.ExpiresAt == null || g.ExpiresAt > now));
    }
}
