using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Consent;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Platform.Services.Consent;

/// <summary>
/// What a guardian may see of their wards over the platform's endpoints (Spec 095 §12, aonik#326):
/// the child as a party, the age boundaries the platform computed, and the consent that stands.
///
/// <para>
/// Scoped through the guardian edge, never through the party table alone. A child is visible to the
/// guardians who hold active authority over them and to nobody else; asking about any other party
/// answers as if they did not exist. This is presentation: it reads the grants the same way the
/// gate does but authorises nothing — refusal stays with <see cref="IConsentGate"/>.
/// </para>
/// </summary>
internal interface IWardReader
{
    Task<IReadOnlyList<WardInfo>> ListAsync(Guid guardianPartyId, CancellationToken cancellationToken = default);

    Task<WardInfo?> GetAsync(Guid guardianPartyId, Guid childPartyId, CancellationToken cancellationToken = default);
}

/// <param name="ConsentBand">Which side of the consent-age line the child is on (Spec 095 §5).</param>
/// <param name="SafetyBand">The generation safety band Spec 096 applies to this child.</param>
internal sealed record WardInfo(
    Guid ChildPartyId,
    string DisplayName,
    string? ConsentBand,
    string? SafetyBand,
    DateTime? ConsentAgeOn,
    DateTime? MajorityOn,
    IReadOnlyList<WardGrant> Grants);

internal sealed record WardGrant(
    string Purpose,
    string TermsVersion,
    string VerificationMethod,
    Guid GrantedByPartyId,
    DateTime GrantedAt,
    DateTime? ExpiresAt);

internal sealed class WardReader : IWardReader
{
    private readonly PlatformDbContext _dbContext;
    private readonly IGuardianshipReader _guardianships;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;

    public WardReader(
        PlatformDbContext dbContext,
        IGuardianshipReader guardianships,
        ITenantProvider tenantProvider,
        IClock clock)
    {
        _dbContext = dbContext;
        _guardianships = guardianships;
        _tenantProvider = tenantProvider;
        _clock = clock;
    }

    public async Task<IReadOnlyList<WardInfo>> ListAsync(Guid guardianPartyId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var wardIds = await _guardianships.GetWardsAsync(tenantId, guardianPartyId, cancellationToken);
        var wards = new List<WardInfo>();

        foreach (var childPartyId in wardIds)
        {
            var ward = await ReadAsync(tenantId, childPartyId, cancellationToken);
            if (ward is not null)
            {
                wards.Add(ward);
            }
        }

        return wards.OrderBy(w => w.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<WardInfo?> GetAsync(Guid guardianPartyId, Guid childPartyId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        if (!await _guardianships.HasAuthorityAsync(tenantId, guardianPartyId, childPartyId, cancellationToken))
        {
            return null;
        }

        return await ReadAsync(tenantId, childPartyId, cancellationToken);
    }

    private async Task<WardInfo?> ReadAsync(Guid tenantId, Guid childPartyId, CancellationToken cancellationToken)
    {
        var party = await _dbContext.Parties
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.Id == childPartyId)
            .Select(p => new { p.DisplayName, p.ConsentBand, p.SafetyBand, p.ConsentAgeOn, p.MajorityOn })
            .FirstOrDefaultAsync(cancellationToken);

        if (party is null)
        {
            return null;
        }

        var now = _clock.UtcNow;

        // The same "active" the gate uses: unrevoked and unexpired. AnkLegacyConsents is not consulted,
        // for the reason ConsentReader gives — consent obtained before verification existed authorises nothing.
        var grants = await _dbContext.ConsentGrants
            .AsNoTracking()
            .Where(g => g.TenantId == tenantId
                && g.SubjectPartyId == childPartyId
                && g.RevokedAt == null
                && (g.ExpiresAt == null || g.ExpiresAt > now))
            .OrderBy(g => g.Purpose)
            .Select(g => new WardGrant(g.Purpose, g.TermsVersion, g.VerificationMethod, g.GrantedByPartyId, g.GrantedAt, g.ExpiresAt))
            .ToListAsync(cancellationToken);

        return new WardInfo(
            childPartyId, party.DisplayName, party.ConsentBand, party.SafetyBand, party.ConsentAgeOn, party.MajorityOn, grants);
    }
}
