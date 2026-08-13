using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Consent;
using Aonik.Platform.Entities.Party;
using Aonik.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Platform.Services.Consent;

/// <summary>
/// Spec 095 §12. Answers "may this party act for that one" from the Guardian edge alone.
/// </summary>
internal sealed class GuardianshipReader : IGuardianshipReader
{
    private readonly PlatformDbContext _dbContext;
    private readonly IClock _clock;

    public GuardianshipReader(PlatformDbContext dbContext, IClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public async Task<bool> HasAuthorityAsync(
        Guid tenantId, Guid guardianPartyId, Guid childPartyId, CancellationToken cancellationToken = default)
    {
        if (guardianPartyId == Guid.Empty || childPartyId == Guid.Empty)
        {
            return false;
        }

        // Kinship is never consulted. A Mother edge grants nothing: parental authority is not
        // parenthood, and inferring it gets real families wrong in the direction of granting access
        // to someone who should not have it (Spec 095 §7).
        return await ActiveEdges(tenantId)
            .AnyAsync(r => r.FromPartyId == guardianPartyId && r.ToPartyId == childPartyId, cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetGuardiansAsync(
        Guid tenantId, Guid childPartyId, CancellationToken cancellationToken = default)
        => await ActiveEdges(tenantId)
            .Where(r => r.ToPartyId == childPartyId)
            .Select(r => r.FromPartyId)
            .Distinct()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetWardsAsync(
        Guid tenantId, Guid guardianPartyId, CancellationToken cancellationToken = default)
        => await ActiveEdges(tenantId)
            .Where(r => r.FromPartyId == guardianPartyId)
            .Select(r => r.ToPartyId)
            .Distinct()
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Active edges only, and the majority date is enforced here rather than trusted to a job.
    /// A guardian edge that outlives majority is a continuing authority over an adult's data — so if
    /// the transition job has not run yet, the read must still refuse.
    /// </summary>
    private IQueryable<PartyRelationship> ActiveEdges(Guid tenantId)
    {
        var now = _clock.UtcNow;

        return _dbContext.PartyRelationships
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId
                && r.IsActive
                && r.RelationshipTypeCode == PartyRelationshipTypes.Guardian)
            .Join(
                _dbContext.Parties.AsNoTracking().Where(p => p.MajorityOn == null || p.MajorityOn > now),
                r => r.ToPartyId,
                p => p.Id,
                (r, _) => r);
    }
}
