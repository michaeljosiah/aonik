using Aonik.PersonalFinance.Entities;
using Aonik.SharedKernel.Abstractions.Finance;
using Aonik.SharedKernel.Abstractions.Ordering;
using Aonik.SharedKernel.Abstractions.Platform;

namespace Aonik.PersonalFinance.Services;

/// <summary>
/// Read-model snapshot consumed by the FinancialLifeGraph builder.
///
/// Spec 027 Phase 3: Orders / Invoices / Payments / FxQuotes / Parties /
/// Relationships are exposed as SharedKernel DTOs so this record can travel
/// with the PersonalFinance module without dragging Aonik.Finance.Entities
/// or Aonik.Platform.Entities along.
/// </summary>
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
    IReadOnlyList<FxQuoteHistoryItem> FxQuotes,
    IReadOnlyList<OrderHistoryItem> Orders,
    IReadOnlyList<InvoiceHistoryItem> Invoices,
    IReadOnlyList<PaymentHistoryItem> PaymentIntents,
    Guid? SelfPartyId,
    IReadOnlyList<PartyHistoryItem> RelatedParties,
    IReadOnlyList<PartyRelationshipHistoryItem> PartyRelationships,
    IReadOnlyList<FinancialLifeGraphNode> NativeNodes,
    IReadOnlyList<FinancialLifeGraphEdge> NativeEdges);
