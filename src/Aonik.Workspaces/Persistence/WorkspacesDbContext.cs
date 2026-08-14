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
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<WorkspaceRevision> Revisions => Set<WorkspaceRevision>();
    public DbSet<WorkspaceFile> Files => Set<WorkspaceFile>();
    public DbSet<WorkspaceBlob> Blobs => Set<WorkspaceBlob>();
    public DbSet<BlobPossession> Possessions => Set<BlobPossession>();

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
    }

    private static void MapTable<TEntity>(ModelBuilder modelBuilder, string tableName)
        where TEntity : class
        => MapModuleTable<TEntity>(modelBuilder, ModuleTablePrefixes.Default, tableName);
}
