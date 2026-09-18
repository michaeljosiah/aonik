using Aonik.SharedKernel.Abstractions.Subscriptions;
using Aonik.Workspaces.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Workspaces.Services;

/// <summary>
/// The subscriber a workspace's possession and quota are held against (Spec 089 §9).
/// </summary>
internal static class WorkspaceBilling
{
    /// <summary>
    /// Falls back to the owning party when no billing subscriber is recorded, which is the shape of a workspace
    /// created before metering existed. Falling back to <em>nothing</em> would make every hash unreachable and
    /// every commit refuse. A workspace that does not exist resolves to an empty party, which possesses nothing.
    /// </summary>
    public static async Task<SubscriberRef> SubscriberForAsync(
        IWorkspaceDataContext dbContext,
        Guid tenantId,
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var billing = await dbContext.Workspaces
            .AsNoTracking()
            .Where(w => w.TenantId == tenantId && w.Id == workspaceId)
            .Select(w => new { w.BillingSubscriberKind, w.BillingSubscriberId, w.OwnerPartyId })
            .FirstOrDefaultAsync(cancellationToken);

        if (billing is null)
        {
            return new SubscriberRef(SubscriberKinds.Party, Guid.Empty);
        }

        return billing.BillingSubscriberId == Guid.Empty
            ? new SubscriberRef(SubscriberKinds.Party, billing.OwnerPartyId)
            : new SubscriberRef(billing.BillingSubscriberKind, billing.BillingSubscriberId);
    }
}
