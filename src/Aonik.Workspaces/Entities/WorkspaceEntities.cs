using Aonik.SharedKernel.Abstractions.Workspaces;
using Aonik.SharedKernel.Primitives;

namespace Aonik.Workspaces.Entities;

/// <summary>
/// A named container of versioned files (Spec 089 §4).
///
/// <para>
/// Nothing here names a product concept. <c>Kind</c> carries <c>world</c> as a <em>value</em>, and that is the
/// only place Arke's vocabulary appears — Spec 086 paid three review rounds for the opposite choice, when
/// <c>Household</c> in platform code led a contributor to treat an Arke Kids family as a household and refuse the
/// second one.
/// </para>
/// </summary>
public class Workspace : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>One of <see cref="WorkspaceKinds"/>. Open string; additive by design.</summary>
    public string Kind { get; set; } = WorkspaceKinds.World;

    public string Name { get; set; } = string.Empty;

    /// <summary>URL-safe, unique per tenant. Renaming does not move it.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// A <em>party</em>, not a user. ADR-015's reason applies directly: a member need not have a login, and a
    /// child's workspace in Arke Kids has an owner who cannot authenticate.
    /// </summary>
    public Guid OwnerPartyId { get; set; }

    /// <summary>
    /// The revision a fresh clone gets. Null until the first commit.
    ///
    /// <para>
    /// Advanced by a <c>RowVersion</c> compare-and-swap rather than inside a transaction (§6.2) — "inside the
    /// transaction" is not the same as atomic when two clients commit from the same head.
    /// </para>
    /// </summary>
    public Guid? HeadRevisionId { get; set; }

    /// <summary>
    /// The next sequence a commit may take (Spec 089 §6.2).
    ///
    /// <para>
    /// It lives here, beside the head, because <strong>allocating a sequence and advancing the head are the same
    /// atomic decision</strong>. An earlier revision of the spec allocated the sequence first and guarded only the
    /// head update; two commits reading the same head then both computed N+1 and the loser collided on the unique
    /// sequence index during its insert — dying before it could reach the guard meant to reclassify it as
    /// diverged. One guarded write over both fields is what makes the loser reclassifiable instead of dead.
    /// </para>
    /// </summary>
    public long NextSequence { get; set; } = 1;

    public int FileCount { get; set; }

    /// <summary><c>long</c>. A 3GB workspace is ordinary here and must be counted without truncation.</summary>
    public long TotalBytes { get; set; }

    public string Status { get; set; } = WorkspaceStatuses.Active;
}

/// <summary>
/// An immutable point in a workspace's history (Spec 089 §6).
/// </summary>
public class WorkspaceRevision : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid WorkspaceId { get; set; }

    /// <summary>
    /// Monotonic per workspace, assigned by the <strong>server</strong> at commit time. Ordering and history
    /// only — a client never sends it and it is never used to detect a retry, because making it the idempotency
    /// key breaks the divergence flow the whole design rests on (§6.1).
    /// </summary>
    public long Sequence { get; set; }

    /// <summary>
    /// What the client believed it was building on. This is what makes divergence detectable: a revision that
    /// does not descend from the head is not a fast-forward.
    /// </summary>
    public Guid? ParentRevisionId { get; set; }

    /// <summary>
    /// Client-chosen, once, reused unchanged on every retry. Idempotency (§6.1).
    /// </summary>
    public Guid CommitId { get; set; }

    /// <summary>
    /// SHA-256 over the workspace id, the declared parent, and the manifest as a sorted path→hash list.
    ///
    /// <para>
    /// The reason a <c>CommitId</c> alone is not enough (§6.1.1): after a timeout the author may have kept
    /// working, and the client correctly reuses the id while rebuilding the manifest from a <em>changed</em> tree.
    /// Replaying the original outcome would report success for work that was never stored, and the next pull
    /// would treat those edits as absent. Same id with a different hash is a loud 409 instead.
    /// </para>
    /// </summary>
    public string RequestHash { get; set; } = string.Empty;

    public Guid AuthorPartyId { get; set; }

    /// <summary>Null when the author has no login — the same reason ownership is a party.</summary>
    public Guid? AuthorUserId { get; set; }

    public string? Message { get; set; }

    /// <summary>One of <see cref="RevisionStates"/>.</summary>
    public string State { get; set; } = RevisionStates.FastForward;

    public int FileCount { get; set; }
    public long TotalBytes { get; set; }
    public DateTime CommittedAt { get; set; }

    /// <summary>Set when a human accepted or rejected a divergent revision (§7.1).</summary>
    public DateTime? ResolvedAt { get; set; }

    public Guid? ResolvedByPartyId { get; set; }
}

/// <summary>
/// One path in one revision. <strong>The manifest is the revision</strong> (Spec 089 §4).
/// </summary>
public class WorkspaceFile : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid RevisionId { get; set; }

    /// <summary>
    /// Forward-slash, NFC-normalised, no traversal segments.
    ///
    /// <para>
    /// §12 treats normalisation as a <strong>security</strong> property rather than a tidiness one: two paths
    /// that differ only by Unicode composition or case are two rows pointing at what a filesystem will treat as
    /// one file, and that is how a checkout overwrites something the manifest never named.
    /// </para>
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Lowercase hex SHA-256. The blob's identity, not a pointer to one.</summary>
    public string ContentHash { get; set; } = string.Empty;

    public long SizeBytes { get; set; }
    public string? ContentType { get; set; }
}

/// <summary>
/// Content, once, per tenant (Spec 089 §5).
/// </summary>
public class WorkspaceBlob : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>Lowercase hex SHA-256 of the bytes. Unique per tenant — that uniqueness IS the dedupe.</summary>
    public string ContentHash { get; set; } = string.Empty;

    /// <summary><c>workspaces/{tenantId:N}/blobs/{hash[0..2]}/{hash}</c>.</summary>
    public string StorageKey { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    /// <summary>
    /// How many manifest rows name these bytes.
    ///
    /// <para>
    /// <strong>Not optional.</strong> Deleting a revision must not delete bytes another revision still names, and
    /// deleting a workspace must not delete bytes another workspace in the same tenant deduped against. Getting
    /// this wrong destroys data silently, and it is discovered by a user opening an old revision to find it empty.
    /// </para>
    /// </summary>
    public int RefCount { get; set; }

    /// <summary>
    /// Set by the sweeper before it deletes, and cleared if a new reference lands first (§5.1).
    ///
    /// <para>
    /// The tombstone is what serialises sweeping against new references. Without it a commit can reference a blob
    /// in the moment between the sweeper reading <c>RefCount == 0</c> and deleting the object — leaving a manifest
    /// naming bytes that no longer exist. With it, the referencing commit is told the blob is missing (and
    /// re-uploads) rather than silently pointing at nothing.
    /// </para>
    /// </summary>
    public bool IsDeleting { get; set; }

    public DateTime? DeletingSince { get; set; }
}
