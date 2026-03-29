using Aonik.Platform.Entities.Autonumbering;
using Aonik.Platform.Entities.Cms;
using Aonik.Platform.Entities.Compliance;
using Aonik.Platform.Entities.Features;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Entities.Notifications;
using Aonik.Platform.Entities.Operations;
using Aonik.Platform.Entities.Party;
using Aonik.Platform.Entities.ReferenceData;
using Aonik.Platform.Entities.Settings;
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
    public DbSet<PartyAccount> PartyAccounts { get; set; } = null!;
    public DbSet<PartyRoleAssignment> PartyRoleAssignments { get; set; } = null!;
    public DbSet<PartyRelationship> PartyRelationships { get; set; } = null!;
    public DbSet<NotificationPreference> NotificationPreferences { get; set; } = null!;
    public DbSet<MarketingPreference> MarketingPreferences { get; set; } = null!;

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

    // Settings
    public DbSet<Setting> Settings { get; set; } = null!;

    // Features
    public DbSet<TenantFeature> TenantFeatures { get; set; } = null!;

    // Reference Data
    public DbSet<ReferenceDataItem> ReferenceDataItems { get; set; } = null!;
    public DbSet<Country> Countries { get; set; } = null!;
    public DbSet<Currency> Currencies { get; set; } = null!;
    public DbSet<CountryCurrency> CountryCurrencies { get; set; } = null!;

    // CMS
    public DbSet<ContentBlock> ContentBlocks { get; set; } = null!;
    public DbSet<ContentBlockMedia> ContentBlockMedia { get; set; } = null!;

    // Autonumbering
    public DbSet<AutonumberProfile> AutonumberProfiles { get; set; } = null!;
    public DbSet<AutonumberReservation> AutonumberReservations { get; set; } = null!;

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

        modelBuilder.HasDefaultSchema(SchemaNames.Default);

        // Apply EF configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlatformDbContext).Assembly);

        ApplyDboPrefixedTableNames(modelBuilder);

        // Configure RowVersion as optimistic concurrency token on all AuditableEntity types
        ConfigureRowVersions(modelBuilder);

        // Apply tenant query filters for all ITenantScoped entities in this context
        ApplyTenantQueryFilters(modelBuilder);

        // NotificationTemplate has nullable TenantId (shared + tenant-specific templates)
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(NotificationTemplate));

        // ReferenceData entities have nullable TenantId (global + tenant-specific)
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(ReferenceDataItem));
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(Country));
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(Currency));
    }

    protected override bool IsGlobalEntity(object entity)
    {
        return entity is Role;
    }

    private static void ApplyDboPrefixedTableNames(ModelBuilder modelBuilder)
    {
        MapTable<Tenant>(modelBuilder, "Tenants");
        MapTable<TenantCountry>(modelBuilder, "TenantCountries");
        MapTable<TenantCurrency>(modelBuilder, "TenantCurrencies");
        MapTable<User>(modelBuilder, "Users");
        MapTable<Role>(modelBuilder, "Roles");
        MapTable<Permission>(modelBuilder, "Permissions");
        MapTable<UserRole>(modelBuilder, "UserRoles");
        MapTable<RolePermission>(modelBuilder, "RolePermissions");
        MapTable<UserParty>(modelBuilder, "UserParties");
        MapTable<VerificationChallenge>(modelBuilder, "VerificationChallenges");

        MapTable<PartyEntity>(modelBuilder, "Parties");
        MapTable<PartyAddress>(modelBuilder, "PartyAddresses");
        MapTable<PartyContact>(modelBuilder, "PartyContacts");
        MapTable<PartyConsent>(modelBuilder, "PartyConsents");
        MapTable<PersonProfile>(modelBuilder, "PersonProfiles");
        MapTable<BusinessProfile>(modelBuilder, "BusinessProfiles");
        MapTable<PartyAccount>(modelBuilder, "PartyAccounts");
        MapTable<PartyRoleAssignment>(modelBuilder, "PartyRoleAssignments");
        MapTable<PartyRelationship>(modelBuilder, "PartyRelationships");
        MapTable<NotificationPreference>(modelBuilder, "NotificationPreferences");
        MapTable<MarketingPreference>(modelBuilder, "MarketingPreferences");

        MapTable<ScreeningCheck>(modelBuilder, "ScreeningChecks");
        MapTable<ComplianceCase>(modelBuilder, "ComplianceCases");
        MapTable<AuditLog>(modelBuilder, "AuditLogs");
        MapTable<Document>(modelBuilder, "Documents");
        MapTable<DocumentFile>(modelBuilder, "DocumentFiles");
        MapTable<DocumentUsage>(modelBuilder, "DocumentUsages");
        MapTable<DocumentVerification>(modelBuilder, "DocumentVerifications");
        MapTable<DocumentVersion>(modelBuilder, "DocumentVersions");

        MapTable<Notification>(modelBuilder, "Notifications");
        MapTable<NotificationTemplate>(modelBuilder, "NotificationTemplates");
        MapTable<NotificationTemplateBinding>(modelBuilder, "NotificationTemplateBindings");
        MapTable<WebhookSubscription>(modelBuilder, "WebhookSubscriptions");

        MapTable<WorkItem>(modelBuilder, "WorkItems");
        MapTable<Job>(modelBuilder, "Jobs");

        MapTable<Setting>(modelBuilder, "Settings");
        MapTable<TenantFeature>(modelBuilder, "TenantFeatures");

        MapTable<ReferenceDataItem>(modelBuilder, "ReferenceData");
        MapTable<Country>(modelBuilder, "Countries");
        MapTable<Currency>(modelBuilder, "Currencies");
        MapTable<CountryCurrency>(modelBuilder, "CountryCurrencies");

        MapTable<ContentBlock>(modelBuilder, "ContentBlocks");
        MapTable<ContentBlockMedia>(modelBuilder, "ContentBlockMedia");

        MapTable<AutonumberProfile>(modelBuilder, "AutonumberProfiles");
        MapTable<AutonumberReservation>(modelBuilder, "AutonumberReservations");
    }

    private static void MapTable<TEntity>(ModelBuilder modelBuilder, string tableName)
        where TEntity : class
    {
        modelBuilder.Entity<TEntity>()
            .ToTable($"{ModuleTablePrefixes.Platform}{tableName}", SchemaNames.Default);
    }
}
