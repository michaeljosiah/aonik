using Aonik.SharedKernel.Abstractions.Subscriptions;
using Aonik.SharedKernel.Primitives;

namespace Aonik.Workspaces.Entities;

/// <summary>
/// One in-progress multipart upload (Spec 091 §7).
///
/// <para>
/// <strong>Resume between blobs is not resume within one.</strong> An earlier draft claimed hash-naming made a
/// resumable large-media sync possible without any resume protocol, because a client can simply ask again which
/// hashes are missing. A 4GB take that fails at 3.9GB is missing, so the whole 4GB transfers again — on a
/// domestic uplink that is hours, repeatedly, and it is the single most likely reason a customer concludes sync
/// does not work.
/// </para>
///
/// <para>
/// The server records which parts it holds against this session, so a resumed upload asks <em>"which parts of
/// this blob do you not have"</em> — the same question as the manifest negotiation, one level down.
/// </para>
/// </summary>
public class BlobUploadSession : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public string SubscriberKind { get; set; } = SubscriberKinds.Party;

    public Guid SubscriberId { get; set; }

    /// <summary>
    /// What the client says the assembled bytes will hash to.
    ///
    /// <para>
    /// A <em>declaration</em>, never a trust: the assembled parts are hashed and compared before anything is
    /// promoted. That check is also the hash-substitution defence in 089 §12 — without it a client could upload
    /// its own bytes under someone else's hash and then read that hash back as if it were theirs.
    /// </para>
    /// </summary>
    public string DeclaredHash { get; set; } = string.Empty;

    /// <summary>
    /// What the client says the blob weighs.
    ///
    /// <para>
    /// Enforced as a bound rather than recorded as a hint. A caller declaring 1MB and streaming 4GB is aborted
    /// at the bound, so the quota check that ran against the declaration cannot be outrun by the transfer.
    /// </para>
    /// </summary>
    public long DeclaredLength { get; set; }

    public int PartSizeBytes { get; set; }

    public long ReceivedBytes { get; set; }

    public string Status { get; set; } = UploadSessionStatuses.Open;

    /// <summary>
    /// When an abandoned session and its parts become sweepable.
    ///
    /// <para>
    /// Incomplete uploads must not be billable and must not accumulate. Before this record existed, abandoned
    /// staging objects had no row at all — nothing could find them, and a multi-gigabyte upload left behind was
    /// invisible and paid for.
    /// </para>
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    public List<BlobUploadPart> Parts { get; set; } = [];
}

/// <summary>One part the server actually holds.</summary>
public class BlobUploadPart : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid SessionId { get; set; }

    /// <summary>One-based, and contiguous. Assembly reads them in this order.</summary>
    public int PartNumber { get; set; }

    public long SizeBytes { get; set; }

    public string StorageKey { get; set; } = string.Empty;
}

public static class UploadSessionStatuses
{
    public const string Open = "open";

    /// <summary>Assembled, verified and promoted. The parts are sweepable.</summary>
    public const string Completed = "completed";

    /// <summary>
    /// Abandoned by the client, or aborted by the server for exceeding its declared length.
    ///
    /// <para>
    /// Kept as a row rather than deleted immediately so the sweeper has something to find. A failed upload that
    /// leaves no trace leaves its parts behind too.
    /// </para>
    /// </summary>
    public const string Aborted = "aborted";
}
