namespace Aonik.SharedKernel.Abstractions.Groups;

/// <summary>
/// What kind of group this is (Spec 086 §4). An open string with a known-values helper, following
/// the <c>OrderType</c> / <c>BusinessType</c> precedent, so a new shape of group is additive.
/// </summary>
public static class GroupKinds
{
    /// <summary>A personal-finance household — the shape Spec 020 built, and what existing rows become.</summary>
    public const string Household = "household";

    /// <summary>A family on a consumer product: adults who can log in, and children who cannot.</summary>
    public const string Family = "family";
}

/// <summary>
/// How much of an owner's records a grant exposes (Spec 048, generalised by Spec 086).
/// </summary>
public static class ShareScopes
{
    /// <summary>Everything the owner holds of the named resource kind.</summary>
    public const string All = "all";

    /// <summary>Only the resources named on the grant.</summary>
    public const string Entities = "entities";

    /// <summary>Documents only. The owning module interprets what that means for its resource kind.</summary>
    public const string DocsOnly = "docsOnly";
}

/// <summary>
/// Known resource kinds a grant can name (Spec 086 §6). Open, and each is owned by whichever module
/// registers an <see cref="IShareResourceResolver"/> for it — the platform never interprets one.
/// </summary>
public static class ShareResourceKinds
{
    /// <summary>PersonalFinance care entities, the kind the Circle feature shares today.</summary>
    public const string CareEntity = "care-entity";
}

/// <summary>
/// Where a member is in the invitation lifecycle (Spec 020, carried across by Spec 086).
/// </summary>
/// <remarks>
/// These are the <em>stored</em> values, not new ones. Rev 1 of this file called the first state
/// <c>Invited</c>; the column has always held <c>Pending</c>, and a constant that disagrees with the
/// data is worse than no constant at all — every comparison against it silently returns false.
/// </remarks>
public static class GroupMemberStatuses
{
    public const string Pending = "Pending";
    public const string Accepted = "Accepted";
    public const string Declined = "Declined";
    public const string Removed = "Removed";
}

/// <summary>
/// What a member may do in a group (Spec 020, carried across by Spec 086).
/// </summary>
/// <remarks>
/// Ownership is a <b>role</b>, not a second field on the group, so the two can never disagree about
/// who the owner is. The values match what the column already holds.
/// </remarks>
public static class GroupRoles
{
    public const string Owner = "Owner";
    public const string Manager = "Manager";
    public const string Viewer = "Viewer";
}

/// <summary>Lifecycle of a share grant and its invite (Spec 048).</summary>
public static class ShareGrantStatuses
{
    /// <summary>Minted but not yet accepted; there is no member on it.</summary>
    public const string Pending = "pending";

    public const string Active = "active";
    public const string Revoked = "revoked";
}

/// <summary>
/// The transitions a <see cref="IGroupLifecycleContributor"/> is told about (Spec 086 §7.1).
/// </summary>
public static class GroupTransitionKinds
{
    public const string Created = "created";

    /// <summary>A party added directly, with no invitation — the path a member without a login takes.</summary>
    public const string MemberAdded = "member-added";

    public const string InviteAccepted = "invite-accepted";
    public const string MemberRemoved = "member-removed";
    public const string OwnershipTransferred = "ownership-transferred";
}
