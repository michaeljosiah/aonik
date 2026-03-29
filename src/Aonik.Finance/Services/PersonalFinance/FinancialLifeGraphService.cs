using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Entities.Billing;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Finance.Entities;
using Aonik.Finance.Entities.Orders;
using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Entities.PersonalFinance;

namespace Aonik.Finance.Services.PersonalFinance;

internal sealed class FinancialLifeGraphService : IFinancialLifeGraphService
{
    private readonly FinancialLifeGraphHydrationService _hydrationService;

    public FinancialLifeGraphService(
        FinancialLifeGraphHydrationService hydrationService)
    {
        _hydrationService = hydrationService;
    }

    public async Task<FinancialLifeGraphResponse> GetGraphAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _hydrationService.GetSnapshotAsync(cancellationToken);
        return BuildGraph(snapshot);
    }

    public async Task<FinancialLifeGraphSummaryResponse> GetGraphSummaryAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _hydrationService.GetSnapshotAsync(cancellationToken);
        return BuildSummary(snapshot);
    }

    public async Task<IReadOnlyList<UpcomingObligationResponse>> GetUpcomingObligationsAsync(
        int withinDays = 30,
        CancellationToken cancellationToken = default)
    {
        if (withinDays <= 0)
        {
            throw new ArgumentException("withinDays must be greater than 0.", nameof(withinDays));
        }

        var snapshot = await _hydrationService.GetSnapshotAsync(cancellationToken);
        return BuildUpcomingObligations(snapshot, withinDays);
    }

    public async Task<HouseholdFinanceContextResponse> GetHouseholdFinanceContextAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _hydrationService.GetSnapshotAsync(cancellationToken);

        if (snapshot.Household == null)
        {
            return new HouseholdFinanceContextResponse(false, null, 0, [], []);
        }

        var graph = BuildGraph(snapshot);
        var householdPrefix = FinancialLifeGraphFormatting.BuildNodeId(FinancialLifeGraphNodeKeys.Household, snapshot.Household.Id);
        var memberPrefix = "household-member:";

        return new HouseholdFinanceContextResponse(
            true,
            snapshot.Household.Id,
            snapshot.HouseholdMembers.Count,
            graph.Nodes.Where(item => string.Equals(item.NodeId, householdPrefix, StringComparison.OrdinalIgnoreCase) || item.NodeId.StartsWith(memberPrefix, StringComparison.OrdinalIgnoreCase)).ToList(),
            graph.Edges.Where(item => string.Equals(item.FromNodeId, householdPrefix, StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.ToNodeId, householdPrefix, StringComparison.OrdinalIgnoreCase)
                || item.FromNodeId.StartsWith(memberPrefix, StringComparison.OrdinalIgnoreCase)
                || item.ToNodeId.StartsWith(memberPrefix, StringComparison.OrdinalIgnoreCase)).ToList());
    }

    public async Task<RelatedPartyFinanceContextResponse> GetRelatedPartyFinanceContextAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _hydrationService.GetSnapshotAsync(cancellationToken);
        var response = snapshot.RelatedParties
            .Select(party =>
            {
                var relationship = snapshot.PartyRelationships.FirstOrDefault(item => item.FromPartyId == party.Id || item.ToPartyId == party.Id);
                return new RelatedPartyFinanceContextItemResponse(
                    party.Id,
                    party.DisplayName,
                    relationship?.RelationshipTypeCode,
                    relationship?.Notes);
            })
            .ToList();

        return new RelatedPartyFinanceContextResponse(response);
    }

    /// <summary>
    /// Builds the full graph read model from a snapshot. Used by traversal service.
    /// </summary>
    internal static FinancialLifeGraphResponse BuildGraphFromSnapshot(FinancialLifeGraphSnapshot snapshot)
        => BuildGraph(snapshot);

    private static FinancialLifeGraphResponse BuildGraph(FinancialLifeGraphSnapshot snapshot)
    {
        var nodes = new List<FinancialLifeGraphNodeResponse>();
        var edges = new List<FinancialLifeGraphEdgeResponse>();

        var userNodeId = FinancialLifeGraphFormatting.BuildNodeId(FinancialLifeGraphNodeKeys.User, snapshot.UserId);
        nodes.Add(new FinancialLifeGraphNodeResponse(
            userNodeId,
            FinancialLifeGraphNodeTypes.UserRoot,
            "Current User",
            "User",
            snapshot.UserId,
            FinancialLifeGraphFormatting.SerializeMetadata(new
            {
                snapshot.TenantId,
                snapshot.UserId,
                snapshot.PersonalProfile?.PartyId,
                snapshot.PersonalProfile?.HouseholdId
            })));

        if (snapshot.Household != null)
        {
            var householdNodeId = FinancialLifeGraphFormatting.BuildNodeId(FinancialLifeGraphNodeKeys.Household, snapshot.Household.Id);
            nodes.Add(new FinancialLifeGraphNodeResponse(
                householdNodeId,
                FinancialLifeGraphNodeTypes.Household,
                snapshot.Household.Name,
                nameof(Household),
                snapshot.Household.Id,
                FinancialLifeGraphFormatting.SerializeMetadata(new
                {
                    MemberCount = snapshot.HouseholdMembers.Count,
                    snapshot.Household.CreatedAt
                })));
            edges.Add(new FinancialLifeGraphEdgeResponse(userNodeId, FinancialLifeGraphPredicates.BelongsToHousehold, householdNodeId, null));

            foreach (var member in snapshot.HouseholdMembers)
            {
                var memberNodeId = FinancialLifeGraphFormatting.BuildNodeId(FinancialLifeGraphNodeKeys.HouseholdMember, member.Id);
                nodes.Add(new FinancialLifeGraphNodeResponse(
                    memberNodeId,
                    FinancialLifeGraphNodeTypes.HouseholdMember,
                    snapshot.HouseholdMemberDisplayNames.TryGetValue(member.Id, out var householdMemberName)
                        ? householdMemberName
                        : member.UserId == snapshot.UserId ? "You" : $"Member {member.UserId}",
                    nameof(HouseholdMember),
                    member.Id,
                    FinancialLifeGraphFormatting.SerializeMetadata(new
                    {
                        member.UserId,
                        member.Role,
                        member.PermissionsJson
                    })));
                edges.Add(new FinancialLifeGraphEdgeResponse(householdNodeId, FinancialLifeGraphPredicates.HouseholdHasMember, memberNodeId, null));
            }
        }

        foreach (var account in snapshot.Accounts)
        {
            var accountNodeId = FinancialLifeGraphFormatting.BuildNodeId(FinancialLifeGraphNodeKeys.PersonalAccount, account.Id);
            nodes.Add(new FinancialLifeGraphNodeResponse(
                accountNodeId,
                FinancialLifeGraphNodeTypes.PersonalAccount,
                account.Name,
                nameof(PersonalAccount),
                account.Id,
                FinancialLifeGraphFormatting.SerializeMetadata(new
                {
                    account.AccountType,
                    account.Currency,
                    account.InstitutionName,
                    account.Status,
                    account.AccountSubtype,
                    account.Last4,
                    account.IsArchived
                })));
            edges.Add(new FinancialLifeGraphEdgeResponse(userNodeId, FinancialLifeGraphPredicates.OwnsAccount, accountNodeId, null));
        }

        foreach (var linkedAccount in snapshot.LinkedAccounts)
        {
            var linkedAccountNodeId = FinancialLifeGraphFormatting.BuildNodeId(FinancialLifeGraphNodeKeys.LinkedAccount, linkedAccount.Id);
            var parentAccountNodeId = FinancialLifeGraphFormatting.BuildNodeId(FinancialLifeGraphNodeKeys.PersonalAccount, linkedAccount.PersonalAccountId);
            nodes.Add(new FinancialLifeGraphNodeResponse(
                linkedAccountNodeId,
                FinancialLifeGraphNodeTypes.PersonalLinkedAccount,
                linkedAccount.Name,
                nameof(PersonalLinkedAccount),
                linkedAccount.Id,
                FinancialLifeGraphFormatting.SerializeMetadata(new
                {
                    linkedAccount.ProviderAccountReference,
                    linkedAccount.AccountType,
                    linkedAccount.AccountSubtype,
                    linkedAccount.Currency,
                    linkedAccount.Status,
                    linkedAccount.Last4,
                    linkedAccount.LastSyncedAt,
                        linkedAccount.LastSyncStatus
                })));
            edges.Add(new FinancialLifeGraphEdgeResponse(parentAccountNodeId, FinancialLifeGraphPredicates.UsesLinkedAccount, linkedAccountNodeId, null));
        }

        foreach (var transaction in snapshot.Transactions)
        {
            var transactionNodeId = FinancialLifeGraphFormatting.BuildNodeId(FinancialLifeGraphNodeKeys.PersonalTransaction, transaction.Id);
            nodes.Add(new FinancialLifeGraphNodeResponse(
                transactionNodeId,
                FinancialLifeGraphNodeTypes.PersonalTransaction,
                transaction.Merchant ?? transaction.Description ?? $"Transaction {transaction.Id}",
                nameof(PersonalTransaction),
                transaction.Id,
                FinancialLifeGraphFormatting.SerializeMetadata(new
                {
                    transaction.Amount,
                    transaction.Currency,
                    transaction.OccurredAt,
                    transaction.Merchant,
                    transaction.Description,
                    transaction.Category,
                    transaction.SubCategory,
                    transaction.SourceType,
                    transaction.ClassificationMethod,
                    transaction.ReviewStatus,
                    transaction.Notes
                })));

            if (transaction.PersonalAccountId.HasValue)
            {
                var accountNodeId = FinancialLifeGraphFormatting.BuildNodeId(FinancialLifeGraphNodeKeys.PersonalAccount, transaction.PersonalAccountId.Value);
                edges.Add(new FinancialLifeGraphEdgeResponse(
                    accountNodeId,
                    FinancialLifeGraphPredicates.HasTransaction,
                    transactionNodeId,
                    null));
                edges.Add(new FinancialLifeGraphEdgeResponse(
                    transactionNodeId,
                    FinancialLifeGraphPredicates.UsesAccount,
                    accountNodeId,
                    null));
            }
            else
            {
                edges.Add(new FinancialLifeGraphEdgeResponse(userNodeId, FinancialLifeGraphPredicates.HasTransaction, transactionNodeId, null));
            }
        }

        foreach (var bill in snapshot.Bills)
        {
            var billNodeId = FinancialLifeGraphFormatting.BuildNodeId(FinancialLifeGraphNodeKeys.Bill, bill.Id);
            nodes.Add(new FinancialLifeGraphNodeResponse(
                billNodeId,
                FinancialLifeGraphNodeTypes.Bill,
                bill.Payee,
                nameof(Bill),
                bill.Id,
                FinancialLifeGraphFormatting.SerializeMetadata(new
                {
                    bill.PaidFromAccountId,
                    bill.ExpectedAmount,
                    bill.Currency,
                    bill.NextDueDate,
                    bill.Frequency,
                    bill.Autopay,
                    bill.Status,
                    bill.LinkedOrderId,
                    bill.LinkedInvoiceId
                })));
            edges.Add(new FinancialLifeGraphEdgeResponse(userNodeId, FinancialLifeGraphPredicates.HasBill, billNodeId, null));

            if (bill.PaidFromAccountId.HasValue && snapshot.Accounts.Any(item => item.Id == bill.PaidFromAccountId.Value))
            {
                edges.Add(new FinancialLifeGraphEdgeResponse(
                    billNodeId,
                    FinancialLifeGraphPredicates.FundedByAccount,
                    FinancialLifeGraphFormatting.BuildNodeId(FinancialLifeGraphNodeKeys.PersonalAccount, bill.PaidFromAccountId.Value),
                    null));
            }

            if (bill.LinkedOrderId.HasValue)
            {
                var order = snapshot.Orders.FirstOrDefault(item => item.Id == bill.LinkedOrderId.Value);
                if (order != null)
                {
                    var orderNodeId = FinancialLifeGraphFormatting.BuildNodeId(FinancialLifeGraphNodeKeys.OrderRef, order.Id);
                    nodes.Add(new FinancialLifeGraphNodeResponse(
                        orderNodeId,
                        FinancialLifeGraphNodeTypes.OrderRef,
                        $"Order {order.Id}",
                        nameof(Order),
                        order.Id,
                        FinancialLifeGraphFormatting.SerializeMetadata(new
                        {
                            order.OrderType,
                            order.Status,
                            order.CurrencyIn,
                            order.AmountIn,
                            order.CurrencyOut,
                            order.AmountOut
                        })));
                    edges.Add(new FinancialLifeGraphEdgeResponse(
                        billNodeId,
                        FinancialLifeGraphPredicates.LinkedToOrder,
                        orderNodeId,
                        null));
                }
            }

            if (bill.LinkedInvoiceId.HasValue)
            {
                var invoice = snapshot.Invoices.FirstOrDefault(item => item.Id == bill.LinkedInvoiceId.Value);
                if (invoice != null)
                {
                    var invoiceNodeId = FinancialLifeGraphFormatting.BuildNodeId(FinancialLifeGraphNodeKeys.InvoiceRef, invoice.Id);
                    nodes.Add(new FinancialLifeGraphNodeResponse(
                        invoiceNodeId,
                        FinancialLifeGraphNodeTypes.InvoiceRef,
                        $"Invoice {invoice.Id}",
                        nameof(Invoice),
                        invoice.Id,
                        FinancialLifeGraphFormatting.SerializeMetadata(new
                        {
                            invoice.Status,
                            invoice.Currency,
                            invoice.Total,
                            invoice.DueDate,
                            invoice.OrderId
                        })));
                    edges.Add(new FinancialLifeGraphEdgeResponse(
                        billNodeId,
                        FinancialLifeGraphPredicates.LinkedToInvoice,
                        invoiceNodeId,
                        null));
                }
            }

            foreach (var paymentIntent in snapshot.PaymentIntents.Where(item => item.OrderId == bill.LinkedOrderId || item.InvoiceId == bill.LinkedInvoiceId))
            {
                var paymentIntentNodeId = FinancialLifeGraphFormatting.BuildNodeId(FinancialLifeGraphNodeKeys.PaymentIntentRef, paymentIntent.Id);
                nodes.Add(new FinancialLifeGraphNodeResponse(
                    paymentIntentNodeId,
                    FinancialLifeGraphNodeTypes.PaymentIntentRef,
                    $"Payment Intent {paymentIntent.Id}",
                    nameof(PaymentIntent),
                    paymentIntent.Id,
                    FinancialLifeGraphFormatting.SerializeMetadata(new
                    {
                        paymentIntent.Status,
                        paymentIntent.Currency,
                        paymentIntent.Amount,
                        paymentIntent.OrderId,
                        paymentIntent.InvoiceId,
                        paymentIntent.PurposeType,
                        paymentIntent.PurposeId
                    })));
                edges.Add(new FinancialLifeGraphEdgeResponse(
                    billNodeId,
                    FinancialLifeGraphPredicates.LinkedToPaymentIntent,
                    paymentIntentNodeId,
                    null));
            }
        }

        foreach (var goal in snapshot.Goals)
        {
            var goalNodeId = FinancialLifeGraphFormatting.BuildNodeId(FinancialLifeGraphNodeKeys.Goal, goal.Id);
            nodes.Add(new FinancialLifeGraphNodeResponse(
                goalNodeId,
                FinancialLifeGraphNodeTypes.Goal,
                goal.Name,
                nameof(Goal),
                goal.Id,
                FinancialLifeGraphFormatting.SerializeMetadata(new
                {
                    goal.FundingAccountId,
                    goal.TargetAmount,
                    goal.ProgressAmount,
                    goal.Currency,
                    goal.TargetDate,
                    goal.Status
                })));
            edges.Add(new FinancialLifeGraphEdgeResponse(userNodeId, FinancialLifeGraphPredicates.HasGoal, goalNodeId, null));

            if (goal.FundingAccountId.HasValue && snapshot.Accounts.Any(item => item.Id == goal.FundingAccountId.Value))
            {
                edges.Add(new FinancialLifeGraphEdgeResponse(
                    goalNodeId,
                    FinancialLifeGraphPredicates.FundedByAccount,
                    FinancialLifeGraphFormatting.BuildNodeId(FinancialLifeGraphNodeKeys.PersonalAccount, goal.FundingAccountId.Value),
                    null));
            }
        }

        foreach (var subscription in snapshot.Subscriptions)
        {
            var subscriptionNodeId = FinancialLifeGraphFormatting.BuildNodeId(FinancialLifeGraphNodeKeys.Subscription, subscription.Id);
            nodes.Add(new FinancialLifeGraphNodeResponse(
                subscriptionNodeId,
                FinancialLifeGraphNodeTypes.Subscription,
                subscription.Merchant,
                nameof(Subscription),
                subscription.Id,
                FinancialLifeGraphFormatting.SerializeMetadata(new
                {
                    subscription.ExpectedAmount,
                    subscription.Currency,
                    subscription.RenewalDate,
                    subscription.Status,
                    subscription.DetectedBy
                })));
            edges.Add(new FinancialLifeGraphEdgeResponse(userNodeId, FinancialLifeGraphPredicates.HasSubscription, subscriptionNodeId, null));
        }

        foreach (var quote in snapshot.FxQuotes)
        {
            var fxNodeId = FinancialLifeGraphFormatting.BuildNodeId(FinancialLifeGraphNodeKeys.FxQuote, quote.Id);
            nodes.Add(new FinancialLifeGraphNodeResponse(
                fxNodeId,
                FinancialLifeGraphNodeTypes.FxQuote,
                $"{quote.BaseCurrency}/{quote.TargetCurrency}",
                nameof(Entities.Pricing.FxQuote),
                quote.Id,
                FinancialLifeGraphFormatting.SerializeMetadata(new
                {
                    quote.BaseCurrency,
                    quote.TargetCurrency,
                    quote.Rate,
                    QuotedAt = quote.UpdatedAt ?? quote.CreatedAt,
                    quote.ExpiresAt,
                    quote.Provider
                })));
            edges.Add(new FinancialLifeGraphEdgeResponse(userNodeId, FinancialLifeGraphPredicates.HasFxContext, fxNodeId, null));
        }

        foreach (var party in snapshot.RelatedParties)
        {
            var partyNodeId = FinancialLifeGraphFormatting.BuildNodeId(FinancialLifeGraphNodeKeys.Party, party.Id);
            var relationship = snapshot.PartyRelationships
                .FirstOrDefault(item => item.FromPartyId == party.Id || item.ToPartyId == party.Id);
            nodes.Add(new FinancialLifeGraphNodeResponse(
                partyNodeId,
                FinancialLifeGraphNodeTypes.Party,
                !string.IsNullOrWhiteSpace(relationship?.RelationshipTypeCode)
                    ? $"{party.DisplayName} ({relationship.RelationshipTypeCode})"
                    : party.DisplayName,
                FinancialLifeGraphNodeTypes.Party,
                party.Id,
                FinancialLifeGraphFormatting.SerializeMetadata(new
                {
                    party.Status,
                    party.CustomerTierCode,
                    RelationshipTypeCode = relationship?.RelationshipTypeCode,
                    RelationshipNotes = relationship?.Notes
                })));
            edges.Add(new FinancialLifeGraphEdgeResponse(
                userNodeId,
                FinancialLifeGraphPredicates.RelatedToParty,
                partyNodeId,
                relationship == null ? null : FinancialLifeGraphFormatting.SerializeMetadata(new
                {
                    relationship.RelationshipTypeCode,
                    relationship.Notes
                })));
        }

        foreach (var node in snapshot.NativeNodes)
        {
            nodes.Add(new FinancialLifeGraphNodeResponse(
                FinancialLifeGraphFormatting.BuildNodeId(FinancialLifeGraphNodeKeys.NativeNode, node.Id),
                node.NodeType,
                node.DisplayName,
                node.SourceEntity ?? nameof(FinancialLifeGraphNode),
                node.SourceId ?? node.Id,
                FinancialLifeGraphFormatting.NormalizeMetadataJson(node.PropertiesJson)));
        }

        foreach (var edge in snapshot.NativeEdges)
        {
            edges.Add(new FinancialLifeGraphEdgeResponse(
                edge.FromNodeKey,
                edge.Predicate,
                edge.ToNodeKey,
                FinancialLifeGraphFormatting.NormalizeMetadataJson(edge.PropertiesJson)));
        }

        var summary = BuildSummary(snapshot);
        var sourceCoverage = new List<FinancialLifeGraphSourceCoverageItemResponse>
        {
            new(FinancialLifeGraphNodeTypes.PersonalAccount, snapshot.Accounts.Count),
            new(FinancialLifeGraphNodeTypes.PersonalLinkedAccount, snapshot.LinkedAccounts.Count),
            new(FinancialLifeGraphNodeTypes.PersonalTransaction, snapshot.Transactions.Count),
            new(FinancialLifeGraphNodeTypes.Bill, snapshot.Bills.Count),
            new(FinancialLifeGraphNodeTypes.Goal, snapshot.Goals.Count),
            new(FinancialLifeGraphNodeTypes.Subscription, snapshot.Subscriptions.Count),
            new(FinancialLifeGraphNodeTypes.FxQuote, snapshot.FxQuotes.Count),
            new(FinancialLifeGraphNodeTypes.OrderRef, snapshot.Orders.Count),
            new(FinancialLifeGraphNodeTypes.InvoiceRef, snapshot.Invoices.Count),
            new(FinancialLifeGraphNodeTypes.PaymentIntentRef, snapshot.PaymentIntents.Count),
            new("PartyRelationship", snapshot.PartyRelationships.Count),
            new("FinancialLifeGraphNode", snapshot.NativeNodes.Count),
            new("FinancialLifeGraphEdge", snapshot.NativeEdges.Count)
        };

        return new FinancialLifeGraphResponse(
            snapshot.TenantId,
            snapshot.UserId,
            snapshot.PersonalProfile?.HouseholdId,
            DateTime.UtcNow,
            summary,
            nodes,
            edges,
            sourceCoverage);
    }

    private static FinancialLifeGraphSummaryResponse BuildSummary(FinancialLifeGraphSnapshot snapshot)
    {
        return new FinancialLifeGraphSummaryResponse(
            snapshot.Accounts.Count,
            snapshot.LinkedAccounts.Count,
            snapshot.Transactions.Count,
            snapshot.Bills.Count,
            snapshot.Goals.Count,
            snapshot.Subscriptions.Count,
            CountFundingRelationships(snapshot),
            snapshot.NativeNodes.Count(item => item.IsInferred),
            snapshot.Household != null,
            snapshot.HouseholdMembers.Count,
            snapshot.RelatedParties.Count,
            snapshot.PersonalProfile?.PartyId,
            snapshot.PersonalProfile?.HouseholdId);
    }

    private static IReadOnlyList<UpcomingObligationResponse> BuildUpcomingObligations(
        FinancialLifeGraphSnapshot snapshot,
        int withinDays)
    {
        var today = DateTime.UtcNow.Date;
        var latestDate = today.AddDays(withinDays);
        var items = new List<UpcomingObligationResponse>();

        items.AddRange(snapshot.Bills
            .Where(item => item.NextDueDate.Date <= latestDate)
            .Select(item => new UpcomingObligationResponse(
                FinancialLifeGraphNodeTypes.Bill,
                item.Id,
                item.Payee,
                item.ExpectedAmount,
                item.Currency,
                item.NextDueDate,
                (item.NextDueDate.Date - today).Days,
                item.Status)));

        items.AddRange(snapshot.Subscriptions
            .Where(item => item.RenewalDate.Date <= latestDate)
            .Select(item => new UpcomingObligationResponse(
                FinancialLifeGraphNodeTypes.Subscription,
                item.Id,
                item.Merchant,
                item.ExpectedAmount,
                item.Currency,
                item.RenewalDate,
                (item.RenewalDate.Date - today).Days,
                item.Status)));

        items.AddRange(snapshot.Goals
            .Where(item => item.TargetDate.HasValue
                && item.TargetDate.Value.Date <= latestDate
                && !string.Equals(item.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            .Select(item => new UpcomingObligationResponse(
                FinancialLifeGraphNodeTypes.Goal,
                item.Id,
                item.Name,
                item.TargetAmount - item.ProgressAmount,
                item.Currency,
                item.TargetDate!.Value,
                (item.TargetDate.Value.Date - today).Days,
                item.Status)));

        return items
            .OrderBy(item => item.DueDate)
            .ThenBy(item => item.ItemType)
            .ToList();
    }

    private static int CountFundingRelationships(FinancialLifeGraphSnapshot snapshot)
    {
        return snapshot.Bills.Count(item => item.PaidFromAccountId.HasValue)
               + snapshot.Goals.Count(item => item.FundingAccountId.HasValue);
    }
}
