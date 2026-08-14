using Aonik.SharedKernel.Abstractions.Groups;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Workspaces;
using Aonik.Workspaces.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Workspaces.Services;

/// <summary>
/// Resolves <c>workspace</c> ids for Spec 086's sharing machinery (Spec 089 §8).
///
/// <para>
/// Registering the kind is all it takes to inherit every mechanic: opaque single-use invite tokens, expiry,
/// immediate revocation, ownership validation and the anonymous preview — with no new code.
/// </para>
///
/// <para>
/// It returns only workspaces the owner <em>actually owns</em>, omitting the rest rather than reporting them.
/// That omission is load-bearing: grant creation compares counts, so a caller naming somebody else's ids is
/// detected by the short list rather than by a check it could have skipped.
/// </para>
/// </summary>
internal sealed class WorkspaceShareResourceResolver : IShareResourceResolver
{
    private readonly IWorkspaceDataContext _dbContext;
    private readonly ITenantProvider _tenantProvider;

    public WorkspaceShareResourceResolver(
        IWorkspaceDataContext dbContext, ITenantProvider tenantProvider)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
    }

    public IReadOnlyCollection<string> ResourceKinds => [WorkspaceShareResource.Kind];

    public async Task<IReadOnlyList<ShareResourceRef>> ResolveAsync(
        string resourceKind,
        ShareResourceOwner owner,
        IReadOnlyCollection<Guid> resourceIds,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(resourceKind, WorkspaceShareResource.Kind, StringComparison.OrdinalIgnoreCase)
            || owner.PartyId is not { } ownerPartyId
            || resourceIds.Count == 0)
        {
            return [];
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var ids = resourceIds.ToList();

        // A local query, so an authorisation check costs one round trip and never leaves the platform.
        var workspaces = await _dbContext.Workspaces
            .AsNoTracking()
            .Where(w => w.TenantId == tenantId
                && ids.Contains(w.Id)
                && w.OwnerPartyId == ownerPartyId
                && w.Status == WorkspaceStatuses.Active)
            .Select(w => new { w.Id, w.Name })
            .ToListAsync(cancellationToken);

        return
        [
            .. workspaces.Select(w =>
                new ShareResourceRef(w.Id, WorkspaceShareResource.Kind, w.Name))
        ];
    }
}
