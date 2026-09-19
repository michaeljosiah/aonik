using Aonik.Groups.Persistence;
using Aonik.SharedKernel.Abstractions.Groups;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Safety;
using Aonik.SharedKernel.Abstractions.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Aonik.Groups.Services;

/// <summary>Opt-in family profile enforcement at the membership transaction boundary.</summary>
internal sealed class FamilyProfileLimitContributor(
    IGroupDataContext db,
    ITenantProvider tenantProvider,
    IConfiguration configuration,
    IServiceProvider services) : IGroupLifecycleContributor
{
    public string ModuleName => "groups";

    public async Task<string?> VetoAsync(GroupTransition transition, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantProvider.GetCurrentTenantId();
        var enabled = configuration.GetSection("Groups:ProfileLimits:TenantIds").GetChildren()
            .Any(item => Guid.TryParse(item.Value, out var id) && id == tenantId);
        if (!enabled || transition.GroupKind != GroupKinds.Family ||
            transition.Kind is not (GroupTransitionKinds.MemberAdded or GroupTransitionKinds.InviteAccepted) ||
            transition.MemberPartyId is not { } partyId)
            return null;

        var bands = services.GetService(typeof(ISafetyBandReader)) as ISafetyBandReader;
        var entitlements = services.GetService(typeof(IEntitlementReader)) as IEntitlementReader;
        if (bands is null || entitlements is null)
            return "Family profile verification is unavailable.";
        var band = await bands.GetSafetyBandAsync(partyId, cancellationToken);
        // Adults use invitations. A direct addition without an attested band fails closed.
        if (band == "adult" || (band is null && transition.Kind == GroupTransitionKinds.InviteAccepted))
            return null;
        if (band is null)
            return "The child's age record must be verified before linking a profile.";

        var allowance = await entitlements.GetMeterAsync(new SubscriberRef(SubscriberKinds.Group, transition.GroupId),
            "child-profiles", cancellationToken);
        if (allowance is null || allowance.Kind != MeterKinds.Ceiling)
            return "Choose a family plan before adding a child profile.";

        var members = await db.GroupMembers.AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.HouseholdId == transition.GroupId &&
                m.InvitationStatus == GroupMemberStatuses.Accepted && m.PartyId != null)
            .Select(m => m.PartyId!.Value).Distinct().ToListAsync(cancellationToken);
        var count = 0;
        foreach (var memberId in members.Where(id => id != partyId))
        {
            var memberBand = await bands.GetSafetyBandAsync(memberId, cancellationToken);
            if (memberBand is not null && memberBand != "adult") count++;
        }
        return count >= allowance.Allowance ? "Your family plan's child profile limit has been reached." : null;
    }

    public Task OnCommittedAsync(GroupTransition transition, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

