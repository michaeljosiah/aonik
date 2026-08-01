using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Persistence;
using Aonik.Subscriptions.Entities.Catalogue;
using Aonik.Subscriptions.Entities.Subscriptions;
using Aonik.Subscriptions.Entities.Usage;

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

    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<SubscriptionPeriod> SubscriptionPeriods => Set<SubscriptionPeriod>();
    public DbSet<EntitlementGrant> EntitlementGrants => Set<EntitlementGrant>();
    public DbSet<UsageReservation> UsageReservations => Set<UsageReservation>();
    public DbSet<UsageReservationAllocation> UsageReservationAllocations => Set<UsageReservationAllocation>();
    public DbSet<UsageRecord> UsageRecords => Set<UsageRecord>();
    public DbSet<CeilingHolding> CeilingHoldings => Set<CeilingHolding>();
    public DbSet<CeilingClaim> CeilingClaims => Set<CeilingClaim>();

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
        MapTable<Subscription>(modelBuilder, "PlanSubscriptions");
        MapTable<SubscriptionPeriod>(modelBuilder, "SubscriptionPeriods");
        MapTable<EntitlementGrant>(modelBuilder, "EntitlementGrants");
        MapTable<UsageReservation>(modelBuilder, "UsageReservations");
        MapTable<UsageReservationAllocation>(modelBuilder, "UsageReservationAllocations");
        MapTable<UsageRecord>(modelBuilder, "UsageRecords");
        MapTable<CeilingHolding>(modelBuilder, "CeilingHoldings");
        MapTable<CeilingClaim>(modelBuilder, "CeilingClaims");
    }

    private static void MapTable<TEntity>(ModelBuilder modelBuilder, string tableName)
        where TEntity : class
        => MapModuleTable<TEntity>(modelBuilder, ModuleTablePrefixes.Default, tableName);
}
