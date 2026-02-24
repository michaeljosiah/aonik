using Aonik.Finance.Entities;
using Aonik.Finance.Entities.Billing;
using Aonik.Finance.Entities.Catalog;
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

    // ── PersonalFinance ─────────────────────────────────────────────
    public DbSet<PersonalProfile> PersonalProfiles { get; set; } = null!;
    public DbSet<Household> Households { get; set; } = null!;
    public DbSet<HouseholdMember> HouseholdMembers { get; set; } = null!;
    public DbSet<PersonalAccount> PersonalAccounts { get; set; } = null!;
    public DbSet<PersonalTransaction> PersonalTransactions { get; set; } = null!;
    public DbSet<CategorisationRule> CategorisationRules { get; set; } = null!;
    public DbSet<BudgetLine> BudgetLines { get; set; } = null!;
    public DbSet<Bill> Bills { get; set; } = null!;
    public DbSet<Subscription> Subscriptions { get; set; } = null!;
    public DbSet<Goal> Goals { get; set; } = null!;
    public DbSet<Budget> Budgets { get; set; } = null!;

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

        // All Finance entities use the 'finance' schema by default
        modelBuilder.HasDefaultSchema(SchemaNames.Finance);

        // ── Schema overrides for entities created in dbo by existing migrations ──
        // All these entities were created in dbo schema before the Finance module existed.
        // They must continue to use dbo to match the existing database.

        // Ledger
        modelBuilder.Entity<Ledger>().ToTable("Ledgers", SchemaNames.Default);
        modelBuilder.Entity<LedgerAccount>().ToTable("LedgerAccounts", SchemaNames.Default);
        modelBuilder.Entity<JournalEntry>().ToTable("JournalEntries", SchemaNames.Default);
        modelBuilder.Entity<JournalEntryLine>().ToTable("JournalEntryLines", SchemaNames.Default);
        modelBuilder.Entity<BalanceSnapshot>().ToTable("BalanceSnapshots", SchemaNames.Default);

        // Payments
        modelBuilder.Entity<PaymentIntent>().ToTable("PaymentIntents", SchemaNames.Default);
        modelBuilder.Entity<Payment>().ToTable("Payments", SchemaNames.Default);
        modelBuilder.Entity<Payout>().ToTable("Payouts", SchemaNames.Default);
        modelBuilder.Entity<Refund>().ToTable("Refunds", SchemaNames.Default);
        modelBuilder.Entity<Chargeback>().ToTable("Chargebacks", SchemaNames.Default);

        // Billing
        modelBuilder.Entity<Invoice>().ToTable("Invoices", SchemaNames.Default);
        modelBuilder.Entity<InvoiceLine>().ToTable("InvoiceLines", SchemaNames.Default);
        modelBuilder.Entity<InvoiceAllocation>().ToTable("InvoiceAllocations", SchemaNames.Default);
        modelBuilder.Entity<CustomerAccount>().ToTable("CustomerAccounts", SchemaNames.Default);
        modelBuilder.Entity<DunningPlan>().ToTable("DunningPlans", SchemaNames.Default);

        // Orders
        modelBuilder.Entity<Order>().ToTable("Orders", SchemaNames.Default);
        modelBuilder.Entity<OrderItem>().ToTable("OrderItems", SchemaNames.Default);
        modelBuilder.Entity<OrderPartyRole>().ToTable("OrderPartyRoles", SchemaNames.Default);
        modelBuilder.Entity<OrderFundingRef>().ToTable("OrderFundingRefs", SchemaNames.Default);
        modelBuilder.Entity<OrderFulfilmentRef>().ToTable("OrderFulfilmentRefs", SchemaNames.Default);
        modelBuilder.Entity<OrderHistoryEvent>().ToTable("OrderHistoryEvents", SchemaNames.Default);
        modelBuilder.Entity<OrderNote>().ToTable("OrderNotes", SchemaNames.Default);

        // Pricing
        modelBuilder.Entity<FeePolicy>().ToTable("FeePolicies", SchemaNames.Default);
        modelBuilder.Entity<FxQuote>().ToTable("FxQuotes", SchemaNames.Default);
        modelBuilder.Entity<FxRateSource>().ToTable("FxRateSources", SchemaNames.Default);
        modelBuilder.Entity<FxRefreshSchedule>().ToTable("FxRefreshSchedules", SchemaNames.Default);
        modelBuilder.Entity<FxSpreadPolicy>().ToTable("FxSpreadPolicies", SchemaNames.Default);
        modelBuilder.Entity<LimitsPolicy>().ToTable("LimitsPolicies", SchemaNames.Default);
        modelBuilder.Entity<PricingQuote>().ToTable("PricingQuotes", SchemaNames.Default);

        // Partners
        modelBuilder.Entity<Partner>().ToTable("Partners", SchemaNames.Default);
        modelBuilder.Entity<PartnerBranch>().ToTable("PartnerBranches", SchemaNames.Default);
        // PartnerFundingAccount table name is set in PartnerFundingAccountConfiguration
        modelBuilder.Entity<Connector>().ToTable("Connectors", SchemaNames.Default);
        modelBuilder.Entity<RoutingRule>().ToTable("RoutingRules", SchemaNames.Default);
        modelBuilder.Entity<PayoutSchema>().ToTable("PayoutSchemas", SchemaNames.Default);
        modelBuilder.Entity<Transmission>().ToTable("Transmissions", SchemaNames.Default);

        // Temporary cross-module entities
        modelBuilder.Entity<PartyReadModel>().ToTable("Parties", SchemaNames.Default);
        modelBuilder.Entity<UserReadModel>().ToTable("Users", SchemaNames.Default);
        modelBuilder.Entity<CatalogBillerCategory>().ToTable("CatalogBillerCategories", SchemaNames.Default);
        modelBuilder.Entity<CatalogBiller>().ToTable("CatalogBillers", SchemaNames.Default);
        modelBuilder.Entity<CatalogBillerService>().ToTable("CatalogBillerServices", SchemaNames.Default);

        // Reference data read models (read-only projections of Platform entities)
        modelBuilder.Entity<CountryReadModel>().ToTable("Countries", SchemaNames.Default);
        modelBuilder.Entity<CurrencyReadModel>().ToTable("Currencies", SchemaNames.Default);
        modelBuilder.Entity<CountryCurrencyReadModel>().ToTable("CountryCurrencies", SchemaNames.Default);

        // PersonalFinance
        modelBuilder.Entity<PersonalProfile>().ToTable("PersonalProfiles", SchemaNames.Default);
        modelBuilder.Entity<Household>().ToTable("Households", SchemaNames.Default);
        modelBuilder.Entity<HouseholdMember>().ToTable("HouseholdMembers", SchemaNames.Default);
        modelBuilder.Entity<PersonalAccount>().ToTable("PersonalAccounts", SchemaNames.Default);
        modelBuilder.Entity<PersonalTransaction>().ToTable("PersonalTransactions", SchemaNames.Default);
        modelBuilder.Entity<CategorisationRule>().ToTable("CategorisationRules", SchemaNames.Default);
        modelBuilder.Entity<BudgetLine>().ToTable("BudgetLines", SchemaNames.Default);
        modelBuilder.Entity<Bill>().ToTable("Bills", SchemaNames.Default);
        modelBuilder.Entity<Subscription>().ToTable("Subscriptions", SchemaNames.Default);
        modelBuilder.Entity<Goal>().ToTable("Goals", SchemaNames.Default);
        modelBuilder.Entity<Budget>().ToTable("Budgets", SchemaNames.Default);

        // Apply EF configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FinanceDbContext).Assembly);

        // Apply tenant query filters for all ITenantScoped entities in this context
        ApplyTenantQueryFilters(modelBuilder);
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
