using Microsoft.EntityFrameworkCore;

using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;

namespace Aonik.Platform.Services.Identity;

internal sealed class UserPartyMembershipReader(PlatformDbContext db, ITenantProvider tenantProvider) : IUserPartyMembershipReader
{
    public async Task<IReadOnlyList<Guid>> GetPartyIdsAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || tenantProvider.GetCurrentTenantId() != tenantId) throw new UnauthorizedAccessException("Party membership scope mismatch.");
        return await (from link in db.UserParties.AsNoTracking()
                      join user in db.Users.AsNoTracking() on link.UserId equals user.Id
                      join party in db.Parties.AsNoTracking() on link.PartyId equals party.Id
                      where link.TenantId == tenantId && user.TenantId == tenantId && party.TenantId == tenantId
                          && user.Id == userId && user.Status == "Active" && party.Status == "Active"
                      select link.PartyId).Distinct().ToListAsync(cancellationToken);
    }
}
