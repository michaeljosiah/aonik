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
using LedgerEntity = Aonik.Domain.Ledger.Entities.Ledger;
using PartyEntity = Aonik.Domain.Party.Entities.Party;

namespace Aonik.Application.Abstractions.Persistence;

public interface IAonikDbContext
{
    // Identity
    DbSet<Tenant> Tenants { get; }
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<RolePermission> RolePermissions { get; }

    // Party
    DbSet<PartyEntity> Parties { get; }
    DbSet<Aonik.Domain.Party.Entities.PartyAddress> PartyAddresses { get; }
    DbSet<Aonik.Domain.Party.Entities.PartyContact> PartyContacts { get; }
    DbSet<Aonik.Domain.Party.Entities.PartyConsent> PartyConsents { get; }
    DbSet<Aonik.Domain.Party.Entities.PersonProfile> PersonProfiles { get; }
    DbSet<Aonik.Domain.Party.Entities.BusinessProfile> BusinessProfiles { get; }
    DbSet<Aonik.Domain.Party.Entities.ExternalAccount> ExternalAccounts { get; }
    DbSet<Aonik.Domain.Party.Entities.PartyRoleAssignment> PartyRoleAssignments { get; }

    // Ledger
    DbSet<LedgerEntity> Ledgers { get; }
    DbSet<Aonik.Domain.Ledger.Entities.LedgerAccount> LedgerAccounts { get; }
    DbSet<Aonik.Domain.Ledger.Entities.JournalEntry> JournalEntries { get; }
    DbSet<Aonik.Domain.Ledger.Entities.JournalEntryLine> JournalEntryLines { get; }
    DbSet<Aonik.Domain.Ledger.Entities.BalanceSnapshot> BalanceSnapshots { get; }

    // Payments
    DbSet<PaymentIntent> PaymentIntents { get; }
    DbSet<Payment> Payments { get; }
    DbSet<Payout> Payouts { get; }
    DbSet<Refund> Refunds { get; }
    DbSet<Chargeback> Chargebacks { get; }

    // Billing
    DbSet<Invoice> Invoices { get; }
    DbSet<InvoiceLine> InvoiceLines { get; }
    DbSet<CustomerAccount> CustomerAccounts { get; }
    DbSet<InvoiceAllocation> InvoiceAllocations { get; }
    DbSet<DunningPlan> DunningPlans { get; }

    // Partners
    DbSet<Partner> Partners { get; }
    DbSet<PartnerBranch> PartnerBranches { get; }
    DbSet<Connector> Connectors { get; }
    DbSet<RoutingRule> RoutingRules { get; }
    DbSet<PayoutSchema> PayoutSchemas { get; }
    DbSet<Transmission> Transmissions { get; }

    // Pricing
    DbSet<FeePolicy> FeePolicies { get; }
    DbSet<FxQuote> FxQuotes { get; }
    DbSet<LimitsPolicy> LimitsPolicies { get; }

    // Compliance
    DbSet<ScreeningCheck> ScreeningChecks { get; }
    DbSet<ComplianceCase> ComplianceCases { get; }
    DbSet<AuditLog> AuditLogs { get; }

    // Operations
    DbSet<WorkItem> WorkItems { get; }
    DbSet<Job> Jobs { get; }

    // Notifications
    DbSet<Notification> Notifications { get; }
    DbSet<WebhookSubscription> WebhookSubscriptions { get; }

    // AI
    DbSet<AiProvider> AiProviders { get; }
    DbSet<AiModel> AiModels { get; }
    DbSet<AiRoutePolicy> AiRoutePolicies { get; }
    DbSet<PromptSpec> PromptSpecs { get; }
    DbSet<ToolSpec> ToolSpecs { get; }
    DbSet<AiPolicy> AiPolicies { get; }
    DbSet<AiRun> AiRuns { get; }
    DbSet<AiTrace> AiTraces { get; }
    DbSet<AiFeedback> AiFeedbacks { get; }
    DbSet<EvalSuite> EvalSuites { get; }
    DbSet<EvalRun> EvalRuns { get; }
    DbSet<Insight> Insights { get; }
    DbSet<Signal> Signals { get; }

    // Agents
    DbSet<Agent> Agents { get; }
    DbSet<AgentRun> AgentRuns { get; }
    DbSet<OrchestratorPolicy> OrchestratorPolicies { get; }
    DbSet<Proposal> Proposals { get; }

    // Orders
    DbSet<Order> Orders { get; }
    DbSet<OrderPartyRole> OrderPartyRoles { get; }
    DbSet<OrderFundingRef> OrderFundingRefs { get; }
    DbSet<OrderFulfilmentRef> OrderFulfilmentRefs { get; }
    DbSet<OrderHistoryEvent> OrderHistoryEvents { get; }
    DbSet<OrderNote> OrderNotes { get; }

    // Personal Finance
    DbSet<Aonik.Domain.PersonalFinance.Entities.PersonalProfile> PersonalFinanceProfiles { get; }
    DbSet<Household> Households { get; }
    DbSet<HouseholdMember> HouseholdMembers { get; }
    DbSet<PersonalTransaction> PersonalTransactions { get; }
    DbSet<CategorisationRule> CategorisationRules { get; }
    DbSet<BudgetLine> BudgetLines { get; }
    DbSet<Bill> Bills { get; }
    DbSet<Subscription> Subscriptions { get; }
    DbSet<Goal> Goals { get; }
    DbSet<Budget> Budgets { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
