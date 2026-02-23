using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Persistence;

/// <summary>
/// Module-scoped DbContext for the Finance domain.
/// Owns Ledger, Payments, Billing, Orders, Pricing, Partners, and PersonalFinance entities.
/// Inherits multi-tenancy enforcement and audit stamping from <see cref="AonikDbContextBase"/>.
///
/// During migration, entities are progressively moved here from AonikDbContext.
/// Both contexts share the same physical SQL Server database.
/// </summary>
internal class FinanceDbContext : AonikDbContextBase
{
    // DbSets will be added as entities are migrated in PRs 2.2–2.4

    public FinanceDbContext(
        DbContextOptions<FinanceDbContext> options,
        ITenantProvider? tenantProvider = null,
        ICurrentUserProvider? currentUserProvider = null,
        IClock? clock = null)
        : base(options, tenantProvider, currentUserProvider, clock)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // All Finance entities use the 'finance' schema
        modelBuilder.HasDefaultSchema(SchemaNames.Finance);

        // Apply EF configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FinanceDbContext).Assembly);

        // Apply tenant query filters for all ITenantScoped entities in this context
        ApplyTenantQueryFilters(modelBuilder);
    }
}
