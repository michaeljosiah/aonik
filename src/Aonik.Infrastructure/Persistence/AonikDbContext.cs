using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Domain.Agents.Entities;
using Aonik.Domain.Autonumbering.Entities;
using Aonik.Domain.Catalog.Entities;
using Aonik.Domain.Cms.Entities;
using Aonik.Platform.Entities.Compliance;
using Aonik.Platform.Entities.Features;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Entities.Notifications;
using Aonik.Platform.Entities.Operations;
using Aonik.Platform.Entities.Party;
using Aonik.Domain.PersonalFinance.Entities;
using Aonik.Platform.Entities.ReferenceData;
using Aonik.Platform.Entities.Settings;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using LedgerEntity = Aonik.Finance.Entities.Ledger.Ledger;
using PartyEntity = Aonik.Platform.Entities.Party.Party;

namespace Aonik.Infrastructure.Persistence;

public class AonikDbContext : AonikDbContextBase, IAonikDbContext
{
    // Identity
    public virtual DbSet<Tenant> Tenants { get; set; } = null!;
    public virtual DbSet<TenantCountry> TenantCountries { get; set; } = null!;
    public virtual DbSet<TenantCurrency> TenantCurrencies { get; set; } = null!;
    public virtual DbSet<User> Users { get; set; } = null!;
    public virtual DbSet<Role> Roles { get; set; } = null!;
    public virtual DbSet<Permission> Permissions { get; set; } = null!;
    public virtual DbSet<UserRole> UserRoles { get; set; } = null!;
    public virtual DbSet<RolePermission> RolePermissions { get; set; } = null!;
    public virtual DbSet<UserParty> UserParties { get; set; } = null!;
    public virtual DbSet<VerificationChallenge> VerificationChallenges { get; set; } = null!;

    // Autonumbering
    public virtual DbSet<AutonumberProfile> AutonumberProfiles { get; set; } = null!;
    public virtual DbSet<AutonumberReservation> AutonumberReservations { get; set; } = null!;

    public virtual DbSet<Setting> Settings { get; set; } = null!;
    public virtual DbSet<ReferenceDataItem> ReferenceDataItems { get; set; } = null!;
    public virtual DbSet<Country> Countries { get; set; } = null!;
    public virtual DbSet<Currency> Currencies { get; set; } = null!;
    public virtual DbSet<CountryCurrency> CountryCurrencies { get; set; } = null!;

    // Party
    public virtual DbSet<PartyEntity> Parties { get; set; } = null!;
    public virtual DbSet<PartyAddress> PartyAddresses { get; set; } = null!;
    public virtual DbSet<PartyContact> PartyContacts { get; set; } = null!;
    public virtual DbSet<PartyConsent> PartyConsents { get; set; } = null!;
    public virtual DbSet<PersonProfile> PersonProfiles { get; set; } = null!;
    public virtual DbSet<BusinessProfile> BusinessProfiles { get; set; } = null!;
    public virtual DbSet<ExternalAccount> ExternalAccounts { get; set; } = null!;
    public virtual DbSet<PartyRoleAssignment> PartyRoleAssignments { get; set; } = null!;
    public virtual DbSet<PartyRelationship> PartyRelationships { get; set; } = null!;

    // CMS
    public virtual DbSet<ContentBlock> ContentBlocks { get; set; } = null!;
    public virtual DbSet<ContentBlockMedia> ContentBlockMedia { get; set; } = null!;

    // Catalog
    public virtual DbSet<CatalogBillerCategory> CatalogBillerCategories { get; set; } = null!;
    public virtual DbSet<CatalogBiller> CatalogBillers { get; set; } = null!;
    public virtual DbSet<CatalogBillerService> CatalogBillerServices { get; set; } = null!;

    // Compliance
    public virtual DbSet<ScreeningCheck> ScreeningChecks { get; set; } = null!;
    public virtual DbSet<ComplianceCase> ComplianceCases { get; set; } = null!;
    public virtual DbSet<AuditLog> AuditLogs { get; set; } = null!;
    public virtual DbSet<Document> Documents { get; set; } = null!;
    public virtual DbSet<DocumentFile> DocumentFiles { get; set; } = null!;
    public virtual DbSet<DocumentUsage> DocumentUsages { get; set; } = null!;
    public virtual DbSet<DocumentVerification> DocumentVerifications { get; set; } = null!;
    public virtual DbSet<DocumentVersion> DocumentVersions { get; set; } = null!;

    // Features
    public virtual DbSet<TenantFeature> TenantFeatures { get; set; } = null!;

    // Operations
    public virtual DbSet<WorkItem> WorkItems { get; set; } = null!;
    public virtual DbSet<Job> Jobs { get; set; } = null!;

    // Notifications
    public virtual DbSet<Notification> Notifications { get; set; } = null!;
    public virtual DbSet<NotificationTemplate> NotificationTemplates { get; set; } = null!;
    public virtual DbSet<NotificationTemplateBinding> NotificationTemplateBindings { get; set; } = null!;
    public virtual DbSet<WebhookSubscription> WebhookSubscriptions { get; set; } = null!;

    // Agents
    public virtual DbSet<Agent> Agents { get; set; } = null!;
    public virtual DbSet<AgentRun> AgentRuns { get; set; } = null!;
    public virtual DbSet<OrchestratorPolicy> OrchestratorPolicies { get; set; } = null!;
    public virtual DbSet<Proposal> Proposals { get; set; } = null!;

    // Personal Finance
    public virtual DbSet<PersonalProfile> PersonalProfiles { get; set; } = null!;
    public virtual DbSet<Household> Households { get; set; } = null!;
    public virtual DbSet<HouseholdMember> HouseholdMembers { get; set; } = null!;
    public virtual DbSet<PersonalAccount> PersonalAccounts { get; set; } = null!;
    public virtual DbSet<PersonalTransaction> PersonalTransactions { get; set; } = null!;
    public virtual DbSet<CategorisationRule> CategorisationRules { get; set; } = null!;
    public virtual DbSet<BudgetLine> BudgetLines { get; set; } = null!;
    public virtual DbSet<Bill> Bills { get; set; } = null!;
    public virtual DbSet<Subscription> Subscriptions { get; set; } = null!;
    public virtual DbSet<Goal> Goals { get; set; } = null!;
    public virtual DbSet<Budget> Budgets { get; set; } = null!;

    public AonikDbContext(
        DbContextOptions<AonikDbContext> options,
        ITenantProvider? tenantProvider = null,
        ICurrentUserProvider? currentUserProvider = null,
        IClock? clock = null)
        : base(options, tenantProvider, currentUserProvider, clock)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Apply Identity configurations from Platform assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Tenant).Assembly);

        // Apply Finance configurations from Finance assembly (required for EF migrations)
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LedgerEntity).Assembly);

        // Apply AI configurations from Ai assembly (required for EF migrations)
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Aonik.Ai.Entities.AiProvider).Assembly);

        // Apply tenant query filters
        ApplyTenantQueryFilters(modelBuilder);

        // Apply nullable tenant filters for entities with optional TenantId
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(Agent));
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(OrchestratorPolicy));
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(ReferenceDataItem));
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(Country));
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(Currency));
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(NotificationTemplate));
    }

    protected override bool IsGlobalEntity(object entity)
    {
        return entity is Role;
    }
}
