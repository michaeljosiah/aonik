using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Platform.Entities.Autonumbering;
using Aonik.Finance.Entities.Catalog;
using Aonik.Platform.Entities.Cms;
using Aonik.Platform.Entities.Compliance;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Entities.Inventory;
using Aonik.Commerce.Entities.Promotions;
using Aonik.Documents.Entities;
using Aonik.Platform.Entities.Features;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Entities.Notifications;
using Aonik.Platform.Entities.Operations;
using Aonik.Platform.Entities.Party;
using Aonik.Platform.Entities.Tasks;
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
using Aonik.Agents.Entities.Workflows;
using Aonik.Voice.Entities;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Events.Outbox;
using Aonik.SharedKernel.Persistence;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using LedgerEntity = Aonik.Finance.Entities.Ledger.Ledger;
using PartyEntity = Aonik.Platform.Entities.Party.Party;

namespace Aonik.Infrastructure.Persistence;

public class AonikDbContext : AonikDbContextBase, IAonikDbContext, IDataProtectionKeyContext
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
    public virtual DbSet<PreRegistrationChallenge> PreRegistrationChallenges { get; set; } = null!;
    public virtual DbSet<UserInviteLog> UserInviteLogs { get; set; } = null!;
    public virtual DbSet<UserSessionBlocklistEntry> UserSessionBlocklist { get; set; } = null!;
    public virtual DbSet<UserTombstone> UserTombstones { get; set; } = null!;

    // Autonumbering
    public virtual DbSet<AutonumberProfile> AutonumberProfiles { get; set; } = null!;
    public virtual DbSet<AutonumberReservation> AutonumberReservations { get; set; } = null!;

    public virtual DbSet<Setting> Settings { get; set; } = null!;
    public virtual DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;
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
    public virtual DbSet<PartyAccount> PartyAccounts { get; set; } = null!;
    public virtual DbSet<PartyRoleAssignment> PartyRoleAssignments { get; set; } = null!;
    public virtual DbSet<PartyRelationship> PartyRelationships { get; set; } = null!;
    public virtual DbSet<NotificationPreference> NotificationPreferences { get; set; } = null!;
    public virtual DbSet<MarketingPreference> MarketingPreferences { get; set; } = null!;

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

    // Documents (Spec 035) — new module entities; canonical migration stream stays here.
    public virtual DbSet<DocumentIngestion> DocumentIngestions { get; set; } = null!;
    public virtual DbSet<DocumentExtraction> DocumentExtractions { get; set; } = null!;
    public virtual DbSet<DocumentLink> DocumentLinks { get; set; } = null!;

    // Commerce (Spec 042) — catalog + bundle entities; canonical migration stream stays here.
    public virtual DbSet<Product> Products { get; set; } = null!;
    public virtual DbSet<ProductVariant> ProductVariants { get; set; } = null!;
    public virtual DbSet<ProductCategory> ProductCategories { get; set; } = null!;
    public virtual DbSet<ProductMedia> ProductMedia { get; set; } = null!;
    public virtual DbSet<ProductPrice> ProductPrices { get; set; } = null!;
    public virtual DbSet<BundleSlot> BundleSlots { get; set; } = null!;
    public virtual DbSet<BundleSlotOption> BundleSlotOptions { get; set; } = null!;
    public virtual DbSet<InventoryLevel> InventoryLevels { get; set; } = null!;
    public virtual DbSet<InventoryReservation> InventoryReservations { get; set; } = null!;
    public virtual DbSet<Aonik.Commerce.Entities.Cart.Cart> Carts { get; set; } = null!;
    public virtual DbSet<CartItem> CartItems { get; set; } = null!;
    public virtual DbSet<CartItemSelection> CartItemSelections { get; set; } = null!;
    public virtual DbSet<OrderBundleSelection> OrderBundleSelections { get; set; } = null!;
    public virtual DbSet<Discount> Discounts { get; set; } = null!;
    public virtual DbSet<OrderChargeSummary> OrderChargeSummaries { get; set; } = null!;

    // Features
    public virtual DbSet<TenantFeature> TenantFeatures { get; set; } = null!;

    // Tasks (Spec 034 — Task/WorkItem scheduling)
    public virtual DbSet<WorkItem> WorkItems { get; set; } = null!;
    public virtual DbSet<WorkItemRun> WorkItemRuns { get; set; } = null!;

    // Operations
    public virtual DbSet<Job> Jobs { get; set; } = null!;
    public virtual DbSet<ScheduledJobProjection> ScheduledJobProjections { get; set; } = null!;
    public virtual DbSet<ScheduledJobAdminCommand> ScheduledJobAdminCommands { get; set; } = null!;
    public virtual DbSet<ScheduledJobRun> ScheduledJobRuns { get; set; } = null!;
    public virtual DbSet<SchedulerHealthSnapshot> SchedulerHealthSnapshots { get; set; } = null!;
    public virtual DbSet<AzureMonitorAlertEvent> AzureMonitorAlertEvents { get; set; } = null!;

    // Notifications
    public virtual DbSet<Notification> Notifications { get; set; } = null!;
    public virtual DbSet<NotificationDevice> NotificationDevices { get; set; } = null!;
    public virtual DbSet<NotificationTemplate> NotificationTemplates { get; set; } = null!;
    public virtual DbSet<NotificationTemplateBinding> NotificationTemplateBindings { get; set; } = null!;
    public virtual DbSet<WebhookSubscription> WebhookSubscriptions { get; set; } = null!;

    // Personal Finance
    public virtual DbSet<PersonalProfile> PersonalProfiles { get; set; } = null!;
    public virtual DbSet<Household> Households { get; set; } = null!;
    public virtual DbSet<HouseholdMember> HouseholdMembers { get; set; } = null!;
    public virtual DbSet<FinancialConnectionSession> FinancialConnectionSessions { get; set; } = null!;
    public virtual DbSet<FinancialConnection> FinancialConnections { get; set; } = null!;
    public virtual DbSet<PersonalLinkedAccount> PersonalLinkedAccounts { get; set; } = null!;
    public virtual DbSet<FinancialWebhookEvent> FinancialWebhookEvents { get; set; } = null!;
    public virtual DbSet<PersonalAccount> PersonalAccounts { get; set; } = null!;
    public virtual DbSet<PersonalTransaction> PersonalTransactions { get; set; } = null!;
    public virtual DbSet<TransactionCategory> TransactionCategories { get; set; } = null!;
    public virtual DbSet<CategorisationRule> CategorisationRules { get; set; } = null!;
    public virtual DbSet<BudgetLine> BudgetLines { get; set; } = null!;
    public virtual DbSet<Bill> Bills { get; set; } = null!;
    public virtual DbSet<Subscription> Subscriptions { get; set; } = null!;
    public virtual DbSet<PersonalRecurringBill> PersonalRecurringBills { get; set; } = null!;
    public virtual DbSet<DebtRepayment> DebtRepayments { get; set; } = null!;
    public virtual DbSet<Goal> Goals { get; set; } = null!;
    public virtual DbSet<Budget> Budgets { get; set; } = null!;
    public virtual DbSet<CareEntity> CareEntities { get; set; } = null!;
    public virtual DbSet<PaymentLog> PaymentLogs { get; set; } = null!;
    public virtual DbSet<CommitmentCycle> CommitmentCycles { get; set; } = null!;
    public virtual DbSet<CircleGrant> CircleGrants { get; set; } = null!;
    public virtual DbSet<CircleInvite> CircleInvites { get; set; } = null!;
    public virtual DbSet<CustomerInsightSnapshot> CustomerInsightSnapshots { get; set; } = null!;
    public virtual DbSet<StatementImport> StatementImports { get; set; } = null!;
    public virtual DbSet<StatementImportRow> StatementImportRows { get; set; } = null!;
    public virtual DbSet<FinancialLifeGraphNode> FinancialLifeGraphNodes { get; set; } = null!;
    public virtual DbSet<FinancialLifeGraphEdge> FinancialLifeGraphEdges { get; set; } = null!;
    public virtual DbSet<TransactionAttachment> TransactionAttachments { get; set; } = null!;

    // Chat Threads (Agents module)
    public virtual DbSet<ChatThread> ChatThreads { get; set; } = null!;
    public virtual DbSet<ChatThreadMessage> ChatThreadMessages { get; set; } = null!;

    // Tool approval requests (Agents module — Spec 032 §7.5 durable audit/correlation row)
    public virtual DbSet<ToolApprovalRequest> ToolApprovalRequests { get; set; } = null!;

    // Tenant-managed agent extensibility (Agents module — Spec 033)
    public virtual DbSet<TenantSkill> TenantSkills { get; set; } = null!;
    public virtual DbSet<TenantMcpServer> TenantMcpServers { get; set; } = null!;
    public virtual DbSet<TenantHttpTool> TenantHttpTools { get; set; } = null!;

    // Playground Scenarios (Agents module)
    public virtual DbSet<PlaygroundScenario> PlaygroundScenarios { get; set; } = null!;
    public virtual DbSet<PlaygroundScenarioTurn> PlaygroundScenarioTurns { get; set; } = null!;

    // Workflows (Agents module)
    public virtual DbSet<Workflow> Workflows { get; set; } = null!;
    public virtual DbSet<WorkflowNode> WorkflowNodes { get; set; } = null!;
    public virtual DbSet<WorkflowEdge> WorkflowEdges { get; set; } = null!;
    public virtual DbSet<WorkflowVersion> WorkflowVersions { get; set; } = null!;
    public virtual DbSet<WorkflowComment> WorkflowComments { get; set; } = null!;
    public virtual DbSet<WorkflowRun> WorkflowRuns { get; set; } = null!;

    // Voice — speech provider library + recipe library + active settings (spec 024)
    public virtual DbSet<SpeechProviderEntity> SpeechProviders { get; set; } = null!;
    public virtual DbSet<VoiceRecipeEntity> VoiceRecipes { get; set; } = null!;
    public virtual DbSet<VoiceModeSettingsEntity> VoiceModeSettings { get; set; } = null!;
    public virtual DbSet<ChatSpeechSettingsEntity> ChatSpeechSettings { get; set; } = null!;

    // Transactional outbox / inbox (configured on AonikDbContextBase; this is the
    // canonical migration stream that owns the AnkOutboxMessages/AnkInboxMessages tables)
    public virtual DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;
    public virtual DbSet<InboxMessage> InboxMessages { get; set; } = null!;

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

        // Spec 041 / ADR-011: the Order EF configurations relocated to Aonik.Ordering (namespace
        // preserved). Apply them here so AonikDbContext — the canonical migration stream — keeps
        // the identical Order model (table names come from the MapTable calls below).
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Order).Assembly);

        // Apply PersonalFinance configurations from Aonik.PersonalFinance assembly.
        // Spec 027 Phase 2: PF entity types + EF configs now live in their own
        // assembly. The canonical migration stream stays in AonikDbContext, so
        // this scan keeps the model in sync with the configs.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Aonik.PersonalFinance.PersonalFinanceModule).Assembly);

        // Apply AI configurations from Ai assembly (required for EF migrations)
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Aonik.Ai.Entities.AiProvider).Assembly);

        // Apply Agents configurations from Agents assembly (required for EF migrations)
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Aonik.Agents.Entities.Agent).Assembly);

        // Apply Voice configurations (spec 024 — speech provider library)
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SpeechProviderEntity).Assembly);

        // Apply Documents configurations from Aonik.Documents assembly (Spec 035).
        // The canonical migration stream stays in AonikDbContext; this scan keeps the
        // model in sync with the module's EF configs.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Aonik.Documents.DocumentsModule).Assembly);

        // Apply Commerce configurations from Aonik.Commerce assembly (Spec 042).
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Aonik.Commerce.CommerceModule).Assembly);

        // Configure RowVersion as optimistic concurrency token on all AuditableEntity types
        ConfigureRowVersions(modelBuilder);

        // Apply tenant query filters
        ApplyTenantQueryFilters(modelBuilder);

        // Apply nullable tenant filters for entities with optional TenantId
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(Aonik.Agents.Entities.Agent));
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(Aonik.Agents.Entities.OrchestratorPolicy));
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(ReferenceDataItem));
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(Country));
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(Currency));
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(NotificationTemplate));
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(AiRoutePolicy));
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(PromptSpec));
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(AiTask));
        // FinancialInstitution is a global directory (nullable TenantId): tenants share the base bank
        // list and may add their own rows. Mirrors ReferenceDataItem.
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(FinancialInstitution));

        // Apply soft-delete filters for AuditableEntity types that are not tenant-scoped
        // and not already covered by nullable tenant filters above
        ApplySoftDeleteQueryFilters(modelBuilder,
            typeof(Aonik.Agents.Entities.Agent),
            typeof(Aonik.Agents.Entities.OrchestratorPolicy),
            typeof(ReferenceDataItem),
            typeof(Country),
            typeof(Currency),
            typeof(NotificationTemplate),
            typeof(AiRoutePolicy),
            typeof(PromptSpec),
            typeof(AiTask),
            typeof(FinancialInstitution));

        ApplyDboPrefixedTableNames(modelBuilder);
        ConfigureScheduledJobProjection(modelBuilder);
        ConfigureScheduledJobAdminCommand(modelBuilder);
        ConfigureScheduledJobRun(modelBuilder);
        ConfigureSchedulerHealthSnapshot(modelBuilder);
    }

    protected override bool IsGlobalEntity(object entity)
    {
        return entity is Role
            or Job
            or ScheduledJobProjection
            or ScheduledJobAdminCommand
            or ScheduledJobRun
            or SchedulerHealthSnapshot
            or AzureMonitorAlertEvent;
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
        MapPlatformTable<PreRegistrationChallenge>(modelBuilder, "PreRegistrationChallenges");
        MapPlatformTable<UserInviteLog>(modelBuilder, "UserInviteLogs");
        MapPlatformTable<UserSessionBlocklistEntry>(modelBuilder, "UserSessionBlocklist");
        MapPlatformTable<UserTombstone>(modelBuilder, "UserTombstones");

        MapPlatformTable<AutonumberProfile>(modelBuilder, "AutonumberProfiles");
        MapPlatformTable<AutonumberReservation>(modelBuilder, "AutonumberReservations");

        MapPlatformTable<Setting>(modelBuilder, "Settings");
        modelBuilder.Entity<DataProtectionKey>().ToTable("AnkDataProtectionKeys", SchemaNames.Default);
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
        MapPlatformTable<PartyAccount>(modelBuilder, "PartyAccounts");
        MapPlatformTable<PartyRoleAssignment>(modelBuilder, "PartyRoleAssignments");
        MapPlatformTable<PartyRelationship>(modelBuilder, "PartyRelationships");
        MapPlatformTable<NotificationPreference>(modelBuilder, "NotificationPreferences");
        MapPlatformTable<MarketingPreference>(modelBuilder, "MarketingPreferences");

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
        MapPlatformTable<DocumentIngestion>(modelBuilder, "DocumentIngestions");
        MapPlatformTable<DocumentExtraction>(modelBuilder, "DocumentExtractions");
        MapPlatformTable<DocumentLink>(modelBuilder, "DocumentLinks");

        // Commerce (Spec 042)
        MapCommerceTable<Product>(modelBuilder, "Products");
        MapCommerceTable<ProductVariant>(modelBuilder, "ProductVariants");
        MapCommerceTable<ProductCategory>(modelBuilder, "ProductCategories");
        MapCommerceTable<ProductMedia>(modelBuilder, "ProductMedia");
        MapCommerceTable<ProductPrice>(modelBuilder, "ProductPrices");
        MapCommerceTable<BundleSlot>(modelBuilder, "BundleSlots");
        MapCommerceTable<BundleSlotOption>(modelBuilder, "BundleSlotOptions");
        MapCommerceTable<InventoryLevel>(modelBuilder, "InventoryLevels");
        MapCommerceTable<InventoryReservation>(modelBuilder, "InventoryReservations");
        MapCommerceTable<Aonik.Commerce.Entities.Cart.Cart>(modelBuilder, "Carts");
        MapCommerceTable<CartItem>(modelBuilder, "CartItems");
        MapCommerceTable<CartItemSelection>(modelBuilder, "CartItemSelections");
        MapCommerceTable<OrderBundleSelection>(modelBuilder, "OrderBundleSelections");
        MapCommerceTable<Discount>(modelBuilder, "Discounts");
        MapCommerceTable<OrderChargeSummary>(modelBuilder, "OrderChargeSummaries");

        MapPlatformTable<TenantFeature>(modelBuilder, "TenantFeatures");
        MapPlatformTable<WorkItem>(modelBuilder, "WorkItems");
        MapPlatformTable<WorkItemRun>(modelBuilder, "WorkItemRuns");
        MapPlatformTable<Job>(modelBuilder, "Jobs");
        MapPlatformTable<ScheduledJobProjection>(modelBuilder, "ScheduledJobProjections");
        MapPlatformTable<ScheduledJobAdminCommand>(modelBuilder, "ScheduledJobAdminCommands");
        MapPlatformTable<ScheduledJobRun>(modelBuilder, "ScheduledJobRuns");
        MapPlatformTable<SchedulerHealthSnapshot>(modelBuilder, "SchedulerHealthSnapshots");
        MapPlatformTable<AzureMonitorAlertEvent>(modelBuilder, "AzureMonitorAlertEvents");
        MapPlatformTable<Notification>(modelBuilder, "Notifications");
        MapPlatformTable<NotificationDevice>(modelBuilder, "NotificationDevices");
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
        MapFinanceTable<CredentialBundle>(modelBuilder, "CredentialBundles");
        MapFinanceTable<RoutingRule>(modelBuilder, "RoutingRules");
        MapFinanceTable<PayoutSchema>(modelBuilder, "PayoutSchemas");
        MapFinanceTable<Transmission>(modelBuilder, "Transmissions");

        MapFinanceTable<CatalogBillerCategory>(modelBuilder, "CatalogBillerCategories");
        MapFinanceTable<CatalogBiller>(modelBuilder, "CatalogBillers");
        MapFinanceTable<CatalogBillerService>(modelBuilder, "CatalogBillerServices");

        // Partner integration abstraction (spec 031): payout / collection / bill-payment plumbing.
        MapFinanceTable<ExternalPayoutAccount>(modelBuilder, "ExternalPayoutAccounts");
        MapFinanceTable<PayoutReversal>(modelBuilder, "PayoutReversals");
        MapFinanceTable<BillValidation>(modelBuilder, "BillValidations");
        MapFinanceTable<PartnerBillPayment>(modelBuilder, "PartnerBillPayments");
        MapFinanceTable<FinancialInstitution>(modelBuilder, "FinancialInstitutions");
        MapFinanceTable<ConnectorInstitutionCode>(modelBuilder, "ConnectorInstitutionCodes");
        MapFinanceTable<ConnectorCapability>(modelBuilder, "ConnectorCapabilities");
        MapFinanceTable<ConnectorBillerMapping>(modelBuilder, "ConnectorBillerMappings");
        MapFinanceTable<PartnerWebhookEvent>(modelBuilder, "PartnerWebhookEvents");

        MapFinanceTable<PersonalProfile>(modelBuilder, "PersonalProfiles");
        MapFinanceTable<Household>(modelBuilder, "Households");
        MapFinanceTable<HouseholdMember>(modelBuilder, "HouseholdMembers");
        MapFinanceTable<FinancialConnectionSession>(modelBuilder, "FinancialConnectionSessions");
        MapFinanceTable<FinancialConnection>(modelBuilder, "FinancialConnections");
        MapFinanceTable<PersonalLinkedAccount>(modelBuilder, "PersonalLinkedAccounts");
        MapFinanceTable<FinancialWebhookEvent>(modelBuilder, "FinancialWebhookEvents");
        MapFinanceTable<PersonalAccount>(modelBuilder, "PersonalAccounts");
        MapFinanceTable<PersonalTransaction>(modelBuilder, "PersonalTransactions");
        MapFinanceTable<CategorisationRule>(modelBuilder, "CategorisationRules");
        MapFinanceTable<BudgetLine>(modelBuilder, "BudgetLines");
        MapFinanceTable<Bill>(modelBuilder, "Bills");
        MapFinanceTable<Subscription>(modelBuilder, "Subscriptions");
        MapFinanceTable<PersonalRecurringBill>(modelBuilder, "PersonalRecurringBills");
        MapFinanceTable<DebtRepayment>(modelBuilder, "DebtRepayments");
        MapFinanceTable<Goal>(modelBuilder, "Goals");
        MapFinanceTable<Budget>(modelBuilder, "Budgets");
        MapFinanceTable<CareEntity>(modelBuilder, "CareEntities");
        MapFinanceTable<PaymentLog>(modelBuilder, "PaymentLogs");
        MapFinanceTable<CommitmentCycle>(modelBuilder, "CommitmentCycles");
        MapFinanceTable<CircleGrant>(modelBuilder, "CircleGrants");
        MapFinanceTable<CircleInvite>(modelBuilder, "CircleInvites");
        MapFinanceTable<CustomerInsightSnapshot>(modelBuilder, "CustomerInsightSnapshots");
        MapFinanceTable<StatementImport>(modelBuilder, "StatementImports");
        MapFinanceTable<StatementImportRow>(modelBuilder, "StatementImportRows");
        MapFinanceTable<FinancialLifeGraphNode>(modelBuilder, "FinancialLifeGraphNodes");
        MapFinanceTable<FinancialLifeGraphEdge>(modelBuilder, "FinancialLifeGraphEdges");

        MapAiTable<AiProvider>(modelBuilder, "AiProviders");
        MapAiTable<AiModel>(modelBuilder, "AiModels");
        MapAiTable<AiRoutePolicy>(modelBuilder, "AiRoutePolicies");
        MapAiTable<PromptSpec>(modelBuilder, "PromptSpecs");
        MapAiTable<AiTask>(modelBuilder, "AiTasks");
        MapAiTable<ToolSpec>(modelBuilder, "ToolSpecs");
        MapAiTable<AiPolicy>(modelBuilder, "AiPolicies");
        MapAiTable<AiRun>(modelBuilder, "AiRuns");
        MapAiTable<TenantAgentSettings>(modelBuilder, "TenantAgentSettings");
        MapAiTable<AiTrace>(modelBuilder, "AiTraces");
        MapAiTable<AiFeedback>(modelBuilder, "AiFeedbacks");
        MapAiTable<EvalSuite>(modelBuilder, "EvalSuites");
        MapAiTable<EvalRun>(modelBuilder, "EvalRuns");
        MapAiTable<CustomerInsightAiSummary>(modelBuilder, "CustomerInsightAiSummaries");
        MapAiTable<Insight>(modelBuilder, "Insights");
        MapAiTable<Signal>(modelBuilder, "Signals");
        // Spec 041 follow-up — reconcile the canonical snapshot to the live table name. The runtime
        // AiDbContext already maps this to AnkUserMemoryEntries (and a raw-SQL sp_rename migration
        // renamed the physical table), but AonikDbContext never mapped it, so its model snapshot had
        // drifted to "UserMemoryEntry". Mapping it here corrects the snapshot; the paired reconcile
        // migration's Up() is a deliberate no-op because the rename already happened out-of-band.
        MapAiTable<UserMemoryEntry>(modelBuilder, "UserMemoryEntries");
        MapAiTable<DecisionPattern>(modelBuilder, "DecisionPatterns");

        MapAgentsTable<Agent>(modelBuilder, "Agents");
        MapAgentsTable<AgentRun>(modelBuilder, "AgentRuns");
        MapAgentsTable<OrchestratorPolicy>(modelBuilder, "OrchestratorPolicies");
        MapAgentsTable<Proposal>(modelBuilder, "Proposals");
        MapAgentsTable<ToolApprovalRequest>(modelBuilder, "ToolApprovalRequests");
        MapAgentsTable<TenantSkill>(modelBuilder, "TenantSkills");
        MapAgentsTable<TenantMcpServer>(modelBuilder, "TenantMcpServers");
        MapAgentsTable<TenantHttpTool>(modelBuilder, "TenantHttpTools");
        MapAgentsTable<ChatThread>(modelBuilder, "ChatThreads");
        MapAgentsTable<ChatThreadMessage>(modelBuilder, "ChatThreadMessages");
        MapAgentsTable<PlaygroundScenario>(modelBuilder, "PlaygroundScenarios");
        MapAgentsTable<PlaygroundScenarioTurn>(modelBuilder, "PlaygroundScenarioTurns");

        MapAgentsTable<Workflow>(modelBuilder, "Workflows");
        MapAgentsTable<WorkflowNode>(modelBuilder, "WorkflowNodes");
        MapAgentsTable<WorkflowEdge>(modelBuilder, "WorkflowEdges");
        MapAgentsTable<WorkflowVersion>(modelBuilder, "WorkflowVersions");
        MapAgentsTable<WorkflowComment>(modelBuilder, "WorkflowComments");
        MapAgentsTable<WorkflowRun>(modelBuilder, "WorkflowRuns");
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

    private static void MapCommerceTable<TEntity>(ModelBuilder modelBuilder, string tableName)
        where TEntity : class
    {
        modelBuilder.Entity<TEntity>()
            .ToTable($"{ModuleTablePrefixes.Commerce}{tableName}", SchemaNames.Default);
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

    private static void ConfigureScheduledJobProjection(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<ScheduledJobProjection>();

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.JobName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.GroupName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(x => x.CronExpression)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(x => x.TimeZoneId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.State)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.LastOutcome)
            .HasMaxLength(50);

        builder.Property(x => x.LastOutcomeSummary)
            .HasMaxLength(1000);

        builder.HasIndex(x => new { x.GroupName, x.JobName })
            .IsUnique()
            .HasDatabaseName("IX_ScheduledJobProjection_GroupName_JobName");
    }

    private static void ConfigureScheduledJobAdminCommand(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<ScheduledJobAdminCommand>();

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.JobName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.GroupName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.CommandType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.PayloadJson)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.ResultMessage)
            .HasMaxLength(1000);

        builder.HasIndex(x => new { x.Status, x.CreatedAt })
            .HasDatabaseName("IX_ScheduledJobAdminCommand_Status_CreatedAt");

        builder.HasIndex(x => new { x.GroupName, x.JobName, x.Status })
            .HasDatabaseName("IX_ScheduledJobAdminCommand_GroupName_JobName_Status");
    }

    private static void ConfigureScheduledJobRun(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<ScheduledJobRun>();

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.JobName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.GroupName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Outcome)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(x => x.TriggeredBy)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.FireInstanceId)
            .HasMaxLength(200);

        builder.HasIndex(x => new { x.GroupName, x.JobName, x.FiredAtUtc })
            .HasDatabaseName("IX_ScheduledJobRun_GroupName_JobName_FiredAtUtc");

        builder.HasIndex(x => x.FiredAtUtc)
            .HasDatabaseName("IX_ScheduledJobRun_FiredAtUtc");
    }

    private static void ConfigureSchedulerHealthSnapshot(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<SchedulerHealthSnapshot>();

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.SchedulerName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.SchedulerInstanceId)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(x => new { x.SchedulerName, x.SchedulerInstanceId })
            .IsUnique()
            .HasDatabaseName("IX_SchedulerHealthSnapshot_Name_InstanceId");
    }
}
