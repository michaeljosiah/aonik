namespace Aonik.SharedKernel.Abstractions.Groups;

/// <summary>
/// Group membership as a platform capability (Spec 086 / ADR-015), implemented by
/// <c>Aonik.Platform</c>.
///
/// Two ways in, and they partition cleanly rather than overlapping: <b>invitation</b> for anyone who
/// can consent, <b>direct addition</b> for anyone who cannot. Neither can be used to do the other's
/// job, which is what stops the second becoming a way around the first.
/// </summary>
public interface IGroupService
{
    Task<GroupDto> CreateAsync(CreateGroupCommand command, CancellationToken cancellationToken = default);

    Task<GroupDto?> GetAsync(Guid groupId, CancellationToken cancellationToken = default);

    /// <summary>Groups the current caller's party belongs to.</summary>
    Task<IReadOnlyList<GroupDto>> GetMineAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Add a party directly as a member — no invitation, because the party may have no user to
    /// accept one. Authorised against the <b>caller's</b> ownership or admin role in the group,
    /// never against the added party: a party is added <em>by</em> someone with the right, never by
    /// itself.
    /// </summary>
    /// <remarks>
    /// Two constraints, without which this becomes a consent bypass:
    /// <list type="bullet">
    ///   <item>The party must resolve <b>in the current tenant</b>. A foreign or unknown id is rejected, not silently accepted.</item>
    ///   <item>The party must <b>not be linked to a user</b>. Anyone with a login belongs on the invitation path, which is where their consent is recorded.</item>
    /// </list>
    /// </remarks>
    /// <exception cref="NotFoundException">The group, or the party in this tenant, does not exist.</exception>
    /// <exception cref="InvalidStateException">The party has a user and must be invited instead.</exception>
    /// <exception cref="PermissionDeniedException">The caller may not add members to this group.</exception>
    Task<GroupMemberDto> AddMemberAsync(
        Guid groupId,
        Guid partyId,
        string role,
        CancellationToken cancellationToken = default);

    /// <summary>Invite someone who can consent. The invitation is accepted by them, not on their behalf.</summary>
    Task<GroupMemberDto> InviteAsync(InviteGroupMemberCommand command, CancellationToken cancellationToken = default);

    Task<GroupMemberDto> AcceptInvitationAsync(Guid membershipId, CancellationToken cancellationToken = default);

    Task<GroupMemberDto> DeclineInvitationAsync(Guid membershipId, CancellationToken cancellationToken = default);

    Task<GroupMemberDto> ChangeRoleAsync(Guid membershipId, string role, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hand ownership to another member. Ownership is a <b>role</b>, not a second field on the group
    /// — one representation, so the two can never disagree.
    /// </summary>
    Task<GroupDto> TransferOwnershipAsync(Guid groupId, Guid toMembershipId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove a member. Contributed <see cref="IGroupLifecycleContributor"/>s may veto this, and
    /// those that do not still get to react — removal has real consequences in the modules that
    /// hang off a membership.
    /// </summary>
    Task RemoveMemberAsync(Guid membershipId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Read-only group access for modules that need to know who is in a group without being able to
/// change it (Spec 086 §9).
/// </summary>
public interface IGroupReader
{
    Task<GroupDto?> GetAsync(Guid groupId, CancellationToken cancellationToken = default);

    /// <summary>Groups a party belongs to, whatever their role.</summary>
    Task<IReadOnlyList<GroupDto>> GetForPartyAsync(Guid partyId, CancellationToken cancellationToken = default);

    /// <summary>Groups a <b>user</b> belongs to. Transitional — see the remark.</summary>
    /// <remarks>
    /// Added in P7 so the personal-finance consumers can stop querying the group tables directly.
    /// User-keyed rather than party-keyed because those consumers are, and because a membership
    /// written before the P3 backfill has no party at all — asking by party would silently drop it,
    /// and for an authorisation read that means denying someone access they have. Goes with the
    /// user columns.
    /// </remarks>
    Task<IReadOnlyList<GroupDto>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Whether a group exists in the current tenant, without loading it or its members.</summary>
    Task<bool> ExistsAsync(Guid groupId, CancellationToken cancellationToken = default);

    /// <summary>Accepted members only — invited-but-unanswered people are not yet in the group.</summary>
    Task<IReadOnlyList<GroupMemberDto>> GetMembersAsync(Guid groupId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Lets a module veto a membership change <b>and</b> react to one (Spec 086 §7.1).
/// Module-contributed via <c>IEnumerable&lt;T&gt;</c> DI.
///
/// A veto-only interface was the original design and it was wrong: removing a member today clears
/// the member's personal-finance profile link, unshares every account they own, emits an
/// integration event and invalidates a cache. A refusal has nowhere to put any of that, so moving
/// the lifecycle behind one would silently drop behaviour rather than relocate it.
/// </summary>
public interface IGroupLifecycleContributor
{
    /// <summary>Module name, for diagnostics.</summary>
    string ModuleName { get; }

    /// <summary>Null when the transition may proceed; otherwise a human-readable reason it may not.</summary>
    Task<string?> VetoAsync(GroupTransition transition, CancellationToken cancellationToken = default);

    /// <summary>
    /// React to a committed transition. Runs inside the <b>same transaction</b> as the membership
    /// write, so a failure rolls the whole transition back rather than leaving a member whose
    /// side effects half-applied.
    /// </summary>
    Task OnCommittedAsync(GroupTransition transition, CancellationToken cancellationToken = default);
}
