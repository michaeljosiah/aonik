using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions.Platform;

using Microsoft.EntityFrameworkCore;

namespace Aonik.PersonalFinance.Services;

/// <summary>
/// Resolves the <c>Party</c> behind a user, for the Spec 086 P3 dual-write window.
/// </summary>
/// <remarks>
/// Two sources, in this order, and the order matters:
/// <list type="number">
///   <item><description>
///     The Platform <c>AnkUserParties</c> bridge, via <see cref="IUserPartyResolver"/>. This is the
///     authoritative link and the one the platform itself will read from P5 onward.
///   </description></item>
///   <item><description>
///     <c>PersonalProfile.PartyId</c>. Not redundant: seeded and demo personas carry a synthetic
///     <c>UserId</c> on a personal profile with no real <c>UserParty</c> row —
///     <c>ProjectUserBriefEndpoint</c> already carries the same fallback for the same reason. Without
///     it the P3 backfill would fail loudly on every seeded environment, which would train operators
///     to ignore the failure it exists to raise.
///   </description></item>
/// </list>
/// Returns null when neither source knows. The caller decides what that means: a <b>writer</b>
/// leaves <c>PartyId</c> null (dual-write must never break a flow that works today), while the
/// <b>backfill</b> treats it as an operator-fixable defect and fails.
///
/// Transitional by construction. Once P5 cuts readers over to party ids and a later spec drops the
/// user columns, nothing needs to map between them and this type goes with them.
/// </remarks>
internal sealed class MemberPartyResolver
{
    private readonly PersonalFinanceDbContext _dbContext;
    private readonly IUserPartyResolver _userPartyResolver;

    public MemberPartyResolver(PersonalFinanceDbContext dbContext, IUserPartyResolver userPartyResolver)
    {
        _dbContext = dbContext;
        _userPartyResolver = userPartyResolver;
    }

    public async Task<Guid?> ResolveAsync(Guid tenantId, Guid? userId, CancellationToken cancellationToken = default)
    {
        if (userId is not { } id || id == Guid.Empty || tenantId == Guid.Empty)
        {
            return null;
        }

        var linked = await _userPartyResolver.GetPartyIdForUserAsync(tenantId, id, cancellationToken);
        if (linked is not null)
        {
            return linked;
        }

        var profileParty = await _dbContext.PersonalProfiles
            .AsNoTracking()
            .Where(profile => profile.TenantId == tenantId && profile.UserId == id && profile.PartyId != Guid.Empty)
            .Select(profile => (Guid?)profile.PartyId)
            .FirstOrDefaultAsync(cancellationToken);

        return profileParty;
    }
}
