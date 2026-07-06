using Aonik.Finance.Entities.Orders;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Ordering.Persistence;

/// <summary>
/// Spec 041 / ADR-011 Phase 3 — module-scoped DbContext for the generic Order spine.
/// Shares the same physical SQL Server database as <c>AonikDbContext</c> and the other module
/// contexts; module DbContexts are runtime-only DI scoping, so the migration stream stays in
/// <c>AonikDbContext</c> and this context declares <strong>no</strong> migrations. Order entities
/// keep their <c>Aonik.Finance.Entities.Orders</c> namespace (preserved on relocation), and the
/// tables keep their <c>Ank</c> prefix.
/// </summary>
internal sealed class OrderingDbContext : AonikDbContextBase
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderPartyRole> OrderPartyRoles => Set<OrderPartyRole>();
    public DbSet<OrderFundingRef> OrderFundingRefs => Set<OrderFundingRef>();
    public DbSet<OrderFulfilmentRef> OrderFulfilmentRefs => Set<OrderFulfilmentRef>();
    public DbSet<OrderHistoryEvent> OrderHistoryEvents => Set<OrderHistoryEvent>();
    public DbSet<OrderNote> OrderNotes => Set<OrderNote>();

    public OrderingDbContext(
        DbContextOptions<OrderingDbContext> options,
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

        // Order EF configurations now live in this assembly (preserved namespace).
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrderingDbContext).Assembly);

        ApplyDboPrefixedTableNames(modelBuilder);
        ConfigureRowVersions(modelBuilder);
        ApplyTenantQueryFilters(modelBuilder);
    }

    private static void ApplyDboPrefixedTableNames(ModelBuilder modelBuilder)
    {
        MapTable<Order>(modelBuilder, "Orders");
        MapTable<OrderItem>(modelBuilder, "OrderItems");
        MapTable<OrderPartyRole>(modelBuilder, "OrderPartyRoles");
        MapTable<OrderFundingRef>(modelBuilder, "OrderFundingRefs");
        MapTable<OrderFulfilmentRef>(modelBuilder, "OrderFulfilmentRefs");
        MapTable<OrderHistoryEvent>(modelBuilder, "OrderHistoryEvents");
        MapTable<OrderNote>(modelBuilder, "OrderNotes");
    }

    private static void MapTable<TEntity>(ModelBuilder modelBuilder, string tableName)
        where TEntity : class
        => MapModuleTable<TEntity>(modelBuilder, ModuleTablePrefixes.Finance, tableName);
}
