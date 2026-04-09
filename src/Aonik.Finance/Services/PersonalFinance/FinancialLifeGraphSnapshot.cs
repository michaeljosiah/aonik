using Aonik.Finance.Entities;
using Aonik.Finance.Entities.Billing;
using Aonik.Finance.Entities.Orders;
using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Entities.PersonalFinance;

namespace Aonik.Finance.Services.PersonalFinance;

internal sealed record FinancialLifeGraphSnapshot(
    Guid TenantId,
    Guid UserId,
    PersonalProfile? PersonalProfile,
    Household? Household,
    IReadOnlyList<HouseholdMember> HouseholdMembers,
    IReadOnlyDictionary<Guid, string> HouseholdMemberDisplayNames,
    IReadOnlyList<PersonalAccount> Accounts,
    IReadOnlyList<PersonalAccount> HouseholdAccounts,
    IReadOnlyList<PersonalLinkedAccount> LinkedAccounts,
    IReadOnlyList<PersonalTransaction> Transactions,
    IReadOnlyList<Bill> Bills,
    IReadOnlyList<Goal> Goals,
    IReadOnlyList<Subscription> Subscriptions,
    IReadOnlyList<Entities.Pricing.FxQuote> FxQuotes,
    IReadOnlyList<Order> Orders,
    IReadOnlyList<Invoice> Invoices,
    IReadOnlyList<PaymentIntent> PaymentIntents,
    Guid? SelfPartyId,
    IReadOnlyList<PartyReadModel> RelatedParties,
    IReadOnlyList<PartyRelationshipReadModel> PartyRelationships,
    IReadOnlyList<FinancialLifeGraphNode> NativeNodes,
    IReadOnlyList<FinancialLifeGraphEdge> NativeEdges);
