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

// Cross-module entity imports (temporary — will be removed when Finance/AI modules are extracted)
using Aonik.Domain.Ai.Entities;
using Aonik.Finance.Entities.Ledger;
using Aonik.Finance.Entities.Orders;
using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Entities.Pricing;
using LedgerEntity = Aonik.Finance.Entities.Ledger.Ledger;

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

    // Settings
    public DbSet<Setting> Settings { get; set; } = null!;

    // Features
    public DbSet<TenantFeature> TenantFeatures { get; set; } = null!;

    // Reference Data
    public DbSet<ReferenceDataItem> ReferenceDataItems { get; set; } = null!;
    public DbSet<Country> Countries { get; set; } = null!;
    public DbSet<Currency> Currencies { get; set; } = null!;
    public DbSet<CountryCurrency> CountryCurrencies { get; set; } = null!;

    // Cross-module DbSets (temporary — used by TenantProvisioner and CustomerAdminService
    // during migration. Will be removed when Finance/AI modules are extracted and these
    // services are refactored to use cross-module contracts instead of direct DB access.)
    public DbSet<AiRoutePolicy> AiRoutePolicies { get; set; } = null!;
    public DbSet<FeePolicy> FeePolicies { get; set; } = null!;
    public DbSet<LimitsPolicy> LimitsPolicies { get; set; } = null!;
    public DbSet<Order> Orders { get; set; } = null!;
    public DbSet<OrderPartyRole> OrderPartyRoles { get; set; } = null!;
    public DbSet<PaymentIntent> PaymentIntents { get; set; } = null!;
    public DbSet<LedgerEntity> Ledgers { get; set; } = null!;
    public DbSet<LedgerAccount> LedgerAccounts { get; set; } = null!;

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

        // Cross-module entities use default schema (not platform) — they are owned by
        // other modules and only temporarily registered here for direct DB access.
        // No EF configurations are applied; they use convention-based mapping only.
        modelBuilder.Entity<AiRoutePolicy>().ToTable("AiRoutePolicies", SchemaNames.Default);
        modelBuilder.Entity<FeePolicy>().ToTable("FeePolicies", SchemaNames.Default);
        modelBuilder.Entity<LimitsPolicy>().ToTable("LimitsPolicies", SchemaNames.Default);
        modelBuilder.Entity<Order>().ToTable("Orders", SchemaNames.Default);
        modelBuilder.Entity<OrderPartyRole>().ToTable("OrderPartyRoles", SchemaNames.Default);
        modelBuilder.Entity<PaymentIntent>().ToTable("PaymentIntents", SchemaNames.Default);
        modelBuilder.Entity<LedgerEntity>().ToTable("Ledgers", SchemaNames.Default);
        modelBuilder.Entity<LedgerAccount>().ToTable("LedgerAccounts", SchemaNames.Default);

        // Apply tenant query filters for all ITenantScoped entities in this context
        ApplyTenantQueryFilters(modelBuilder);

        // NotificationTemplate has nullable TenantId (shared + tenant-specific templates)
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(NotificationTemplate));

        // ReferenceData entities have nullable TenantId (global + tenant-specific)
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(ReferenceDataItem));
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(Country));
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(Currency));

        // AiRoutePolicy has nullable TenantId
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(AiRoutePolicy));
    }

    protected override bool IsGlobalEntity(object entity)
    {
        return entity is Role;
    }
}
