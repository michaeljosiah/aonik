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

public class AonikDbContext : DbContext, IAonikDbContext
{
    // Identity
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    // Party
    public DbSet<PartyEntity> Parties => Set<PartyEntity>();
    public DbSet<PartyAddress> PartyAddresses => Set<PartyAddress>();
    public DbSet<PartyContact> PartyContacts => Set<PartyContact>();
    public DbSet<PartyConsent> PartyConsents => Set<PartyConsent>();
    public DbSet<PersonProfile> PersonProfiles => Set<PersonProfile>();
    public DbSet<BusinessProfile> BusinessProfiles => Set<BusinessProfile>();
    public DbSet<ExternalAccount> ExternalAccounts => Set<ExternalAccount>();
    public DbSet<PartyRoleAssignment> PartyRoleAssignments => Set<PartyRoleAssignment>();

    // Ledger
    public DbSet<LedgerEntity> Ledgers => Set<LedgerEntity>();
    public DbSet<LedgerAccount> LedgerAccounts => Set<LedgerAccount>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalEntryLine> JournalEntryLines => Set<JournalEntryLine>();
    public DbSet<BalanceSnapshot> BalanceSnapshots => Set<BalanceSnapshot>();

    // Payments
    public DbSet<PaymentIntent> PaymentIntents => Set<PaymentIntent>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Payout> Payouts => Set<Payout>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<Chargeback> Chargebacks => Set<Chargeback>();

    // Billing
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<CustomerAccount> CustomerAccounts => Set<CustomerAccount>();
    public DbSet<InvoiceAllocation> InvoiceAllocations => Set<InvoiceAllocation>();
    public DbSet<DunningPlan> DunningPlans => Set<DunningPlan>();

    // Partners
    public DbSet<Partner> Partners => Set<Partner>();
    public DbSet<PartnerBranch> PartnerBranches => Set<PartnerBranch>();
    public DbSet<Connector> Connectors => Set<Connector>();
    public DbSet<RoutingRule> RoutingRules => Set<RoutingRule>();
    public DbSet<PayoutSchema> PayoutSchemas => Set<PayoutSchema>();
    public DbSet<Transmission> Transmissions => Set<Transmission>();

    // Pricing
    public DbSet<FeePolicy> FeePolicies => Set<FeePolicy>();
    public DbSet<FxQuote> FxQuotes => Set<FxQuote>();
    public DbSet<LimitsPolicy> LimitsPolicies => Set<LimitsPolicy>();

    // Compliance
    public DbSet<ScreeningCheck> ScreeningChecks => Set<ScreeningCheck>();
    public DbSet<ComplianceCase> ComplianceCases => Set<ComplianceCase>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Operations
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();
    public DbSet<Job> Jobs => Set<Job>();

    // Notifications
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();

    // AI
    public DbSet<AiProvider> AiProviders => Set<AiProvider>();
    public DbSet<AiModel> AiModels => Set<AiModel>();
    public DbSet<AiRoutePolicy> AiRoutePolicies => Set<AiRoutePolicy>();
    public DbSet<PromptSpec> PromptSpecs => Set<PromptSpec>();
    public DbSet<ToolSpec> ToolSpecs => Set<ToolSpec>();
    public DbSet<AiPolicy> AiPolicies => Set<AiPolicy>();
    public DbSet<AiRun> AiRuns => Set<AiRun>();
    public DbSet<AiTrace> AiTraces => Set<AiTrace>();
    public DbSet<AiFeedback> AiFeedbacks => Set<AiFeedback>();
    public DbSet<EvalSuite> EvalSuites => Set<EvalSuite>();
    public DbSet<EvalRun> EvalRuns => Set<EvalRun>();
    public DbSet<Insight> Insights => Set<Insight>();
    public DbSet<Signal> Signals => Set<Signal>();

    // Agents
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<AgentRun> AgentRuns => Set<AgentRun>();
    public DbSet<OrchestratorPolicy> OrchestratorPolicies => Set<OrchestratorPolicy>();
    public DbSet<Proposal> Proposals => Set<Proposal>();

    // Orders
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderPartyRole> OrderPartyRoles => Set<OrderPartyRole>();
    public DbSet<OrderFundingRef> OrderFundingRefs => Set<OrderFundingRef>();
    public DbSet<OrderFulfilmentRef> OrderFulfilmentRefs => Set<OrderFulfilmentRef>();
    public DbSet<OrderHistoryEvent> OrderHistoryEvents => Set<OrderHistoryEvent>();
    public DbSet<OrderNote> OrderNotes => Set<OrderNote>();

    // Personal Finance
    public DbSet<PersonalProfile> PersonalProfiles => Set<PersonalProfile>();
    public DbSet<Household> Households => Set<Household>();
    public DbSet<HouseholdMember> HouseholdMembers => Set<HouseholdMember>();
    public DbSet<PersonalTransaction> PersonalTransactions => Set<PersonalTransaction>();
    public DbSet<CategorisationRule> CategorisationRules => Set<CategorisationRule>();
    public DbSet<BudgetLine> BudgetLines => Set<BudgetLine>();
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<Budget> Budgets => Set<Budget>();

    public AonikDbContext(DbContextOptions<AonikDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Apply all configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
