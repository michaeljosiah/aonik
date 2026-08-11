using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions.Platform;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Platform.Services.Party;

/// <summary>
/// PlatformDbContext-backed <see cref="IUserPartyResolver"/> over the <c>AnkUserParties</c> bridge.
/// Lives in Aonik.Platform (which owns the <c>UserParty</c> entity) so cross-cutting consumers — the
/// scoped document-search agent tool in particular — can map an authenticated user to their owner
/// party through a SharedKernel contract, with no project reference. When a user has more than one
/// party link the most recent wins; an unlinked user resolves to <c>null</c>.
/// </summary>
internal sealed class UserPartyResolver : IUserPartyResolver
{
    private readonly PlatformDbContext _dbContext;

    public UserPartyResolver(PlatformDbContext dbContext) => _dbContext = dbContext;

    public async Task<Guid?> GetPartyIdForUserAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || userId == Guid.Empty)
        {
            return null;
        }

        return await _dbContext.UserParties
            .AsNoTracking()
            .Where(link => link.TenantId == tenantId && link.UserId == userId)
            .OrderByDescending(link => link.CreatedAt)
            .Select(link => (Guid?)link.PartyId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Guid?> GetUserIdForPartyAsync(
        Guid tenantId,
        Guid partyId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || partyId == Guid.Empty)
        {
            return null;
        }

        return await _dbContext.UserParties
            .AsNoTracking()
            .Where(link => link.TenantId == tenantId && link.PartyId == partyId)
            .OrderByDescending(link => link.CreatedAt)
            .Select(link => (Guid?)link.UserId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
