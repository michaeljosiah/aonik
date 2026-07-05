using Microsoft.EntityFrameworkCore;

using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions.UserBrief;

namespace Aonik.PersonalFinance.Services;

/// <summary>
/// Finance-side fallback resolver for the playground User Brief picker:
/// when a Party has no real <c>UserParty</c> link (e.g. the seeded
/// Seamus / Mark Keane demo personas), look up its <c>PersonalProfile</c>
/// and return the synthetic UserId stored there. See
/// <see cref="IPersonalFinancePartyResolver"/> for the wider context.
/// </summary>
internal sealed class PersonalFinancePartyResolver : IPersonalFinancePartyResolver
{
    private readonly PersonalFinanceDbContext _db;

    public PersonalFinancePartyResolver(PersonalFinanceDbContext db)
    {
        _db = db;
    }

    public async Task<Guid?> GetUserIdForPartyAsync(
        Guid tenantId,
        Guid partyId,
        CancellationToken cancellationToken = default)
    {
        return await _db.PersonalProfiles
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.PartyId == partyId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => (Guid?)p.UserId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
