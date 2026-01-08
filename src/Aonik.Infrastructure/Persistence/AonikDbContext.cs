using Aonik.Application.Abstractions.Persistence;
using Aonik.Domain.Agents.Entities;
using Aonik.Domain.Ai.Entities;
using Aonik.Domain.Billing.Entities;
using Aonik.Domain.Compliance.Entities;
using Aonik.Domain.Identity.Entities;
using Aonik.Domain.Notifications.Entities;
using Aonik.Domain.Operations.Entities;
using Aonik.Domain.Orders.Entities;
using Aonik.Domain.Partners.Entities;
using Aonik.Domain.Payments.Entities;
using Aonik.Domain.PersonalFinance.Entities;
using Aonik.Domain.Pricing.Entities;
using Aonik.Infrastructure.Multitenancy;
using Aonik.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
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

public class AonikDbContext : DbContext, IAonikDbContext
{
    private readonly ITenantProvider? _tenantProvider;

    // Identity
    public virtual DbSet<Tenant> Tenants { get; set; } = null!;
    public virtual DbSet<User> Users { get; set; } = null!;
    public virtual DbSet<Role> Roles { get; set; } = null!;
    public virtual DbSet<Permission> Permissions { get; set; } = null!;
    public virtual DbSet<UserRole> UserRoles { get; set; } = null!;
    public virtual DbSet<RolePermission> RolePermissions { get; set; } = null!;

    // Party
    public virtual DbSet<PartyEntity> Parties { get; set; } = null!;
    public virtual DbSet<PartyAddress> PartyAddresses { get; set; } = null!;
    public virtual DbSet<PartyContact> PartyContacts { get; set; } = null!;
    public virtual DbSet<PartyConsent> PartyConsents { get; set; } = null!;
    public virtual DbSet<PersonProfile> PersonProfiles { get; set; } = null!;
    public virtual DbSet<BusinessProfile> BusinessProfiles { get; set; } = null!;
    public virtual DbSet<ExternalAccount> ExternalAccounts { get; set; } = null!;
    public virtual DbSet<PartyRoleAssignment> PartyRoleAssignments { get; set; } = null!;

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

    // Partners
    public virtual DbSet<Partner> Partners { get; set; } = null!;
    public virtual DbSet<PartnerBranch> PartnerBranches { get; set; } = null!;
    public virtual DbSet<Connector> Connectors { get; set; } = null!;
    public virtual DbSet<RoutingRule> RoutingRules { get; set; } = null!;
    public virtual DbSet<PayoutSchema> PayoutSchemas { get; set; } = null!;
    public virtual DbSet<Transmission> Transmissions { get; set; } = null!;

    // Pricing
    public virtual DbSet<FeePolicy> FeePolicies { get; set; } = null!;
    public virtual DbSet<FxQuote> FxQuotes { get; set; } = null!;
    public virtual DbSet<LimitsPolicy> LimitsPolicies { get; set; } = null!;

    // Compliance
    public virtual DbSet<ScreeningCheck> ScreeningChecks { get; set; } = null!;
    public virtual DbSet<ComplianceCase> ComplianceCases { get; set; } = null!;
    public virtual DbSet<AuditLog> AuditLogs { get; set; } = null!;

    // Operations
    public virtual DbSet<WorkItem> WorkItems { get; set; } = null!;
    public virtual DbSet<Job> Jobs { get; set; } = null!;

    // Notifications
    public virtual DbSet<Notification> Notifications { get; set; } = null!;
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
    public virtual DbSet<OrderPartyRole> OrderPartyRoles { get; set; } = null!;
    public virtual DbSet<OrderFundingRef> OrderFundingRefs { get; set; } = null!;
    public virtual DbSet<OrderFulfilmentRef> OrderFulfilmentRefs { get; set; } = null!;
    public virtual DbSet<OrderHistoryEvent> OrderHistoryEvents { get; set; } = null!;
    public virtual DbSet<OrderNote> OrderNotes { get; set; } = null!;

    // Personal Finance
    public virtual DbSet<PersonalProfile> PersonalProfiles { get; set; } = null!;
    public virtual DbSet<Household> Households { get; set; } = null!;
    public virtual DbSet<HouseholdMember> HouseholdMembers { get; set; } = null!;
    public virtual DbSet<PersonalTransaction> PersonalTransactions { get; set; } = null!;
    public virtual DbSet<CategorisationRule> CategorisationRules { get; set; } = null!;
    public virtual DbSet<BudgetLine> BudgetLines { get; set; } = null!;
    public virtual DbSet<Bill> Bills { get; set; } = null!;
    public virtual DbSet<Subscription> Subscriptions { get; set; } = null!;
    public virtual DbSet<Goal> Goals { get; set; } = null!;
    public virtual DbSet<Budget> Budgets { get; set; } = null!;

    public AonikDbContext(DbContextOptions<AonikDbContext> options, ITenantProvider? tenantProvider = null) 
        : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Apply all configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        
        // Apply tenant query filters
        ApplyTenantQueryFilters(modelBuilder);
    }

    private void ApplyTenantQueryFilters(ModelBuilder modelBuilder)
    {
        // Only apply filters if tenant provider is available
        if (_tenantProvider == null)
            return;

        // Try to get current tenant ID - if not available, skip filter application
        // (e.g., during migrations, seeding, or background jobs without tenant context)
        if (!_tenantProvider.TryGetCurrentTenantId(out var currentTenantId))
            return;

        // Get all entity types that implement ITenantScoped
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;

            // Check if entity implements ITenantScoped
            if (typeof(ITenantScoped).IsAssignableFrom(clrType))
            {
                // Create filter expression: entity => entity.TenantId == currentTenantId
                var parameter = Expression.Parameter(clrType, "e");
                var property = Expression.Property(parameter, nameof(ITenantScoped.TenantId));
                var tenantIdValue = Expression.Constant(currentTenantId);
                var equals = Expression.Equal(property, tenantIdValue);
                var lambda = Expression.Lambda(equals, parameter);

                // Apply the filter
                modelBuilder.Entity(clrType).HasQueryFilter(lambda);
            }
        }
    }
}
