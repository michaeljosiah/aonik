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

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
