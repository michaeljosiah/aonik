using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Consent;
using Aonik.SharedKernel.Abstractions.Safety;
using Aonik.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Platform.Services.Consent;

/// <summary>
/// Spec 096 §16.1. Answers which external companies a subject's content may reach, from the terms
/// version their <em>active</em> grant actually cites.
/// </summary>
internal sealed class ConsentedProviderReader : IConsentedProviderReader
{
    private static readonly IReadOnlySet<string> None =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private readonly PlatformDbContext _dbContext;
    private readonly IClock _clock;

    public ConsentedProviderReader(PlatformDbContext dbContext, IClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public async Task<IReadOnlySet<string>> GetConsentedProvidersAsync(
        Guid tenantId, Guid subjectPartyId, CancellationToken cancellationToken = default)
    {
        if (subjectPartyId == Guid.Empty)
        {
            return None;
        }

        var now = _clock.UtcNow;

        // The version the SUBJECT actually consented to, not the current one. A family who has not
        // re-consented after a terms change must not silently inherit the new provider list — that
        // would make publication meaningless and is the exact bypass §16.1 closes.
        var termsVersion = await _dbContext.ConsentGrants
            .AsNoTracking()
            .Where(g => g.TenantId == tenantId
                && g.SubjectPartyId == subjectPartyId
                && g.Purpose == ConsentPurposes.SafetyClassification
                && g.RevokedAt == null
                && (g.ExpiresAt == null || g.ExpiresAt > now))
            .Select(g => g.TermsVersion)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(termsVersion))
        {
            // No active classification consent means no provider may be used. Returning "everything"
            // here — the permissive reading of an empty result — is the failure this exists to
            // prevent, so the absence is explicit rather than implied by an empty collection.
            return None;
        }

        var named = await _dbContext.ConsentTermsVersions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.Version == termsVersion)
            .Select(t => t.NamedProviders)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(named))
        {
            return None;
        }

        return named
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
