using Aonik.Platform.Entities.Compliance;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Entities.Notifications;
using Aonik.Platform.Entities.Operations;
using Aonik.Platform.Entities.Party;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using PartyEntity = Aonik.Platform.Entities.Party.Party;

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

    // Party
    public DbSet<PartyEntity> Parties { get; set; } = null!;
    public DbSet<PartyAddress> PartyAddresses { get; set; } = null!;
    public DbSet<PartyContact> PartyContacts { get; set; } = null!;
    public DbSet<PartyConsent> PartyConsents { get; set; } = null!;
    public DbSet<PersonProfile> PersonProfiles { get; set; } = null!;
    public DbSet<BusinessProfile> BusinessProfiles { get; set; } = null!;
    public DbSet<ExternalAccount> ExternalAccounts { get; set; } = null!;
    public DbSet<PartyRoleAssignment> PartyRoleAssignments { get; set; } = null!;
    public DbSet<PartyRelationship> PartyRelationships { get; set; } = null!;

    // Compliance
    public DbSet<ScreeningCheck> ScreeningChecks { get; set; } = null!;
    public DbSet<ComplianceCase> ComplianceCases { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;
    public DbSet<Document> Documents { get; set; } = null!;
    public DbSet<DocumentFile> DocumentFiles { get; set; } = null!;
    public DbSet<DocumentUsage> DocumentUsages { get; set; } = null!;
    public DbSet<DocumentVerification> DocumentVerifications { get; set; } = null!;
    public DbSet<DocumentVersion> DocumentVersions { get; set; } = null!;

    // Notifications
    public DbSet<Notification> Notifications { get; set; } = null!;
    public DbSet<NotificationTemplate> NotificationTemplates { get; set; } = null!;
    public DbSet<NotificationTemplateBinding> NotificationTemplateBindings { get; set; } = null!;
    public DbSet<WebhookSubscription> WebhookSubscriptions { get; set; } = null!;

    // Operations
    public DbSet<WorkItem> WorkItems { get; set; } = null!;
    public DbSet<Job> Jobs { get; set; } = null!;

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

        // NotificationTemplate has nullable TenantId (shared + tenant-specific templates)
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(NotificationTemplate));
    }

    protected override bool IsGlobalEntity(object entity)
    {
        return entity is Role;
    }
}
