using Aonik.Groups.Services;
using Aonik.PersonalFinance.Entities;

namespace Aonik.PersonalFinance.Services;

/// <summary>
/// PersonalFinance's view of the group membership rules (Spec 086 §8.1).
/// </summary>
/// <remarks>
/// The rules themselves moved to <see cref="GroupMembershipRules"/> in P4 — none of them knew what
/// a household was. This type survives as a forwarder rather than being deleted so the roughly
/// twenty call sites across this module keep compiling and keep reading in household vocabulary;
/// deleting it would have turned a behaviour-preserving move into a module-wide rename.
///
/// The one member that is genuinely this module's own is <see cref="IsAcceptedUserMember"/>: it
/// encodes that PersonalFinance is a user-scoped domain, which is a fact about finance, not about
/// groups.
/// </remarks>
internal static class HouseholdMembershipRules
{
    internal static readonly IReadOnlyList<string> EmptyPermissions = GroupMembershipRules.EmptyPermissions;

    public static string NormalizeRole(string role) => GroupMembershipRules.NormalizeRole(role);

    public static string NormalizeInvitationStatus(string? invitationStatus)
        => GroupMembershipRules.NormalizeStatus(invitationStatus);

    public static bool IsAccepted(HouseholdMember member) => GroupMembershipRules.IsAccepted(member);

    public static bool IsPending(HouseholdMember member) => GroupMembershipRules.IsPending(member);

    public static bool CanManageMembers(HouseholdMember member) => GroupMembershipRules.CanManageMembers(member);

    public static bool IsOwner(HouseholdMember member) => GroupMembershipRules.IsOwner(member);

    public static IReadOnlyList<string> NormalizePermissions(IReadOnlyList<string>? permissions)
        => GroupMembershipRules.NormalizePermissions(permissions);

    public static IReadOnlyList<string> ParsePermissions(string? permissionsJson)
        => GroupMembershipRules.ParsePermissions(permissionsJson);

    public static string SerializePermissions(IReadOnlyList<string>? permissions)
        => GroupMembershipRules.SerializePermissions(permissions);

    public static void NormalizeLegacyMember(HouseholdMember member) => GroupMembershipRules.NormalizeLegacy(member);

    /// <summary>
    /// An accepted member who has a login.
    /// </summary>
    /// <remarks>
    /// Spec 086 made <c>UserId</c> nullable, because a group member need not have a login — a child
    /// in an Arke Kids family is a party with no principal. Every PersonalFinance path is
    /// user-scoped, though: profiles, accounts and life-graph caches all key on a user. So a
    /// party-only member is not "missing data" here, it is simply not this module's concern, and
    /// filtering it out at the point where a member LIST is built is what lets the call sites read
    /// <c>UserId</c> without null handling.
    /// </remarks>
    public static bool IsAcceptedUserMember(HouseholdMember member)
        => IsAccepted(member) && member.UserId is not null;
}
