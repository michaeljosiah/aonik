using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Domain.Agents.Entities;
using Aonik.Domain.Ai.Entities;
using Aonik.Domain.Autonumbering.Entities;
using Aonik.Domain.Billing.Entities;
using Aonik.Domain.Catalog.Entities;
using Aonik.Domain.Cms.Entities;
using Aonik.Domain.Compliance.Entities;
using Aonik.Domain.Features.Entities;
using Aonik.Domain.Identity.Entities;
using Aonik.Domain.Notifications.Entities;
using Aonik.Domain.Operations.Entities;
using Aonik.Domain.Orders.Entities;
using Aonik.Domain.Partners.Entities;
using Aonik.Domain.Payments.Entities;
using Aonik.Domain.Party.Entities;
using Aonik.Domain.PersonalFinance.Entities;
using Aonik.Domain.Pricing.Entities;
using Aonik.Domain.ReferenceData.Entities;
using Aonik.Domain.Settings.Entities;
using Aonik.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using LedgerEntity = Aonik.Domain.Ledger.Entities.Ledger;
using PartyEntity = Aonik.Domain.Party.Entities.Party;
using PartyAddress = Aonik.Domain.Party.Entities.PartyAddress;
using PartyContact = Aonik.Domain.Party.Entities.PartyContact;
using PartyConsent = Aonik.Domain.Party.Entities.PartyConsent;
using PersonProfile = Aonik.Domain.Party.Entities.PersonProfile;
using BusinessProfile = Aonik.Domain.Party.Entities.BusinessProfile;
using ExternalAccount = Aonik.Domain.Party.Entities.ExternalAccount;
using PartyRoleAssignment = Aonik.Domain.Party.Entities.PartyRoleAssignment;
using LedgerAccount = Aonik.Domain.Ledger.Entities.LedgerAccount;
using JournalEntry = Aonik.Domain.Ledger.Entities.JournalEntry;
using JournalEntryLine = Aonik.Domain.Ledger.Entities.JournalEntryLine;
using BalanceSnapshot = Aonik.Domain.Ledger.Entities.BalanceSnapshot;

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

    // Ledger
    public virtual DbSet<LedgerEntity> Ledgers { get; set; } = null!;
    public virtual DbSet<LedgerAccount> LedgerAccounts { get; set; } = null!;
    public virtual DbSet<JournalEntry> JournalEntries { get; set; } = null!;
    public virtual DbSet<JournalEntryLine> JournalEntryLines { get; set; } = null!;
    public virtual DbSet<BalanceSnapshot> BalanceSnapshots { get; set; } = null!;

    // Payments
    public virtual DbSet<PaymentIntent> PaymentIntents { get; set; } = null!;
    public virtual DbSet<Payment> Payments { get; set; } = null!;
    public virtual DbSet<Payout> Payouts { get; set; } = null!;
    public virtual DbSet<Refund> Refunds { get; set; } = null!;
    public virtual DbSet<Chargeback> Chargebacks { get; set; } = null!;

    // Billing
    public virtual DbSet<Invoice> Invoices { get; set; } = null!;
    public virtual DbSet<InvoiceLine> InvoiceLines { get; set; } = null!;
    public virtual DbSet<CustomerAccount> CustomerAccounts { get; set; } = null!;
    public virtual DbSet<InvoiceAllocation> InvoiceAllocations { get; set; } = null!;
    public virtual DbSet<DunningPlan> DunningPlans { get; set; } = null!;

    // CMS
    public virtual DbSet<ContentBlock> ContentBlocks { get; set; } = null!;
    public virtual DbSet<ContentBlockMedia> ContentBlockMedia { get; set; } = null!;

    // Catalog
    public virtual DbSet<CatalogBillerCategory> CatalogBillerCategories { get; set; } = null!;
    public virtual DbSet<CatalogBiller> CatalogBillers { get; set; } = null!;
    public virtual DbSet<CatalogBillerService> CatalogBillerServices { get; set; } = null!;

    // Partners
    public virtual DbSet<Partner> Partners { get; set; } = null!;
    public virtual DbSet<PartnerBranch> PartnerBranches { get; set; } = null!;
    public virtual DbSet<Connector> Connectors { get; set; } = null!;
    public virtual DbSet<RoutingRule> RoutingRules { get; set; } = null!;
    public virtual DbSet<PayoutSchema> PayoutSchemas { get; set; } = null!;
    public virtual DbSet<Transmission> Transmissions { get; set; } = null!;
    public virtual DbSet<PartnerFundingAccount> PartnerFundingAccounts { get; set; } = null!;

    // Pricing
    public virtual DbSet<FeePolicy> FeePolicies { get; set; } = null!;
    public virtual DbSet<FxQuote> FxQuotes { get; set; } = null!;
    public virtual DbSet<FxRateSource> FxRateSources { get; set; } = null!;
    public virtual DbSet<FxRefreshSchedule> FxRefreshSchedules { get; set; } = null!;
    public virtual DbSet<FxSpreadPolicy> FxSpreadPolicies { get; set; } = null!;
    public virtual DbSet<LimitsPolicy> LimitsPolicies { get; set; } = null!;
    public virtual DbSet<PricingQuote> PricingQuotes { get; set; } = null!;

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

    // AI
    public virtual DbSet<AiProvider> AiProviders { get; set; } = null!;
    public virtual DbSet<AiModel> AiModels { get; set; } = null!;
    public virtual DbSet<AiRoutePolicy> AiRoutePolicies { get; set; } = null!;
    public virtual DbSet<PromptSpec> PromptSpecs { get; set; } = null!;
    public virtual DbSet<ToolSpec> ToolSpecs { get; set; } = null!;
    public virtual DbSet<AiPolicy> AiPolicies { get; set; } = null!;
    public virtual DbSet<AiRun> AiRuns { get; set; } = null!;
    public virtual DbSet<AiTrace> AiTraces { get; set; } = null!;
    public virtual DbSet<AiFeedback> AiFeedbacks { get; set; } = null!;
    public virtual DbSet<EvalSuite> EvalSuites { get; set; } = null!;
    public virtual DbSet<EvalRun> EvalRuns { get; set; } = null!;
    public virtual DbSet<Insight> Insights { get; set; } = null!;
    public virtual DbSet<Signal> Signals { get; set; } = null!;

    // Agents
    public virtual DbSet<Agent> Agents { get; set; } = null!;
    public virtual DbSet<AgentRun> AgentRuns { get; set; } = null!;
    public virtual DbSet<OrchestratorPolicy> OrchestratorPolicies { get; set; } = null!;
    public virtual DbSet<Proposal> Proposals { get; set; } = null!;

    // Orders
    public virtual DbSet<Order> Orders { get; set; } = null!;
    public virtual DbSet<OrderItem> OrderItems { get; set; } = null!;
    public virtual DbSet<OrderPartyRole> OrderPartyRoles { get; set; } = null!;
    public virtual DbSet<OrderFundingRef> OrderFundingRefs { get; set; } = null!;
    public virtual DbSet<OrderFulfilmentRef> OrderFulfilmentRefs { get; set; } = null!;
    public virtual DbSet<OrderHistoryEvent> OrderHistoryEvents { get; set; } = null!;
    public virtual DbSet<OrderNote> OrderNotes { get; set; } = null!;

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

        // Apply tenant query filters
        ApplyTenantQueryFilters(modelBuilder);

        // Apply nullable tenant filters for entities with optional TenantId
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(Agent));
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(OrchestratorPolicy));
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(AiRoutePolicy));
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(ReferenceDataItem));
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(Country));
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(Currency));
        ApplyNullableTenantQueryFilter(modelBuilder, typeof(NotificationTemplate));
    }

    protected override void OnBeforeSave()
    {
        PopulateOrderCompatibilityColumns();
    }

    protected override bool IsGlobalEntity(object entity)
    {
        return entity is Role;
    }

    private void PopulateOrderCompatibilityColumns()
    {
        var orderEntries = ChangeTracker.Entries<Order>()
            .Where(entry => entry.State == EntityState.Added)
            .ToList();

        if (orderEntries.Count == 0)
        {
            return;
        }

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
