using Aonik.Finance.Entities;
using Aonik.Finance.Entities.Billing;
using Aonik.Finance.Entities.Catalog;
using Aonik.Finance.Entities.Accounts;
using Aonik.Finance.Entities.Ledger;
using Aonik.Finance.Entities.Orders;
using Aonik.Finance.Entities.Partners;
using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Entities.Pricing;
using Aonik.Finance.Entities.ReferenceData;
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
    // ── Ledger ─────────────────────────────────────────────────────
    public DbSet<Ledger> Ledgers { get; set; } = null!;
    public DbSet<LedgerAccount> LedgerAccounts { get; set; } = null!;
    public DbSet<JournalEntry> JournalEntries { get; set; } = null!;
    public DbSet<JournalEntryLine> JournalEntryLines { get; set; } = null!;
    public DbSet<BalanceSnapshot> BalanceSnapshots { get; set; } = null!;

    // ── Payments ─────────────────────────────────────────────────────
    public DbSet<PaymentIntent> PaymentIntents { get; set; } = null!;
    public DbSet<Payment> Payments { get; set; } = null!;
    public DbSet<Payout> Payouts { get; set; } = null!;
    public DbSet<Refund> Refunds { get; set; } = null!;
    public DbSet<Chargeback> Chargebacks { get; set; } = null!;

    // ── Billing ─────────────────────────────────────────────────────
    public DbSet<Invoice> Invoices { get; set; } = null!;
    public DbSet<InvoiceLine> InvoiceLines { get; set; } = null!;
    public DbSet<InvoiceAllocation> InvoiceAllocations { get; set; } = null!;
    public DbSet<CustomerAccount> CustomerAccounts { get; set; } = null!;
    public DbSet<DunningPlan> DunningPlans { get; set; } = null!;

    // ── Orders ─────────────────────────────────────────────────────
    public DbSet<Order> Orders { get; set; } = null!;
    public DbSet<OrderItem> OrderItems { get; set; } = null!;
    public DbSet<OrderPartyRole> OrderPartyRoles { get; set; } = null!;
    public DbSet<OrderFundingRef> OrderFundingRefs { get; set; } = null!;
    public DbSet<OrderFulfilmentRef> OrderFulfilmentRefs { get; set; } = null!;
    public DbSet<OrderHistoryEvent> OrderHistoryEvents { get; set; } = null!;
    public DbSet<OrderNote> OrderNotes { get; set; } = null!;

    // ── Pricing ─────────────────────────────────────────────────────
    public DbSet<FeePolicy> FeePolicies { get; set; } = null!;
    public DbSet<FxQuote> FxQuotes { get; set; } = null!;
    public DbSet<FxRateSource> FxRateSources { get; set; } = null!;
    public DbSet<FxRefreshSchedule> FxRefreshSchedules { get; set; } = null!;
    public DbSet<FxSpreadPolicy> FxSpreadPolicies { get; set; } = null!;
    public DbSet<LimitsPolicy> LimitsPolicies { get; set; } = null!;
    public DbSet<PricingQuote> PricingQuotes { get; set; } = null!;

    // ── Partners ─────────────────────────────────────────────────────
    public DbSet<Partner> Partners { get; set; } = null!;
    public DbSet<PartnerBranch> PartnerBranches { get; set; } = null!;
    public DbSet<PartnerFundingAccount> PartnerFundingAccounts { get; set; } = null!;
    public DbSet<Connector> Connectors { get; set; } = null!;
    public DbSet<RoutingRule> RoutingRules { get; set; } = null!;
    public DbSet<PayoutSchema> PayoutSchemas { get; set; } = null!;
    public DbSet<Transmission> Transmissions { get; set; } = null!;

    // ── Temporary Cross-Module DbSets ──────────────────────────────
    // These entities belong to other modules but are queried by Finance
    // services during the migration period. They will be replaced by
    // inter-module service contracts in a future PR.

    /// <summary>Read-only projection of Party (authoritative entity in Platform module)</summary>
    public DbSet<PartyReadModel> Parties { get; set; } = null!;

    /// <summary>Read-only projection of PartyRelationship (authoritative entity in Platform module)</summary>
    public DbSet<PartyRelationshipReadModel> PartyRelationships { get; set; } = null!;

    /// <summary>Read-only projection of User (authoritative entity in Platform module)</summary>
    public DbSet<UserReadModel> Users { get; set; } = null!;

    // ── Catalog ─────────────────────────────────────────────────────
    public DbSet<CatalogBillerCategory> CatalogBillerCategories { get; set; } = null!;
    public DbSet<CatalogBiller> CatalogBillers { get; set; } = null!;
    public DbSet<CatalogBillerService> CatalogBillerServices { get; set; } = null!;

    // ── Reference Data Read Models ──────────────────────────────────
    // These are read-only projections of Platform reference data entities.
    // TEMPORARY: Will be replaced by inter-module service contracts.
    public DbSet<CountryReadModel> Countries { get; set; } = null!;
    public DbSet<CurrencyReadModel> Currencies { get; set; } = null!;
    public DbSet<CountryCurrencyReadModel> CountryCurrencies { get; set; } = null!;

    // ── Accounts (Tenant-Scoped Bank Linking) ──────────────
    public DbSet<AccountConnection> AccountConnections { get; set; } = null!;
    public DbSet<AccountConnectionSession> AccountConnectionSessions { get; set; } = null!;
    public DbSet<Account> Accounts { get; set; } = null!;
    public DbSet<AccountTransaction> AccountTransactions { get; set; } = null!;
    public DbSet<AccountTransactionAttachment> AccountTransactionAttachments { get; set; } = null!;

    // ── PersonalFinance ─────────────────────────────────────────────
    public DbSet<PersonalProfile> PersonalProfiles { get; set; } = null!;
    public DbSet<Household> Households { get; set; } = null!;
    public DbSet<HouseholdMember> HouseholdMembers { get; set; } = null!;
    public DbSet<FinancialConnectionSession> FinancialConnectionSessions { get; set; } = null!;
    public DbSet<FinancialConnection> FinancialConnections { get; set; } = null!;
    public DbSet<PersonalLinkedAccount> PersonalLinkedAccounts { get; set; } = null!;
    public DbSet<FinancialWebhookEvent> FinancialWebhookEvents { get; set; } = null!;
    public DbSet<PersonalAccount> PersonalAccounts { get; set; } = null!;
    public DbSet<PersonalTransaction> PersonalTransactions { get; set; } = null!;
    public DbSet<TransactionCategory> TransactionCategories { get; set; } = null!;
    public DbSet<CategorisationRule> CategorisationRules { get; set; } = null!;
    public DbSet<BudgetLine> BudgetLines { get; set; } = null!;
    public DbSet<Bill> Bills { get; set; } = null!;
    public DbSet<Subscription> Subscriptions { get; set; } = null!;
    public DbSet<Goal> Goals { get; set; } = null!;
    public DbSet<Budget> Budgets { get; set; } = null!;
    public DbSet<StatementImport> StatementImports { get; set; } = null!;
    public DbSet<StatementImportRow> StatementImportRows { get; set; } = null!;
    public DbSet<FinancialLifeGraphNode> FinancialLifeGraphNodes { get; set; } = null!;
    public DbSet<FinancialLifeGraphEdge> FinancialLifeGraphEdges { get; set; } = null!;
    public DbSet<TransactionAttachment> TransactionAttachments { get; set; } = null!;
    public DbSet<FinancialContext> FinancialContexts { get; set; } = null!;
    public DbSet<FinancialContextFundingSource> FinancialContextFundingSources { get; set; } = null!;

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

        modelBuilder.HasDefaultSchema(SchemaNames.Default);

        // Apply EF configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FinanceDbContext).Assembly);

        ApplyDboPrefixedTableNames(modelBuilder);

        // Configure RowVersion as optimistic concurrency token on all AuditableEntity types
        ConfigureRowVersions(modelBuilder);

        // Apply tenant query filters for all ITenantScoped entities in this context
        ApplyTenantQueryFilters(modelBuilder);
    }

    private static void ApplyDboPrefixedTableNames(ModelBuilder modelBuilder)
    {
        MapTable<Ledger>(modelBuilder, "Ledgers");
        MapTable<LedgerAccount>(modelBuilder, "LedgerAccounts");
        MapTable<JournalEntry>(modelBuilder, "JournalEntries");
        MapTable<JournalEntryLine>(modelBuilder, "JournalEntryLines");
        MapTable<BalanceSnapshot>(modelBuilder, "BalanceSnapshots");

        MapTable<PaymentIntent>(modelBuilder, "PaymentIntents");
        MapTable<Payment>(modelBuilder, "Payments");
        MapTable<Payout>(modelBuilder, "Payouts");
        MapTable<Refund>(modelBuilder, "Refunds");
        MapTable<Chargeback>(modelBuilder, "Chargebacks");

        MapTable<Invoice>(modelBuilder, "Invoices");
        MapTable<InvoiceLine>(modelBuilder, "InvoiceLines");
        MapTable<InvoiceAllocation>(modelBuilder, "InvoiceAllocations");
        MapTable<CustomerAccount>(modelBuilder, "CustomerAccounts");
        MapTable<DunningPlan>(modelBuilder, "DunningPlans");

        MapTable<Order>(modelBuilder, "Orders");
        MapTable<OrderItem>(modelBuilder, "OrderItems");
        MapTable<OrderPartyRole>(modelBuilder, "OrderPartyRoles");
        MapTable<OrderFundingRef>(modelBuilder, "OrderFundingRefs");
        MapTable<OrderFulfilmentRef>(modelBuilder, "OrderFulfilmentRefs");
        MapTable<OrderHistoryEvent>(modelBuilder, "OrderHistoryEvents");
        MapTable<OrderNote>(modelBuilder, "OrderNotes");

        MapTable<FeePolicy>(modelBuilder, "FeePolicies");
        MapTable<FxQuote>(modelBuilder, "FxQuotes");
        MapTable<FxRateSource>(modelBuilder, "FxRateSources");
        MapTable<FxRefreshSchedule>(modelBuilder, "FxRefreshSchedules");
        MapTable<FxSpreadPolicy>(modelBuilder, "FxSpreadPolicies");
        MapTable<LimitsPolicy>(modelBuilder, "LimitsPolicies");
        MapTable<PricingQuote>(modelBuilder, "PricingQuotes");

        MapTable<Partner>(modelBuilder, "Partners");
        MapTable<PartnerBranch>(modelBuilder, "PartnerBranches");
        MapTable<PartnerFundingAccount>(modelBuilder, "PartnerFundingAccounts");
        MapTable<Connector>(modelBuilder, "Connectors");
        MapTable<RoutingRule>(modelBuilder, "RoutingRules");
        MapTable<PayoutSchema>(modelBuilder, "PayoutSchemas");
        MapTable<Transmission>(modelBuilder, "Transmissions");

        MapTable<CatalogBillerCategory>(modelBuilder, "CatalogBillerCategories");
        MapTable<CatalogBiller>(modelBuilder, "CatalogBillers");
        MapTable<CatalogBillerService>(modelBuilder, "CatalogBillerServices");

        MapTable<PersonalProfile>(modelBuilder, "PersonalProfiles");
        MapTable<Household>(modelBuilder, "Households");
        MapTable<HouseholdMember>(modelBuilder, "HouseholdMembers");
        MapTable<FinancialConnectionSession>(modelBuilder, "FinancialConnectionSessions");
        MapTable<FinancialConnection>(modelBuilder, "FinancialConnections");
        MapTable<PersonalLinkedAccount>(modelBuilder, "PersonalLinkedAccounts");
        MapTable<FinancialWebhookEvent>(modelBuilder, "FinancialWebhookEvents");
        MapTable<PersonalAccount>(modelBuilder, "PersonalAccounts");
        MapTable<PersonalTransaction>(modelBuilder, "PersonalTransactions");
        MapTable<CategorisationRule>(modelBuilder, "CategorisationRules");
        MapTable<BudgetLine>(modelBuilder, "BudgetLines");
        MapTable<Bill>(modelBuilder, "Bills");
        MapTable<Subscription>(modelBuilder, "Subscriptions");
        MapTable<Goal>(modelBuilder, "Goals");
        MapTable<Budget>(modelBuilder, "Budgets");
        MapTable<StatementImport>(modelBuilder, "StatementImports");
        MapTable<StatementImportRow>(modelBuilder, "StatementImportRows");
        MapTable<FinancialLifeGraphNode>(modelBuilder, "FinancialLifeGraphNodes");
        MapTable<FinancialLifeGraphEdge>(modelBuilder, "FinancialLifeGraphEdges");
        MapTable<FinancialContext>(modelBuilder, "FinancialContexts");
        MapTable<FinancialContextFundingSource>(modelBuilder, "FinancialContextFundingSources");

        MapPlatformTable<PartyReadModel>(modelBuilder, "Parties");
        MapPlatformTable<PartyRelationshipReadModel>(modelBuilder, "PartyRelationships");
        MapPlatformTable<UserReadModel>(modelBuilder, "Users");
        MapPlatformTable<CountryReadModel>(modelBuilder, "Countries");
        MapPlatformTable<CurrencyReadModel>(modelBuilder, "Currencies");
        MapPlatformTable<CountryCurrencyReadModel>(modelBuilder, "CountryCurrencies");
    }

    private static void MapTable<TEntity>(ModelBuilder modelBuilder, string tableName)
        where TEntity : class
    {
        modelBuilder.Entity<TEntity>()
            .ToTable($"{ModuleTablePrefixes.Finance}{tableName}", SchemaNames.Default);
    }

    private static void MapPlatformTable<TEntity>(ModelBuilder modelBuilder, string tableName)
        where TEntity : class
    {
        modelBuilder.Entity<TEntity>()
            .ToTable($"{ModuleTablePrefixes.Platform}{tableName}", SchemaNames.Default);
    }

    /// <summary>
    /// Recognises Finance-domain entities that legitimately have TenantId == Guid.Empty
    /// and should NOT be stamped with the current tenant on write.
    /// Currently: CategorisationRule entities with Scope == "System".
    /// </summary>
    protected override bool IsGlobalEntity(object entity)
    {
        if (base.IsGlobalEntity(entity))
            return true;

        return entity is CategorisationRule rule
            && string.Equals(rule.Scope, "System", StringComparison.OrdinalIgnoreCase);
    }

    protected override void OnBeforeSave()
    {
        PopulateOrderCompatibilityColumns();
    }

    /// <summary>
    /// Populates shadow / compatibility columns on new Order entities.
    /// These columns (OrderNumber, ServiceCode, MetadataJson, OrderDetailsJson) are
    /// defined in migration-managed configurations and may not map to CLR properties.
    /// </summary>
    private void PopulateOrderCompatibilityColumns()
    {
        var orderEntries = ChangeTracker.Entries<Order>()
            .Where(entry => entry.State == EntityState.Added)
            .ToList();

        if (orderEntries.Count == 0)
            return;

        foreach (var entry in orderEntries)
        {
            if (entry.Metadata.FindProperty("OrderNumber") != null)
            {
                var orderNumber = entry.Property("OrderNumber").CurrentValue as string;
                if (string.IsNullOrWhiteSpace(orderNumber))
                {
                    entry.Property("OrderNumber").CurrentValue = GenerateOrderNumber();
                }
            }

            if (entry.Metadata.FindProperty("ServiceCode") != null)
            {
                var serviceCode = entry.Property("ServiceCode").CurrentValue as string;
                if (string.IsNullOrWhiteSpace(serviceCode))
                {
                    entry.Property("ServiceCode").CurrentValue = string.IsNullOrWhiteSpace(entry.Entity.OrderType)
                        ? "UNKNOWN"
                        : entry.Entity.OrderType.Trim().ToUpperInvariant();
                }
            }

            if (entry.Metadata.FindProperty("MetadataJson") != null)
            {
                var metadataJson = entry.Property("MetadataJson").CurrentValue as string;
                if (string.IsNullOrWhiteSpace(metadataJson))
                {
                    entry.Property("MetadataJson").CurrentValue = "{}";
                }
            }

            if (entry.Metadata.FindProperty("OrderDetailsJson") != null)
            {
                var detailsJson = entry.Property("OrderDetailsJson").CurrentValue as string;
                if (string.IsNullOrWhiteSpace(detailsJson))
                {
                    entry.Property("OrderDetailsJson").CurrentValue = "{}";
                }
            }
        }
    }

    private static string GenerateOrderNumber()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        var token = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        return $"ORD-{timestamp}-{token}";
    }
}
