using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Platform.Entities.Autonumbering;
using Aonik.Finance.Entities.Catalog;
using Aonik.Platform.Entities.Cms;
using Aonik.Platform.Entities.Compliance;
using Aonik.Platform.Entities.Features;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Entities.Notifications;
using Aonik.Platform.Entities.Operations;
using Aonik.Platform.Entities.Party;
using Aonik.Finance.Entities.Billing;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Entities.Ledger;
using Aonik.Finance.Entities.Orders;
using Aonik.Finance.Entities.Partners;
using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Entities.Pricing;
using Aonik.Platform.Entities.ReferenceData;
using Aonik.Platform.Entities.Settings;
using Aonik.Ai.Entities;
using Aonik.Agents.Entities;
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

    // Personal Finance
    public virtual DbSet<PersonalProfile> PersonalProfiles { get; set; } = null!;
    public virtual DbSet<Household> Households { get; set; } = null!;
    public virtual DbSet<HouseholdMember> HouseholdMembers { get; set; } = null!;
    public virtual DbSet<FinancialConnectionSession> FinancialConnectionSessions { get; set; } = null!;
    public virtual DbSet<FinancialConnection> FinancialConnections { get; set; } = null!;
    public virtual DbSet<FinancialLinkedAccount> FinancialLinkedAccounts { get; set; } = null!;
    public virtual DbSet<PersonalAccount> PersonalAccounts { get; set; } = null!;
    public virtual DbSet<PersonalTransaction> PersonalTransactions { get; set; } = null!;
    public virtual DbSet<CategorisationRule> CategorisationRules { get; set; } = null!;
    public virtual DbSet<BudgetLine> BudgetLines { get; set; } = null!;
    public virtual DbSet<Bill> Bills { get; set; } = null!;
    public virtual DbSet<Subscription> Subscriptions { get; set; } = null!;
    public virtual DbSet<Goal> Goals { get; set; } = null!;
    public virtual DbSet<Budget> Budgets { get; set; } = null!;
    public virtual DbSet<StatementImport> StatementImports { get; set; } = null!;
    public virtual DbSet<StatementImportRow> StatementImportRows { get; set; } = null!;

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

        modelBuilder.HasDefaultSchema(SchemaNames.Default);

        // Apply all configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Apply Identity configurations from Platform assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Tenant).Assembly);

        // Apply Finance configurations from Finance assembly (required for EF migrations)
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LedgerEntity).Assembly);

        // Apply AI configurations from Ai assembly (required for EF migrations)
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Aonik.Ai.Entities.AiProvider).Assembly);

        // Apply Agents configurations from Agents assembly (required for EF migrations)
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Aonik.Agents.Entities.Agent).Assembly);

        // Apply tenant query filters
        ApplyTenantQueryFilters(modelBuilder);

        // Apply nullable tenant filters for entities with optional TenantId
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(Aonik.Agents.Entities.Agent));
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(Aonik.Agents.Entities.OrchestratorPolicy));
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(ReferenceDataItem));
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(Country));
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(Currency));
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(NotificationTemplate));

        ApplyDboPrefixedTableNames(modelBuilder);
    }

    protected override bool IsGlobalEntity(object entity)
    {
        return entity is Role;
    }

    private static void ApplyDboPrefixedTableNames(ModelBuilder modelBuilder)
    {
        MapPlatformTable<Tenant>(modelBuilder, "Tenants");
        MapPlatformTable<TenantCountry>(modelBuilder, "TenantCountries");
        MapPlatformTable<TenantCurrency>(modelBuilder, "TenantCurrencies");
        MapPlatformTable<User>(modelBuilder, "Users");
        MapPlatformTable<Role>(modelBuilder, "Roles");
        MapPlatformTable<Permission>(modelBuilder, "Permissions");
        MapPlatformTable<UserRole>(modelBuilder, "UserRoles");
        MapPlatformTable<RolePermission>(modelBuilder, "RolePermissions");
        MapPlatformTable<UserParty>(modelBuilder, "UserParties");
        MapPlatformTable<VerificationChallenge>(modelBuilder, "VerificationChallenges");

        MapPlatformTable<AutonumberProfile>(modelBuilder, "AutonumberProfiles");
        MapPlatformTable<AutonumberReservation>(modelBuilder, "AutonumberReservations");

        MapPlatformTable<Setting>(modelBuilder, "Settings");
        MapPlatformTable<ReferenceDataItem>(modelBuilder, "ReferenceData");
        MapPlatformTable<Country>(modelBuilder, "Countries");
        MapPlatformTable<Currency>(modelBuilder, "Currencies");
        MapPlatformTable<CountryCurrency>(modelBuilder, "CountryCurrencies");

        MapPlatformTable<PartyEntity>(modelBuilder, "Parties");
        MapPlatformTable<PartyAddress>(modelBuilder, "PartyAddresses");
        MapPlatformTable<PartyContact>(modelBuilder, "PartyContacts");
        MapPlatformTable<PartyConsent>(modelBuilder, "PartyConsents");
        MapPlatformTable<PersonProfile>(modelBuilder, "PersonProfiles");
        MapPlatformTable<BusinessProfile>(modelBuilder, "BusinessProfiles");
        MapPlatformTable<ExternalAccount>(modelBuilder, "ExternalAccounts");
        MapPlatformTable<PartyRoleAssignment>(modelBuilder, "PartyRoleAssignments");
        MapPlatformTable<PartyRelationship>(modelBuilder, "PartyRelationships");

        MapPlatformTable<ContentBlock>(modelBuilder, "ContentBlocks");
        MapPlatformTable<ContentBlockMedia>(modelBuilder, "ContentBlockMedia");

        MapPlatformTable<ScreeningCheck>(modelBuilder, "ScreeningChecks");
        MapPlatformTable<ComplianceCase>(modelBuilder, "ComplianceCases");
        MapPlatformTable<AuditLog>(modelBuilder, "AuditLogs");
        MapPlatformTable<Document>(modelBuilder, "Documents");
        MapPlatformTable<DocumentFile>(modelBuilder, "DocumentFiles");
        MapPlatformTable<DocumentUsage>(modelBuilder, "DocumentUsages");
        MapPlatformTable<DocumentVerification>(modelBuilder, "DocumentVerifications");
        MapPlatformTable<DocumentVersion>(modelBuilder, "DocumentVersions");

        MapPlatformTable<TenantFeature>(modelBuilder, "TenantFeatures");
        MapPlatformTable<WorkItem>(modelBuilder, "WorkItems");
        MapPlatformTable<Job>(modelBuilder, "Jobs");
        MapPlatformTable<Notification>(modelBuilder, "Notifications");
        MapPlatformTable<NotificationTemplate>(modelBuilder, "NotificationTemplates");
        MapPlatformTable<NotificationTemplateBinding>(modelBuilder, "NotificationTemplateBindings");
        MapPlatformTable<WebhookSubscription>(modelBuilder, "WebhookSubscriptions");

        MapFinanceTable<LedgerEntity>(modelBuilder, "Ledgers");
        MapFinanceTable<LedgerAccount>(modelBuilder, "LedgerAccounts");
        MapFinanceTable<JournalEntry>(modelBuilder, "JournalEntries");
        MapFinanceTable<JournalEntryLine>(modelBuilder, "JournalEntryLines");
        MapFinanceTable<BalanceSnapshot>(modelBuilder, "BalanceSnapshots");

        MapFinanceTable<PaymentIntent>(modelBuilder, "PaymentIntents");
        MapFinanceTable<Payment>(modelBuilder, "Payments");
        MapFinanceTable<Payout>(modelBuilder, "Payouts");
        MapFinanceTable<Refund>(modelBuilder, "Refunds");
        MapFinanceTable<Chargeback>(modelBuilder, "Chargebacks");

        MapFinanceTable<Invoice>(modelBuilder, "Invoices");
        MapFinanceTable<InvoiceLine>(modelBuilder, "InvoiceLines");
        MapFinanceTable<InvoiceAllocation>(modelBuilder, "InvoiceAllocations");
        MapFinanceTable<CustomerAccount>(modelBuilder, "CustomerAccounts");
        MapFinanceTable<DunningPlan>(modelBuilder, "DunningPlans");

        MapFinanceTable<Order>(modelBuilder, "Orders");
        MapFinanceTable<OrderItem>(modelBuilder, "OrderItems");
        MapFinanceTable<OrderPartyRole>(modelBuilder, "OrderPartyRoles");
        MapFinanceTable<OrderFundingRef>(modelBuilder, "OrderFundingRefs");
        MapFinanceTable<OrderFulfilmentRef>(modelBuilder, "OrderFulfilmentRefs");
        MapFinanceTable<OrderHistoryEvent>(modelBuilder, "OrderHistoryEvents");
        MapFinanceTable<OrderNote>(modelBuilder, "OrderNotes");

        MapFinanceTable<FeePolicy>(modelBuilder, "FeePolicies");
        MapFinanceTable<FxQuote>(modelBuilder, "FxQuotes");
        MapFinanceTable<FxRateSource>(modelBuilder, "FxRateSources");
        MapFinanceTable<FxRefreshSchedule>(modelBuilder, "FxRefreshSchedules");
        MapFinanceTable<FxSpreadPolicy>(modelBuilder, "FxSpreadPolicies");
        MapFinanceTable<LimitsPolicy>(modelBuilder, "LimitsPolicies");
        MapFinanceTable<PricingQuote>(modelBuilder, "PricingQuotes");

        MapFinanceTable<Partner>(modelBuilder, "Partners");
        MapFinanceTable<PartnerBranch>(modelBuilder, "PartnerBranches");
        MapFinanceTable<PartnerFundingAccount>(modelBuilder, "PartnerFundingAccounts");
        MapFinanceTable<Connector>(modelBuilder, "Connectors");
        MapFinanceTable<RoutingRule>(modelBuilder, "RoutingRules");
        MapFinanceTable<PayoutSchema>(modelBuilder, "PayoutSchemas");
        MapFinanceTable<Transmission>(modelBuilder, "Transmissions");

        MapFinanceTable<CatalogBillerCategory>(modelBuilder, "CatalogBillerCategories");
        MapFinanceTable<CatalogBiller>(modelBuilder, "CatalogBillers");
        MapFinanceTable<CatalogBillerService>(modelBuilder, "CatalogBillerServices");

        MapFinanceTable<PersonalProfile>(modelBuilder, "PersonalProfiles");
        MapFinanceTable<Household>(modelBuilder, "Households");
        MapFinanceTable<HouseholdMember>(modelBuilder, "HouseholdMembers");
        MapFinanceTable<FinancialConnectionSession>(modelBuilder, "FinancialConnectionSessions");
        MapFinanceTable<FinancialConnection>(modelBuilder, "FinancialConnections");
        MapFinanceTable<FinancialLinkedAccount>(modelBuilder, "FinancialLinkedAccounts");
        MapFinanceTable<PersonalAccount>(modelBuilder, "PersonalAccounts");
        MapFinanceTable<PersonalTransaction>(modelBuilder, "PersonalTransactions");
        MapFinanceTable<CategorisationRule>(modelBuilder, "CategorisationRules");
        MapFinanceTable<BudgetLine>(modelBuilder, "BudgetLines");
        MapFinanceTable<Bill>(modelBuilder, "Bills");
        MapFinanceTable<Subscription>(modelBuilder, "Subscriptions");
        MapFinanceTable<Goal>(modelBuilder, "Goals");
        MapFinanceTable<Budget>(modelBuilder, "Budgets");
        MapFinanceTable<StatementImport>(modelBuilder, "StatementImports");
        MapFinanceTable<StatementImportRow>(modelBuilder, "StatementImportRows");

        MapAiTable<AiProvider>(modelBuilder, "AiProviders");
        MapAiTable<AiModel>(modelBuilder, "AiModels");
        MapAiTable<AiRoutePolicy>(modelBuilder, "AiRoutePolicies");
        MapAiTable<PromptSpec>(modelBuilder, "PromptSpecs");
        MapAiTable<ToolSpec>(modelBuilder, "ToolSpecs");
        MapAiTable<AiPolicy>(modelBuilder, "AiPolicies");
        MapAiTable<AiRun>(modelBuilder, "AiRuns");
        MapAiTable<AiTrace>(modelBuilder, "AiTraces");
        MapAiTable<AiFeedback>(modelBuilder, "AiFeedbacks");
        MapAiTable<EvalSuite>(modelBuilder, "EvalSuites");
        MapAiTable<EvalRun>(modelBuilder, "EvalRuns");
        MapAiTable<Insight>(modelBuilder, "Insights");
        MapAiTable<Signal>(modelBuilder, "Signals");

        MapAgentsTable<Agent>(modelBuilder, "Agents");
        MapAgentsTable<AgentRun>(modelBuilder, "AgentRuns");
        MapAgentsTable<OrchestratorPolicy>(modelBuilder, "OrchestratorPolicies");
        MapAgentsTable<Proposal>(modelBuilder, "Proposals");
    }

    private static void MapPlatformTable<TEntity>(ModelBuilder modelBuilder, string tableName)
        where TEntity : class
    {
        modelBuilder.Entity<TEntity>()
            .ToTable($"{ModuleTablePrefixes.Platform}{tableName}", SchemaNames.Default);
    }

    private static void MapFinanceTable<TEntity>(ModelBuilder modelBuilder, string tableName)
        where TEntity : class
    {
        modelBuilder.Entity<TEntity>()
            .ToTable($"{ModuleTablePrefixes.Finance}{tableName}", SchemaNames.Default);
    }

    private static void MapAiTable<TEntity>(ModelBuilder modelBuilder, string tableName)
        where TEntity : class
    {
        modelBuilder.Entity<TEntity>()
            .ToTable($"{ModuleTablePrefixes.Ai}{tableName}", SchemaNames.Default);
    }

    private static void MapAgentsTable<TEntity>(ModelBuilder modelBuilder, string tableName)
        where TEntity : class
    {
        modelBuilder.Entity<TEntity>()
            .ToTable($"{ModuleTablePrefixes.Agents}{tableName}", SchemaNames.Default);
    }
}
