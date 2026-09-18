namespace Aonik.SharedKernel.Abstractions.Workspaces;

/// <summary>
/// Workspace lifecycle (Spec 089 §11). Consumers reference this contract, never the module.
/// </summary>
public interface IWorkspaceService
{
    /// <param name="billingSubscriber">
    /// Who the workspace's slot, possession and byte quota are held against (Spec 089 §9). Defaults to the owning
    /// party. A consumer product passes the family's <c>Group</c> subscriber (Spec 087, "a family on a consumer
    /// product") so that a member's workspace is paid for by the family plan; the meter refuses a subscriber the
    /// current caller may not act for, so this cannot bill a stranger.
    /// </param>
    Task<WorkspaceSummary> CreateAsync(
        string kind,
        string name,
        Guid ownerPartyId,
        Subscriptions.SubscriberRef? billingSubscriber = null,
        CancellationToken cancellationToken = default);

    Task<WorkspaceSummary?> GetAsync(Guid workspaceId, CancellationToken cancellationToken = default);

    /// <summary>Workspaces this party owns. Grants are a separate question — see <see cref="IWorkspaceSyncService"/>.</summary>
    Task<IReadOnlyList<WorkspaceSummary>> ListForOwnerAsync(
        Guid ownerPartyId, CancellationToken cancellationToken = default);

    Task<bool> RenameAsync(
        Guid workspaceId, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Move ownership, and with it the byte claims.
    ///
    /// <para>
    /// <strong>Atomically or not at all</strong> (§9.2). A transfer that moves the workspace and leaves the claims
    /// behind bills the previous owner for storage they can no longer reach, and frees nothing when they try to
    /// clean up.
    /// </para>
    /// </summary>
    Task<bool> TransferOwnershipAsync(
        Guid workspaceId, Guid newOwnerPartyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete the workspace and return its held bytes.
    ///
    /// <para>
    /// Blobs are dereferenced, never deleted inline: another workspace in the same tenant may have deduped
    /// against them, and deleting bytes a second revision still names destroys data silently — discovered by a
    /// user opening an old revision to find it empty.
    /// </para>
    /// </summary>
    Task<bool> DeleteAsync(Guid workspaceId, CancellationToken cancellationToken = default);
}
