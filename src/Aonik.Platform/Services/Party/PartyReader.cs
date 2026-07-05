using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions.Platform;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Platform.Services.Party;

/// <summary>
/// PlatformDbContext-backed implementation of <see cref="IPartyReader"/>.
/// Lives in Aonik.Platform so PersonalFinance and other consumers can read
/// party / party-relationship history without taking a project reference on
/// <c>Aonik.Finance.Entities.PartyReadModel</c> (a transitional read projection
/// in the Finance module).
/// See <a href="../../../../../docs/specifications/027.extract-personal-finance-module.html">Spec 027</a>.
/// </summary>
internal sealed class PartyReader : IPartyReader
{
    private readonly PlatformDbContext _dbContext;

    public PartyReader(PlatformDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<PartyHistoryItem>> GetByIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> partyIds,
        CancellationToken cancellationToken = default)
    {
        if (partyIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.Parties
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && partyIds.Contains(p.Id))
            .Select(p => new PartyHistoryItem(
                p.Id,
                p.DisplayName,
                p.Status,
                p.CustomerTierCode))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PartyRelationshipHistoryItem>> GetRelationshipsForPartyAsync(
        Guid tenantId,
        Guid partyId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.PartyRelationships
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId
                && r.IsActive
                && (r.FromPartyId == partyId || r.ToPartyId == partyId))
            .OrderBy(r => r.RelationshipTypeCode)
            .Select(r => new PartyRelationshipHistoryItem(
                r.Id,
                r.FromPartyId,
                r.ToPartyId,
                r.RelationshipTypeCode,
                r.IsActive,
                r.Notes))
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsAsync(
        Guid tenantId,
        Guid partyId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Parties
            .AsNoTracking()
            .AnyAsync(p => p.TenantId == tenantId && p.Id == partyId, cancellationToken);
    }

    public Task<bool> HasActiveRelationshipBetweenAsync(
        Guid tenantId,
        Guid partyAId,
        Guid partyBId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.PartyRelationships
            .AsNoTracking()
            .AnyAsync(r => r.TenantId == tenantId
                && r.IsActive
                && ((r.FromPartyId == partyAId && r.ToPartyId == partyBId)
                    || (r.ToPartyId == partyAId && r.FromPartyId == partyBId)),
                cancellationToken);
    }

    public async Task<Guid?> GetTenantPartyIdAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        // The tenant's own party is the earliest-created party in the tenant
        // (ordered by Id). Returns null when the tenant has no party yet.
        return await _dbContext.Parties
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .OrderBy(p => p.Id)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
