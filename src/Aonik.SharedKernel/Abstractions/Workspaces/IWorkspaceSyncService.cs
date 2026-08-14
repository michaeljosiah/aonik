namespace Aonik.SharedKernel.Abstractions.Workspaces;

/// <summary>
/// Negotiating content, committing revisions, and reading manifests (Spec 089 §11).
///
/// <para>
/// Every method here resolves the caller's <strong>effective access before acting</strong> (§8.1). That is not a
/// convenience for the product — it is the platform guarding its own endpoints, because a read-only recipient
/// calling the commit endpoint directly is a five-minute exercise and a product-side check has nothing behind it.
/// </para>
/// </summary>
public interface IWorkspaceSyncService
{
    /// <summary>
    /// The caller's effective access to this workspace:
    /// <c>Owner</c> if they are the owning party, otherwise the level on an active, unexpired, unrevoked grant,
    /// otherwise <see cref="WorkspaceAccessLevel.None"/>.
    /// </summary>
    Task<WorkspaceAccessLevel> ResolveAccessAsync(
        Guid workspaceId, Guid callerPartyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Which of these hashes the tenant does not already have.
    ///
    /// <para>
    /// Answered without transferring anything, which is what makes an unchanged 2GB workspace sync in one round
    /// trip and no bytes.
    /// </para>
    /// </summary>
    Task<BlobNegotiationResult> NegotiateAsync(
        Guid workspaceId,
        Guid callerPartyId,
        IReadOnlyList<string> contentHashes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Append a revision from a complete manifest.
    ///
    /// <para>
    /// Requires <see cref="WorkspaceAccessLevel.Write"/>. Refuses a manifest naming a hash the tenant does not
    /// possess — upload first, commit second, never the reverse.
    /// </para>
    /// </summary>
    /// <exception cref="CommitIdReusedException">
    /// The <c>CommitId</c> was used for a <em>different</em> tree. Loud, deliberately: replaying the original
    /// outcome would tell a client its new work is committed when it is not, and the next pull would treat those
    /// edits as absent — work lost silently with a success response on the record.
    /// </exception>
    Task<CommitRevisionResult> CommitAsync(
        CommitRevisionRequest request,
        Guid callerPartyId,
        CancellationToken cancellationToken = default);

    /// <summary>The manifest of a revision, or of the head when none is named. Requires <c>Read</c>.</summary>
    Task<IReadOnlyList<ManifestEntry>> GetManifestAsync(
        Guid workspaceId,
        Guid callerPartyId,
        Guid? revisionId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RevisionSummary>> GetHistoryAsync(
        Guid workspaceId,
        Guid callerPartyId,
        int take = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// End a divergent revision (§7.1).
    ///
    /// <para>
    /// Accepting advances the head through a <strong>new revision parented on the current head</strong> — never
    /// by rewriting history, so what actually happened stays readable. Rejecting releases its blobs and byte
    /// claims after the retention window.
    /// </para>
    /// </summary>
    Task<bool> ResolveAsync(
        Guid revisionId,
        Guid callerPartyId,
        DivergenceResolution resolution,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A <c>CommitId</c> was reused for a different tree (Spec 089 §6.1.1).
/// </summary>
public sealed class CommitIdReusedException : Exception
{
    public CommitIdReusedException(Guid commitId)
        : base($"CommitId {commitId} was used for a different tree; use a new one.")
        => CommitId = commitId;

    public Guid CommitId { get; }
}

/// <summary>The caller's effective access is below what the operation requires (Spec 089 §8.1).</summary>
public sealed class WorkspaceAccessDeniedException : Exception
{
    public WorkspaceAccessDeniedException(
        Guid workspaceId, Guid callerPartyId, WorkspaceAccessLevel required, WorkspaceAccessLevel effective)
        : base($"Party {callerPartyId} has {effective} access to workspace {workspaceId}; {required} is required.")
    {
        WorkspaceId = workspaceId;
        CallerPartyId = callerPartyId;
        Required = required;
        Effective = effective;
    }

    public Guid WorkspaceId { get; }
    public Guid CallerPartyId { get; }
    public WorkspaceAccessLevel Required { get; }
    public WorkspaceAccessLevel Effective { get; }
}
