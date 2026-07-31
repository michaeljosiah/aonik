using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Persistence;
using Aonik.Subscriptions.Entities.Catalogue;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Subscriptions.Persistence;

/// <summary>
/// Spec 087 — module-scoped DbContext for subscriptions. Shares the same physical database as
/// <c>AonikDbContext</c> and every other module context; module contexts are runtime DI scoping
/// only, so the migration stream stays in <c>AonikDbContext</c> and this context declares
/// <b>no</b> migrations. Tables keep the platform-wide <c>Ank</c> prefix in <c>dbo</c>.
/// </summary>
internal sealed class SubscriptionsDbContext : AonikDbContextBase
{
    public DbSet<Meter> Meters => Set<Meter>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<PlanVersion> PlanVersions => Set<PlanVersion>();
    public DbSet<PlanEntitlement> PlanEntitlements => Set<PlanEntitlement>();

    public SubscriptionsDbContext(
        DbContextOptions<SubscriptionsDbContext> options,
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

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SubscriptionsDbContext).Assembly);

        ApplyDboPrefixedTableNames(modelBuilder);
        ConfigureRowVersions(modelBuilder);
        ApplyTenantQueryFilters(modelBuilder);
    }

    private static void ApplyDboPrefixedTableNames(ModelBuilder modelBuilder)
    {
        MapTable<Meter>(modelBuilder, "Meters");
        MapTable<Plan>(modelBuilder, "Plans");
        MapTable<PlanVersion>(modelBuilder, "PlanVersions");
        MapTable<PlanEntitlement>(modelBuilder, "PlanEntitlements");
    }

    private static void MapTable<TEntity>(ModelBuilder modelBuilder, string tableName)
        where TEntity : class
        => MapModuleTable<TEntity>(modelBuilder, ModuleTablePrefixes.Default, tableName);
}
