using Aonik.Workspaces.Entities;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Workspaces.Persistence;

/// <summary>
/// The unit of work the workspace services write through (Spec 089 §10).
///
/// <para>
/// It exists because <c>RefCount</c> must be maintained <strong>in the same transaction as the manifest
/// write</strong> (§5). A manifest that lands while its reference counts do not is a workspace whose bytes the
/// sweeper is entitled to delete — and that failure is invisible until someone opens an old revision and finds
/// it empty.
/// </para>
/// </summary>
public interface IWorkspaceDataContext
{
    DbSet<Workspace> Workspaces { get; }
    DbSet<WorkspaceRevision> Revisions { get; }
    DbSet<WorkspaceFile> Files { get; }
    DbSet<WorkspaceBlob> Blobs { get; }
    DbSet<BlobPossession> Possessions { get; }
    DbSet<BlobUploadSession> UploadSessions { get; }
    DbSet<BlobUploadPart> UploadParts { get; }
    DbSet<WorkspaceOperation> Operations { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Run <paramref name="action"/> inside one database transaction, retried whole under the
    /// provider's execution strategy, so that everything it writes lands together or not at all
    /// (aonik#322). On a store with no transactions the action simply runs.
    /// </summary>
    Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default);
}
