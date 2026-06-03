using Aonik.Documents.Entities;
using Aonik.Platform.Entities.Compliance; // Document/DocumentFile/DocumentVersion — namespace preserved per Spec 035 to protect the EF snapshot FQN
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Documents.Persistence;

/// <summary>
/// Module-scoped DbContext for the Documents domain (Spec 035 §18). Owns the
/// generic-document sets. Shares the same physical SQL Server database as
/// <c>AonikDbContext</c> (the canonical migration stream) — module DbContexts are
/// runtime-only DI scoping and declare NO migrations, per
/// <a href="../../../docs/decisions/005-adopt-module-first-modular-monolith.md">ADR-005</a>.
///
/// Phase 1 (Spec 035): currently owns the new ingestion/extraction entities. The
/// generic <c>Document</c> / <c>DocumentFile</c> / <c>DocumentVersion</c> sets relocate
/// onto this context in the entity-move commit on this branch.
/// </summary>
internal sealed class DocumentsDbContext : AonikDbContextBase
{
    public DbSet<Document> Documents { get; set; } = null!;
    public DbSet<DocumentFile> DocumentFiles { get; set; } = null!;
    public DbSet<DocumentVersion> DocumentVersions { get; set; } = null!;
    public DbSet<DocumentIngestion> DocumentIngestions { get; set; } = null!;
    public DbSet<DocumentExtraction> DocumentExtractions { get; set; } = null!;

    public DocumentsDbContext(
        DbContextOptions<DocumentsDbContext> options,
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

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DocumentsDbContext).Assembly);

        ApplyDboPrefixedTableNames(modelBuilder);

        ConfigureRowVersions(modelBuilder);

        ApplyTenantQueryFilters(modelBuilder);
    }

    private static void ApplyDboPrefixedTableNames(ModelBuilder modelBuilder)
    {
        MapTable<Document>(modelBuilder, "Documents");
        MapTable<DocumentFile>(modelBuilder, "DocumentFiles");
        MapTable<DocumentVersion>(modelBuilder, "DocumentVersions");
        MapTable<DocumentIngestion>(modelBuilder, "DocumentIngestions");
        MapTable<DocumentExtraction>(modelBuilder, "DocumentExtractions");
    }

    private static void MapTable<TEntity>(ModelBuilder modelBuilder, string tableName)
        where TEntity : class
        => modelBuilder.Entity<TEntity>()
            .ToTable($"{ModuleTablePrefixes.Default}{tableName}", SchemaNames.Default);
}
