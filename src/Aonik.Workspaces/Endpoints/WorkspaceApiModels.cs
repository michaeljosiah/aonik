using Aonik.SharedKernel.Abstractions.Workspaces;

namespace Aonik.Workspaces.Endpoints;

// The wire shapes of the workspace endpoints (Spec 089 §8.1, §11; Spec 091). Kept independent of any
// caller's process identity or local path: a request names a workspace, a revision and content by
// hash, never a folder or a host.

/// <param name="Kind">One of <see cref="WorkspaceKinds"/>; defaults to <c>world</c>.</param>
/// <param name="BillingSubscriber">
/// Who pays for the slot and the bytes. Omitted, the caller's own party; a consumer product names the
/// family <c>Group</c>. The meter refuses a subscriber the caller may not act for.
/// </param>
public sealed record CreateWorkspaceRequest(
    string Name,
    string? Kind = null,
    SubscriberModel? BillingSubscriber = null);

public sealed record SubscriberModel(string Kind, Guid Id);

public sealed record WorkspaceResponse(
    Guid Id,
    string Kind,
    string Name,
    string Slug,
    Guid OwnerPartyId,
    Guid? HeadRevisionId,
    int FileCount,
    long TotalBytes,
    string Status)
{
    public static WorkspaceResponse From(WorkspaceSummary summary) => new(
        summary.Id,
        summary.Kind,
        summary.Name,
        summary.Slug,
        summary.OwnerPartyId,
        summary.HeadRevisionId,
        summary.FileCount,
        summary.TotalBytes,
        summary.Status);
}

public sealed record WorkspaceListResponse(IReadOnlyList<WorkspaceResponse> Workspaces);

/// <param name="Path">Forward-slash, relative, NFC. Validated on commit; rejected rather than sanitised.</param>
/// <param name="ContentHash">Lowercase hex SHA-256 of the bytes.</param>
public sealed record ManifestEntryModel(
    string Path,
    string ContentHash,
    long SizeBytes,
    string? ContentType = null)
{
    public ManifestEntry ToEntry() => new(Path, ContentHash, SizeBytes, ContentType);

    public static ManifestEntryModel From(ManifestEntry entry)
        => new(entry.Path, entry.ContentHash, entry.SizeBytes, entry.ContentType);
}

/// <param name="RevisionId">The revision the entries describe; <c>null</c> when the workspace has no head yet.</param>
public sealed record ManifestResponse(Guid? RevisionId, IReadOnlyList<ManifestEntryModel> Entries);

public sealed record RevisionResponse(
    Guid Id,
    long Sequence,
    Guid? ParentRevisionId,
    Guid AuthorPartyId,
    string? Message,
    DateTime CommittedAt,
    int FileCount,
    long TotalBytes,
    string State)
{
    public static RevisionResponse From(RevisionSummary revision) => new(
        revision.Id,
        revision.Sequence,
        revision.ParentRevisionId,
        revision.AuthorPartyId,
        revision.Message,
        revision.CreatedAt,
        revision.FileCount,
        revision.TotalBytes,
        revision.State);
}

public sealed record RevisionListResponse(IReadOnlyList<RevisionResponse> Revisions);

public sealed record NegotiateRequest(IReadOnlyList<string> ContentHashes);

/// <param name="Missing">Hashes the caller must upload before a manifest may name them.</param>
public sealed record NegotiateResponse(IReadOnlyList<string> Missing);

/// <param name="CommitId">Client-chosen, once, and reused unchanged on every retry (Spec 089 §6.1).</param>
/// <param name="ParentRevisionId">The head the client built on; <c>null</c> only for a first commit.</param>
/// <param name="Manifest">Complete, never a delta.</param>
public sealed record CommitRequest(
    Guid CommitId,
    Guid? ParentRevisionId,
    IReadOnlyList<ManifestEntryModel> Manifest,
    string? Message = null);

/// <param name="Outcome">
/// <c>fastForward</c>: the head advanced to <c>RevisionId</c>. <c>diverged</c>: stored, sequenced, head unchanged;
/// a person resolves it. <c>replayed</c>: a true retry; the stored outcome, no second revision.
/// </param>
public sealed record CommitResponse(
    string Outcome,
    Guid RevisionId,
    long Sequence,
    Guid? HeadRevisionId)
{
    public static string OutcomeName(CommitOutcome outcome) => outcome switch
    {
        CommitOutcome.FastForward => "fastForward",
        CommitOutcome.Diverged => "diverged",
        CommitOutcome.Replayed => "replayed",
        _ => outcome.ToString(),
    };
}

/// <param name="Resolution"><c>accept</c>, <c>reject</c> or <c>supersede</c> (Spec 089 §7.1).</param>
public sealed record ResolveRevisionRequest(string Resolution);

/// <param name="Resolved">False when the revision was not divergent, or was already resolved.</param>
public sealed record ResolveRevisionResponse(bool Resolved);

public sealed record BlobUploadResponse(string ContentHash, long SizeBytes, bool AlreadyPresent);

/// <summary>
/// Every refusal these endpoints make on purpose. <c>Code</c> is stable and machine-readable;
/// <c>Message</c> is for a log. <c>MissingHashes</c> accompanies <c>missing-content</c> only.
/// </summary>
public sealed record WorkspaceProblem(
    int Status,
    string Code,
    string Message,
    IReadOnlyList<string>? MissingHashes = null);
