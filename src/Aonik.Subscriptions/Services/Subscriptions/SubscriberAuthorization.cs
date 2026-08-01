using Aonik.SharedKernel.Abstractions.Groups;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Subscriptions;

namespace Aonik.Subscriptions.Services.Subscriptions;

/// <summary>
/// Resolves the contributed <see cref="ISubscriberAuthorizer"/> for a subscriber kind and enforces
/// it (Spec 087 §11).
///
/// This is the security boundary of a one-tenant-many-subscribers product. The module stores
/// <c>SubscriberId</c> opaquely on purpose, but storing it opaquely is not the same as trusting it:
/// without this check any endpoint passing a caller-controlled id could subscribe, cancel, inspect
/// or consume <b>another family's</b> entitlements inside the same tenant.
/// </summary>
internal sealed class SubscriberAuthorization
{
    private readonly IReadOnlyList<ISubscriberAuthorizer> _authorizers;

    public SubscriberAuthorization(IEnumerable<ISubscriberAuthorizer> authorizers)
    {
        _authorizers = authorizers.ToList();

        var duplicates = _authorizers
            .SelectMany(a => a.SupportedKinds)
            .GroupBy(k => k, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Count > 0)
        {
            // Two answers to "may this caller act for that subscriber" is not a tie to break by
            // taking the first — it is an ambiguity about authorisation.
            throw new InvalidOperationException(
                $"More than one ISubscriberAuthorizer claims subscriber kind(s): {string.Join(", ", duplicates)}.");
        }
    }

    /// <summary>
    /// Throws unless the current caller may act for this subscriber. An <b>unregistered kind fails
    /// closed</b>: an unknown kind is not a reason to allow the call.
    /// </summary>
    public async Task EnsureCanActForAsync(SubscriberRef subscriber, CancellationToken cancellationToken)
    {
        var authorizer = _authorizers.FirstOrDefault(a =>
            a.SupportedKinds.Any(k => string.Equals(k, subscriber.Kind, StringComparison.OrdinalIgnoreCase)));

        if (authorizer is null)
        {
            throw new PermissionDeniedException(
                $"No authorizer is registered for subscriber kind '{subscriber.Kind}'.");
        }

        if (!await authorizer.CanActForAsync(subscriber, cancellationToken))
        {
            // Deliberately one message for both "does not exist" and "not yours": distinguishing
            // them would let a caller enumerate other subscribers.
            throw new PermissionDeniedException(
                $"The current caller may not act for {subscriber.Kind} '{subscriber.Id}'.");
        }
    }
}

/// <summary>
/// Authorises <c>tenant</c>-kind subscribers (Spec 087 §11) — a B2B tenant on a platform plan.
/// The subscriber id must be the current tenant; acting for another tenant's subscription is never
/// in scope for a tenant-scoped caller.
/// </summary>
internal sealed class TenantSubscriberAuthorizer : ISubscriberAuthorizer
{
    private readonly ITenantProvider _tenantProvider;

    public TenantSubscriberAuthorizer(ITenantProvider tenantProvider) => _tenantProvider = tenantProvider;

    public IReadOnlyCollection<string> SupportedKinds => [SubscriberKinds.Tenant];

    public Task<bool> CanActForAsync(SubscriberRef subscriber, CancellationToken cancellationToken = default)
        => Task.FromResult(subscriber.Id == _tenantProvider.GetCurrentTenantId());
}

/// <summary>
/// Authorises <c>party</c>-kind subscribers (Spec 087 §11) — an individual on their own plan. The
/// caller must <em>be</em> that party.
/// </summary>
internal sealed class PartySubscriberAuthorizer : ISubscriberAuthorizer
{
    private readonly ICurrentPartyResolver _currentParty;

    public PartySubscriberAuthorizer(ICurrentPartyResolver currentParty) => _currentParty = currentParty;

    public IReadOnlyCollection<string> SupportedKinds => [SubscriberKinds.Party];

    public async Task<bool> CanActForAsync(SubscriberRef subscriber, CancellationToken cancellationToken = default)
    {
        var partyId = await _currentParty.GetCurrentPartyIdAsync(cancellationToken);
        return partyId is not null && partyId == subscriber.Id;
    }
}

/// <summary>
/// Authorises <c>group</c>-kind subscribers (Spec 087 §11) — a family or household on a shared plan.
/// The caller must be an accepted member of that group.
/// </summary>
/// <remarks>
/// Without this the module advertised <c>SubscriberKinds.Group</c> and then failed closed on every
/// use of it: no authorizer meant "No authorizer is registered for subscriber kind 'group'", which
/// made the entire group-backed subscription model — the one Arke Kids and Payabo are built on —
/// unusable rather than merely unimplemented.
///
/// Membership, not ownership. A group plan is bought for the group, and any accepted member may read
/// its entitlements and draw on them; restricting that to the owner would mean a second parent could
/// not use what the family pays for. Party-only members are included by
/// <c>IGroupReader.GetMembersAsync</c>, which is deliberate — a child consuming their own allowance
/// is the point of the model, and the caller's identity is checked before this is ever reached.
/// </remarks>
internal sealed class GroupSubscriberAuthorizer : ISubscriberAuthorizer
{
    private readonly IGroupReader _groups;
    private readonly ICurrentPartyResolver _currentParty;

    public GroupSubscriberAuthorizer(IGroupReader groups, ICurrentPartyResolver currentParty)
    {
        _groups = groups;
        _currentParty = currentParty;
    }

    public IReadOnlyCollection<string> SupportedKinds => [SubscriberKinds.Group];

    public async Task<bool> CanActForAsync(SubscriberRef subscriber, CancellationToken cancellationToken = default)
    {
        var partyId = await _currentParty.GetCurrentPartyIdAsync(cancellationToken);

        if (partyId is null)
        {
            return false;
        }

        var members = await _groups.GetMembersAsync(subscriber.Id, cancellationToken);
        return members.Any(member => member.PartyId == partyId);
    }
}
