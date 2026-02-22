using Aonik.Platform.Entities.Identity;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Platform.Persistence;

/// <summary>
/// Module-scoped DbContext for the Platform domain.
/// Owns Identity, Tenancy, Party/Profile, Compliance, Notifications, Operations entities.
/// Inherits multi-tenancy enforcement and audit stamping from <see cref="AonikDbContextBase"/>.
/// 
/// During migration, entities are progressively moved here from AonikDbContext.
/// Both contexts share the same physical SQL Server database.
/// </summary>
internal class PlatformDbContext : AonikDbContextBase
{
    // Identity
    public DbSet<Tenant> Tenants { get; set; } = null!;
    public DbSet<TenantCountry> TenantCountries { get; set; } = null!;
    public DbSet<TenantCurrency> TenantCurrencies { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Role> Roles { get; set; } = null!;
    public DbSet<Permission> Permissions { get; set; } = null!;
    public DbSet<UserRole> UserRoles { get; set; } = null!;
    public DbSet<RolePermission> RolePermissions { get; set; } = null!;
    public DbSet<UserParty> UserParties { get; set; } = null!;
    public DbSet<VerificationChallenge> VerificationChallenges { get; set; } = null!;

    public PlatformDbContext(
        DbContextOptions<PlatformDbContext> options,
        ITenantProvider? tenantProvider = null,
        ICurrentUserProvider? currentUserProvider = null,
        IClock? clock = null)
        : base(options, tenantProvider, currentUserProvider, clock)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // All Platform entities use the 'platform' schema
        modelBuilder.HasDefaultSchema(SchemaNames.Platform);

        // Apply EF configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlatformDbContext).Assembly);

        // Apply tenant query filters for all ITenantScoped entities in this context
        ApplyTenantQueryFilters(modelBuilder);
    }

    protected override bool IsGlobalEntity(object entity)
    {
        return entity is Role;
    }
}
