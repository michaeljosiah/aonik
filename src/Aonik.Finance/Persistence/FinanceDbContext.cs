using Aonik.Finance.Entities;
using Aonik.Finance.Entities.Billing;
using Aonik.Finance.Entities.Catalog;
using Aonik.Finance.Entities.Ledger;
using Aonik.Finance.Entities.Orders;
using Aonik.Finance.Entities.Partners;
using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Entities.Pricing;
using Aonik.Finance.Entities.ReferenceData;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Persistence;

/// <summary>
/// Module-scoped DbContext for the Finance domain.
/// Owns Ledger, Payments, Billing, Orders, Pricing, Partners, and Catalog entities,
/// plus the Platform read models Finance joins against (Parties / Users / Countries / …).
/// PersonalFinance entities are owned solely by <c>PersonalFinanceDbContext</c>
/// (Spec 027 S3, #126) — they are no longer part of this context's model.
/// Inherits multi-tenancy enforcement and audit stamping from <see cref="AonikDbContextBase"/>.
///
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

    /// <summary>Spec 007 — tokenised customer card vault (no PCI data stored).</summary>
    public DbSet<PaymentMethod> PaymentMethods { get; set; } = null!;

    // Partner integration abstraction (spec 031): payout / collection / bill-payment execution records.
    public DbSet<ExternalPayoutAccount> ExternalPayoutAccounts { get; set; } = null!;
    public DbSet<PayoutReversal> PayoutReversals { get; set; } = null!;
    public DbSet<BillValidation> BillValidations { get; set; } = null!;
    public DbSet<PartnerBillPayment> PartnerBillPayments { get; set; } = null!;

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
    public DbSet<CredentialBundle> CredentialBundles { get; set; } = null!;
    public DbSet<RoutingRule> RoutingRules { get; set; } = null!;
    public DbSet<PayoutSchema> PayoutSchemas { get; set; } = null!;
    public DbSet<Transmission> Transmissions { get; set; } = null!;

    // Partner integration abstraction (spec 031): institution directory, connector capability / code maps, webhook inbox.
    public DbSet<FinancialInstitution> FinancialInstitutions { get; set; } = null!;
    public DbSet<ConnectorInstitutionCode> ConnectorInstitutionCodes { get; set; } = null!;
    public DbSet<ConnectorCapability> ConnectorCapabilities { get; set; } = null!;
    public DbSet<ConnectorBillerMapping> ConnectorBillerMappings { get; set; } = null!;
    public DbSet<PartnerWebhookEvent> PartnerWebhookEvents { get; set; } = null!;

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

    /// <summary>Read-only projection of UserParty bridge (authoritative entity in Platform module)</summary>
    public DbSet<UserPartyReadModel> UserParties { get; set; } = null!;

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

    // ── PersonalFinance entities: owned solely by PersonalFinanceDbContext ──
    // Spec 027 S3 (#126): the Account* + PersonalFinance DbSets, their table
    // mappings, and the PersonalFinance-assembly config scan were removed from
    // this context. PersonalFinanceDbContext (Aonik.PersonalFinance) is now the
    // sole owner of those entities. No schema change — AonikDbContext (the
    // canonical migration stream) still owns the physical tables.

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

        // Spec 041 / ADR-011: Order EF configs relocated to Aonik.Ordering (namespace preserved).
        // FinanceDbContext still maps the Order DbSets for the type-specific OrderService, so it
        // applies the Order configs from the Ordering assembly too.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Order).Assembly);

        // Spec 027 S3 (#126): the PersonalFinance-assembly config scan was
        // removed. Those configs (and their entity types) now belong solely to
        // PersonalFinanceDbContext, so scanning them here would re-admit the PF
        // entities into this context's model. The Platform read models this
        // context keeps (Parties / Users / Countries / …) are configured in the
        // Finance assembly scan above, so dropping the PF scan is safe.

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
        MapTable<PaymentMethod>(modelBuilder, "PaymentMethods");
        MapTable<ExternalPayoutAccount>(modelBuilder, "ExternalPayoutAccounts");
        MapTable<PayoutReversal>(modelBuilder, "PayoutReversals");
        MapTable<BillValidation>(modelBuilder, "BillValidations");
        MapTable<PartnerBillPayment>(modelBuilder, "PartnerBillPayments");

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
        MapTable<CredentialBundle>(modelBuilder, "CredentialBundles");
        MapTable<RoutingRule>(modelBuilder, "RoutingRules");
        MapTable<PayoutSchema>(modelBuilder, "PayoutSchemas");
        MapTable<Transmission>(modelBuilder, "Transmissions");
        MapTable<FinancialInstitution>(modelBuilder, "FinancialInstitutions");
        MapTable<ConnectorInstitutionCode>(modelBuilder, "ConnectorInstitutionCodes");
        MapTable<ConnectorCapability>(modelBuilder, "ConnectorCapabilities");
        MapTable<ConnectorBillerMapping>(modelBuilder, "ConnectorBillerMappings");
        MapTable<PartnerWebhookEvent>(modelBuilder, "PartnerWebhookEvents");

        MapTable<CatalogBillerCategory>(modelBuilder, "CatalogBillerCategories");
        MapTable<CatalogBiller>(modelBuilder, "CatalogBillers");
        MapTable<CatalogBillerService>(modelBuilder, "CatalogBillerServices");

        // Spec 027 S3 (#126): PersonalFinance table mappings removed — those
        // entities are no longer part of this context's model. They are mapped
        // by PersonalFinanceDbContext, which produces the same Ank-prefixed
        // physical table names the canonical AonikDbContext owns.

        MapPlatformTable<PartyReadModel>(modelBuilder, "Parties");
        MapPlatformTable<PartyRelationshipReadModel>(modelBuilder, "PartyRelationships");
        MapPlatformTable<UserReadModel>(modelBuilder, "Users");
        MapPlatformTable<UserPartyReadModel>(modelBuilder, "UserParties");
        MapPlatformTable<CountryReadModel>(modelBuilder, "Countries");
        MapPlatformTable<CurrencyReadModel>(modelBuilder, "Currencies");
        MapPlatformTable<CountryCurrencyReadModel>(modelBuilder, "CountryCurrencies");
    }

    private static void MapTable<TEntity>(ModelBuilder modelBuilder, string tableName)
        where TEntity : class
        => MapModuleTable<TEntity>(modelBuilder, ModuleTablePrefixes.Finance, tableName);

    private static void MapPlatformTable<TEntity>(ModelBuilder modelBuilder, string tableName)
        where TEntity : class
        => MapModuleTable<TEntity>(modelBuilder, ModuleTablePrefixes.Platform, tableName);

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
