namespace Aonik.SharedKernel.Abstractions.Workspaces;

/// <param name="Kind">One of <see cref="WorkspaceKinds"/>. Open string; the platform never reads meaning from it.</param>
/// <param name="OwnerPartyId">
/// A <em>party</em>, not a user. ADR-015's reason: a member need not have a login, and a child's workspace in
/// Arke Kids has an owner who cannot authenticate.
/// </param>
/// <param name="TotalBytes">
/// <c>long</c>, not <c>int</c>. A 3GB workspace is ordinary here and must be counted without truncation.
/// </param>
public sealed record WorkspaceSummary(
    Guid Id,
    string Kind,
    string Name,
    string Slug,
    Guid OwnerPartyId,
    Guid? HeadRevisionId,
    int FileCount,
    long TotalBytes,
    string Status);

/// <param name="Sequence">
/// Monotonic per workspace, assigned by the <strong>server</strong>. Ordering and history only — never sent by a
/// client and never used to detect a retry, because a client-chosen sequence makes the divergence flow
/// unreachable and a server-chosen one gives a retry nothing stable to land on (§6.1).
/// </param>
/// <param name="ParentRevisionId">
/// What the client believed it was building on. This is what makes divergence <em>detectable</em>: a revision
/// that does not descend from the head is not a fast-forward.
/// </param>
public sealed record RevisionSummary(
    Guid Id,
    Guid WorkspaceId,
    long Sequence,
    Guid? ParentRevisionId,
    Guid AuthorPartyId,
    Guid? AuthorUserId,
    string? Message,
    DateTime CreatedAt,
    int FileCount,
    long TotalBytes,
    string State);

/// <param name="Path">
/// Forward-slash and NFC-normalised. §12 treats that as a <strong>security</strong> property rather than a
/// tidiness one: unnormalised paths are how traversal and case-collision attacks get in.
/// </param>
/// <param name="ContentHash">Lowercase hex SHA-256 of the bytes. The blob's identity, not a pointer to one.</param>
public sealed record ManifestEntry(
    string Path,
    string ContentHash,
    long SizeBytes,
    string? ContentType);

/// <param name="CommitId">
/// Chosen by the <strong>client</strong>, once, before the first attempt, and reused unchanged on every retry.
/// Idempotency — never the sequence number (§6.1).
/// </param>
/// <param name="Manifest">
/// <strong>Complete, not a delta.</strong> A delta requires the server to trust the client's account of what
/// changed, and a client that omits a deletion silently resurrects a file. A complete manifest makes the revision
/// self-describing and the diff computable server-side.
/// </param>
public sealed record CommitRevisionRequest(
    Guid WorkspaceId,
    Guid CommitId,
    Guid? ParentRevisionId,
    IReadOnlyList<ManifestEntry> Manifest,
    string? Message = null);

public enum CommitOutcome
{
    /// <summary>Descended from the head and advanced it.</summary>
    FastForward = 0,

    /// <summary>Stored and sequenced, but the head did not move. Not an error — see §7.</summary>
    Diverged = 1,

    /// <summary>A true retry: the stored outcome is replayed verbatim.</summary>
    Replayed = 2,
}

/// <param name="MissingHashes">
/// Populated only when the commit was refused for naming content the tenant does not possess. Upload first,
/// commit second — never the reverse.
/// </param>
public sealed record CommitRevisionResult(
    CommitOutcome Outcome,
    Guid RevisionId,
    long Sequence,
    Guid? HeadRevisionId,
    IReadOnlyList<string> MissingHashes);

/// <param name="Missing">
/// The hashes the tenant does not already possess. Answering this costs no bytes, which is what lets an
/// unchanged 2GB workspace sync in one round trip.
/// </param>
public sealed record BlobNegotiationResult(IReadOnlyList<string> Missing);

/// <summary>How a human ended a divergent revision (§7.1).</summary>
public enum DivergenceResolution
{
    /// <summary>Advance the head through a NEW revision parented on the current head — never by rewriting.</summary>
    Accept = 0,

    /// <summary>Release its blobs and byte claims after the retention window.</summary>
    Reject = 1,
}
