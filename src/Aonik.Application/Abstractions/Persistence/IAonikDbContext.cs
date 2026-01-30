using Aonik.Domain.Agents.Entities;
using Aonik.Domain.Ai.Entities;
using Aonik.Domain.Autonumbering.Entities;
using Aonik.Domain.Billing.Entities;
using Aonik.Domain.Cms.Entities;
using Aonik.Domain.Catalog.Entities;
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
using Microsoft.EntityFrameworkCore;
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

namespace Aonik.Application.Abstractions.Persistence;

public interface IAonikDbContext
{
    // Identity
    DbSet<Tenant> Tenants { get; set; }
    DbSet<TenantCountry> TenantCountries { get; set; }
    DbSet<TenantCurrency> TenantCurrencies { get; set; }
    DbSet<User> Users { get; set; }
    DbSet<Role> Roles { get; set; }
    DbSet<Permission> Permissions { get; set; }
    DbSet<UserRole> UserRoles { get; set; }
    DbSet<RolePermission> RolePermissions { get; set; }
    DbSet<UserParty> UserParties { get; set; }
    DbSet<VerificationChallenge> VerificationChallenges { get; set; }

    // Autonumbering
    DbSet<AutonumberProfile> AutonumberProfiles { get; set; }
    DbSet<AutonumberReservation> AutonumberReservations { get; set; }

    // Settings
    DbSet<Setting> Settings { get; set; }

    // Reference Data
    DbSet<ReferenceDataItem> ReferenceDataItems { get; set; }
    DbSet<Country> Countries { get; set; }
    DbSet<Currency> Currencies { get; set; }

    // Party
    DbSet<PartyEntity> Parties { get; set; }
    DbSet<PartyAddress> PartyAddresses { get; set; }
    DbSet<PartyContact> PartyContacts { get; set; }
    DbSet<PartyConsent> PartyConsents { get; set; }
    DbSet<PersonProfile> PersonProfiles { get; set; }
    DbSet<BusinessProfile> BusinessProfiles { get; set; }
    DbSet<ExternalAccount> ExternalAccounts { get; set; }
    DbSet<PartyRoleAssignment> PartyRoleAssignments { get; set; }
    DbSet<PartyRelationship> PartyRelationships { get; set; }

    // Ledger
    DbSet<LedgerEntity> Ledgers { get; set; }
    DbSet<LedgerAccount> LedgerAccounts { get; set; }
    DbSet<JournalEntry> JournalEntries { get; set; }
    DbSet<JournalEntryLine> JournalEntryLines { get; set; }
    DbSet<BalanceSnapshot> BalanceSnapshots { get; set; }

    // Payments
    DbSet<PaymentIntent> PaymentIntents { get; set; }
    DbSet<Payment> Payments { get; set; }
    DbSet<Payout> Payouts { get; set; }
    DbSet<Refund> Refunds { get; set; }
    DbSet<Chargeback> Chargebacks { get; set; }

    // Billing
    DbSet<Invoice> Invoices { get; set; }
    DbSet<InvoiceLine> InvoiceLines { get; set; }
    DbSet<CustomerAccount> CustomerAccounts { get; set; }
    DbSet<InvoiceAllocation> InvoiceAllocations { get; set; }
    DbSet<DunningPlan> DunningPlans { get; set; }

    // CMS
    DbSet<ContentBlock> ContentBlocks { get; set; }
    DbSet<ContentBlockMedia> ContentBlockMedia { get; set; }

    // Catalog
    DbSet<CatalogBillerCategory> CatalogBillerCategories { get; set; }
    DbSet<CatalogBiller> CatalogBillers { get; set; }
    DbSet<CatalogBillerService> CatalogBillerServices { get; set; }

    // Partners
    DbSet<Partner> Partners { get; set; }
    DbSet<PartnerBranch> PartnerBranches { get; set; }
    DbSet<Connector> Connectors { get; set; }
    DbSet<RoutingRule> RoutingRules { get; set; }
    DbSet<PayoutSchema> PayoutSchemas { get; set; }
    DbSet<Transmission> Transmissions { get; set; }

    // Pricing
    DbSet<FeePolicy> FeePolicies { get; set; }
    DbSet<FxQuote> FxQuotes { get; set; }
    DbSet<LimitsPolicy> LimitsPolicies { get; set; }
    DbSet<PricingQuote> PricingQuotes { get; set; }

    // Compliance
    DbSet<ScreeningCheck> ScreeningChecks { get; set; }
    DbSet<ComplianceCase> ComplianceCases { get; set; }
    DbSet<AuditLog> AuditLogs { get; set; }

    // Features
    DbSet<TenantFeature> TenantFeatures { get; set; }

    // Operations
    DbSet<WorkItem> WorkItems { get; set; }
    DbSet<Job> Jobs { get; set; }

    // Notifications
    DbSet<Notification> Notifications { get; set; }
    DbSet<WebhookSubscription> WebhookSubscriptions { get; set; }

    // AI
    DbSet<AiProvider> AiProviders { get; set; }
    DbSet<AiModel> AiModels { get; set; }
    DbSet<AiRoutePolicy> AiRoutePolicies { get; set; }
    DbSet<PromptSpec> PromptSpecs { get; set; }
    DbSet<ToolSpec> ToolSpecs { get; set; }
    DbSet<AiPolicy> AiPolicies { get; set; }
    DbSet<AiRun> AiRuns { get; set; }
    DbSet<AiTrace> AiTraces { get; set; }
    DbSet<AiFeedback> AiFeedbacks { get; set; }
    DbSet<EvalSuite> EvalSuites { get; set; }
    DbSet<EvalRun> EvalRuns { get; set; }
    DbSet<Insight> Insights { get; set; }
    DbSet<Signal> Signals { get; set; }

    // Agents
    DbSet<Agent> Agents { get; set; }
    DbSet<AgentRun> AgentRuns { get; set; }
    DbSet<OrchestratorPolicy> OrchestratorPolicies { get; set; }
    DbSet<Proposal> Proposals { get; set; }

    // Orders
    DbSet<Order> Orders { get; set; }
    DbSet<OrderItem> OrderItems { get; set; }
    DbSet<OrderPartyRole> OrderPartyRoles { get; set; }
    DbSet<OrderFundingRef> OrderFundingRefs { get; set; }
    DbSet<OrderFulfilmentRef> OrderFulfilmentRefs { get; set; }
    DbSet<OrderHistoryEvent> OrderHistoryEvents { get; set; }
    DbSet<OrderNote> OrderNotes { get; set; }

    // Personal Finance
    DbSet<PersonalProfile> PersonalProfiles { get; set; }
    DbSet<Household> Households { get; set; }
    DbSet<HouseholdMember> HouseholdMembers { get; set; }
    DbSet<PersonalTransaction> PersonalTransactions { get; set; }
    DbSet<CategorisationRule> CategorisationRules { get; set; }
    DbSet<BudgetLine> BudgetLines { get; set; }
    DbSet<Bill> Bills { get; set; }
    DbSet<Subscription> Subscriptions { get; set; }
    DbSet<Goal> Goals { get; set; }
    DbSet<Budget> Budgets { get; set; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
