using Aonik.PersonalFinance.Entities;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Groups.Persistence;

/// <summary>
/// Spec 086 — module-scoped DbContext for groups and sharing. Shares the same physical database as
/// every other module context; the migration stream stays in <c>AonikDbContext</c> and this
/// declares none.
///
/// The entity types keep their <c>Aonik.PersonalFinance.Entities</c> namespace on relocation — the
/// ADR-006 / Spec 027 technique that <c>Aonik.Ordering</c> also used. That keeps the EF model
/// snapshot's fully-qualified names valid, so the move itself needs no migration and no consumer
/// changes a single <c>using</c>. Renaming to Group / ShareGrant is a later, purely cosmetic pass.
/// </summary>
internal sealed class GroupsDbContext : AonikDbContextBase
{
    public DbSet<Household> Groups => Set<Household>();
    public DbSet<HouseholdMember> GroupMembers => Set<HouseholdMember>();
    public DbSet<CircleGrant> ShareGrants => Set<CircleGrant>();
    public DbSet<CircleInvite> ShareInvites => Set<CircleInvite>();

    public GroupsDbContext(
        DbContextOptions<GroupsDbContext> options,
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
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GroupsDbContext).Assembly);

        ApplyDboPrefixedTableNames(modelBuilder);
        ConfigureRowVersions(modelBuilder);
        ApplyTenantQueryFilters(modelBuilder);
    }

    private static void ApplyDboPrefixedTableNames(ModelBuilder modelBuilder)
    {
        // Table names are PINNED to what they already are. Renaming storage would turn a code move
        // into a data migration, for no functional gain.
        MapTable<Household>(modelBuilder, "Households");
        MapTable<HouseholdMember>(modelBuilder, "HouseholdMembers");
        MapTable<CircleGrant>(modelBuilder, "CircleGrants");
        MapTable<CircleInvite>(modelBuilder, "CircleInvites");
    }

    private static void MapTable<TEntity>(ModelBuilder modelBuilder, string tableName)
        where TEntity : class
        => MapModuleTable<TEntity>(modelBuilder, ModuleTablePrefixes.Default, tableName);
}
