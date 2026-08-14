namespace Aonik.SharedKernel.Abstractions.Workspaces;

/// <summary>
/// What a workspace <em>is</em>, as an open string with a known-values helper — the <c>OrderType</c> and
/// <c>GroupKinds</c> precedent (Spec 089 §4).
///
/// <para>
/// Platform code says <strong>workspace</strong>. "World" is product vocabulary and stays in product UIs
/// per <a href="../../../../docs/decisions/013-product-identity-is-configuration.md">ADR-013</a>. Spec 086
/// paid three review rounds for the opposite choice: <c>Household</c> in platform code led the finance
/// contributor to treat an Arke Kids family as a household and refuse the second one.
/// </para>
/// </summary>
public static class WorkspaceKinds
{
    /// <summary>An Arke world. The only kind today; the list is additive by design.</summary>
    public const string World = "world";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { World };
}

public static class WorkspaceStatuses
{
    public const string Active = "active";

    /// <summary>Deleted by its owner. Bytes are released by the sweeper, not inline (§5).</summary>
    public const string Deleted = "deleted";
}

/// <summary>
/// Where a revision sits relative to the head (Spec 089 §6, §7).
/// </summary>
public static class RevisionStates
{
    /// <summary>Descended from the head and advanced it.</summary>
    public const string FastForward = "fast-forward";

    /// <summary>
    /// Stored, sequenced, and <em>not</em> the head — its declared parent was not the head at commit time.
    ///
    /// <para>
    /// Deliberately not an error. The whole conflict design depends on a divergent revision being a durable
    /// thing a human can look at and accept or reject, rather than a rejected request the client has to
    /// remember.
    /// </para>
    /// </summary>
    public const string Diverged = "diverged";

    /// <summary>A divergent revision a human accepted; the head advanced through a new revision.</summary>
    public const string Accepted = "accepted";

    /// <summary>A divergent revision a human rejected. Its blobs are released after the retention window.</summary>
    public const string Rejected = "rejected";

    /// <summary>
    /// A divergent revision a human resolved by committing a third tree — their edited result — rather than
    /// picking a side.
    ///
    /// <para>
    /// This is what the accept gate actually produces most of the time, and it is why the platform stores three
    /// outcomes rather than two. It never learns what the human reconciled; it records that they did.
    /// </para>
    /// </summary>
    public const string Superseded = "superseded";
}

/// <summary>
/// The <c>ShareResourceKind</c> a workspace registers under, so every Spec 086 mechanic applies with no new
/// code: opaque single-use invite tokens, expiry, immediate revocation, ownership validation.
/// </summary>
public static class WorkspaceShareResource
{
    public const string Kind = "workspace";
}
