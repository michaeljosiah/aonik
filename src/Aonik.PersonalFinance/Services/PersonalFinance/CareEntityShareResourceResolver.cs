using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions.Groups;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Abstractions.UserBrief;
using Aonik.SharedKernel.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Aonik.PersonalFinance.Services;

/// <summary>
/// Resolves <c>care-entity</c> resources for the share-grant service (Spec 086 §6).
/// </summary>
/// <remarks>
/// <para>
/// This is the line the extraction turns on: a grant names a kind and a list of ids, and only the
/// module that owns that kind can say what the ids are or whose they are. Without a resolver the
/// platform would need a foreign key to <c>CareEntity</c> — exactly the coupling ADR-015 exists to
/// remove.
/// </para>
/// <para>
/// <b>Owner-scoped, and ids the owner does not own are omitted rather than reported.</b> That
/// omission is what lets the caller detect them by comparing counts, and it is load-bearing: without
/// it a caller could persist another party's ids into a grant and read them back through
/// <c>IShareGrantReader</c>, which answers from the stored ids alone.
/// </para>
/// <para>
/// Care entities are keyed on <c>UserId</c>, not <c>PartyId</c>, so the owner party is mapped back
/// to a user here. The mapping goes when Spec 086's later phases re-key the personal-finance
/// entities themselves; until then it lives at this boundary rather than leaking party ids into
/// queries that cannot use them.
/// </para>
/// </remarks>
internal sealed class CareEntityShareResourceResolver : IShareResourceResolver
{
    private readonly PersonalFinanceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IUserPartyResolver _userPartyResolver;
    private readonly IPersonalFinancePartyResolver _partyResolver;

    public CareEntityShareResourceResolver(
        PersonalFinanceDbContext dbContext,
        ITenantProvider tenantProvider,
        IUserPartyResolver userPartyResolver,
        IPersonalFinancePartyResolver partyResolver)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _userPartyResolver = userPartyResolver;
        _partyResolver = partyResolver;
    }

    public IReadOnlyCollection<string> ResourceKinds { get; } = [ShareResourceKinds.CareEntity];

    public async Task<IReadOnlyList<ShareResourceRef>> ResolveAsync(
        string resourceKind,
        ShareResourceOwner owner,
        IReadOnlyCollection<Guid> resourceIds,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(resourceKind, ShareResourceKinds.CareEntity, StringComparison.OrdinalIgnoreCase)
            || resourceIds.Count == 0)
        {
            return [];
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();

        // Care entities are keyed on the user, so if the caller already knows it, use it — mapping
        // party to user and back would only introduce a way for the two to disagree. Otherwise the
        // bridge, then the profile: the same chain the caller's party was resolved by, because
        // asking only one of them fails for owners linked through the other, and that failure reads
        // as "you do not own these ids" — the worst possible way to report a lookup mismatch.
        var ownerUserId = owner.UserId
            ?? (owner.PartyId is { } ownerPartyId
                ? await _userPartyResolver.GetUserIdForPartyAsync(tenantId, ownerPartyId, cancellationToken)
                    ?? await _partyResolver.GetUserIdForPartyAsync(tenantId, ownerPartyId, cancellationToken)
                : null);

        if (ownerUserId is not { } userId)
        {
            // An owner with no personal-finance identity owns no care entities. Returning nothing
            // fails the caller's count check, which is the right answer — better than resolving
            // ids the caller could not possibly own.
            return [];
        }

        var ids = resourceIds.ToList();

        // AcrossTenants plus an explicit TenantId predicate: the anonymous invite preview resolves a
        // tenant from the token rather than from the ambient filter, and this must answer there too.
        var entities = await _dbContext.CareEntities
            .AsNoTracking()
            .AcrossTenants()
            .Where(entity => entity.TenantId == tenantId
                && entity.UserId == userId
                && ids.Contains(entity.Id)
                && !entity.Archived
                // Same reason as the invite lookup: AcrossTenants disables the soft-delete filter,
                // and a live invite must not disclose the names of deleted resources.
                && !entity.IsDeleted)
            .OrderBy(entity => entity.Name)
            .Select(entity => new { entity.Id, entity.Name })
            .ToListAsync(cancellationToken);

        return entities
            .Select(entity => new ShareResourceRef(entity.Id, ShareResourceKinds.CareEntity, entity.Name))
            .ToList();
    }
}
