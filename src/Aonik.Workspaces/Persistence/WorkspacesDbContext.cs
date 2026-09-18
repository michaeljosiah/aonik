using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Persistence;
using Aonik.Workspaces.Entities;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Workspaces.Persistence;

/// <summary>
/// Spec 089 §10 — module-scoped DbContext for workspaces.
///
/// <para>
/// Shares the same physical database as every other module context. The canonical migration stream stays in
/// <c>AonikDbContext</c> and this declares none.
/// </para>
/// </summary>
internal sealed class WorkspacesDbContext : AonikDbContextBase, IWorkspaceDataContext
{
    public async Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
    {
        if (!Database.IsRelational())
        {
            return await action(cancellationToken);
        }

        // The retrying strategy owns the transaction: a transient fault replays the whole unit,
        // never half of it (the same shape as ConsentService's enrolment).
        var strategy = Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async ct =>
        {
            await using var transaction = await Database.BeginTransactionAsync(ct);
            var result = await action(ct);
            await transaction.CommitAsync(ct);
            return result;
        }, cancellationToken);
    }

    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<WorkspaceRevision> Revisions => Set<WorkspaceRevision>();
    public DbSet<WorkspaceFile> Files => Set<WorkspaceFile>();
    public DbSet<WorkspaceBlob> Blobs => Set<WorkspaceBlob>();
    public DbSet<BlobPossession> Possessions => Set<BlobPossession>();
    public DbSet<BlobUploadSession> UploadSessions => Set<BlobUploadSession>();
    public DbSet<BlobUploadPart> UploadParts => Set<BlobUploadPart>();
    public DbSet<WorkspaceOperation> Operations => Set<WorkspaceOperation>();

    public WorkspacesDbContext(
        DbContextOptions<WorkspacesDbContext> options,
        ITenantProvider? tenantProvider = null,
        ICurrentUserProvider? currentUserProvider = null,
        IClock? clock = null)
        : base(options, tenantProvider, currentUserProvider, clock)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema(SchemaNames.Default);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WorkspacesDbContext).Assembly);

        ApplyDboPrefixedTableNames(modelBuilder);
        ConfigureRowVersions(modelBuilder);
        ApplyTenantQueryFilters(modelBuilder);
    }

    private static void ApplyDboPrefixedTableNames(ModelBuilder modelBuilder)
    {
        MapTable<Workspace>(modelBuilder, "Workspaces");
        MapTable<WorkspaceRevision>(modelBuilder, "WorkspaceRevisions");
        MapTable<WorkspaceFile>(modelBuilder, "WorkspaceFiles");
        MapTable<WorkspaceBlob>(modelBuilder, "WorkspaceBlobs");
        MapTable<BlobPossession>(modelBuilder, "BlobPossessions");
        MapTable<BlobUploadSession>(modelBuilder, "BlobUploadSessions");
        MapTable<BlobUploadPart>(modelBuilder, "BlobUploadParts");
        MapTable<WorkspaceOperation>(modelBuilder, "WorkspaceOperations");
    }

    private static void MapTable<TEntity>(ModelBuilder modelBuilder, string tableName)
        where TEntity : class
        => MapModuleTable<TEntity>(modelBuilder, ModuleTablePrefixes.Default, tableName);
}
