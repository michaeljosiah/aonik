using System.Text.Json;
using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Finance.Entities;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.PersonalFinance;

internal sealed class FinancialLifeGraphService : IFinancialLifeGraphService
{
    private static readonly TimeSpan GraphCacheDuration = TimeSpan.FromMinutes(5);

    private readonly FinanceDbContext _financeDbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IMemoryCache _memoryCache;

    public FinancialLifeGraphService(
        FinanceDbContext financeDbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        IMemoryCache memoryCache)
    {
        _financeDbContext = financeDbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _memoryCache = memoryCache;
    }

    public async Task<FinancialLifeGraphResponse> GetGraphAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken);
        return BuildGraph(snapshot);
    }

    public async Task<FinancialLifeGraphSummaryResponse> GetGraphSummaryAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken);
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

        var snapshot = await GetSnapshotAsync(cancellationToken);
        return BuildUpcomingObligations(snapshot, withinDays);
    }

    private async Task<FinancialLifeGraphSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();
        var cacheKey = FinancialLifeGraphCacheInvalidator.GetCacheKey(tenantId, userId);

        if (_memoryCache.TryGetValue(cacheKey, out FinancialLifeGraphSnapshot? cachedSnapshot)
            && cachedSnapshot is not null)
        {
            return cachedSnapshot;
        }

        var snapshot = await LoadSnapshotAsync(cancellationToken);
        _memoryCache.Set(cacheKey, snapshot, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = GraphCacheDuration,
            SlidingExpiration = TimeSpan.FromMinutes(2)
        });

        return snapshot;
    }

    private async Task<FinancialLifeGraphSnapshot> LoadSnapshotAsync(CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();

        var personalProfile = await _financeDbContext.PersonalProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                profile => profile.TenantId == tenantId && profile.UserId == userId,
                cancellationToken);

        Household? household = null;
        List<HouseholdMember> householdMembers = new();

        if (personalProfile?.HouseholdId is Guid householdId)
        {
            household = await _financeDbContext.Households
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item => item.TenantId == tenantId && item.Id == householdId,
                    cancellationToken);

            householdMembers = await _financeDbContext.HouseholdMembers
                .AsNoTracking()
                .Where(item => item.HouseholdId == householdId)
                .OrderBy(item => item.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        var accounts = await _financeDbContext.PersonalAccounts
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.UserId == userId)
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

        var accountIds = accounts.Select(item => item.Id).ToList();

        var linkedAccounts = accountIds.Count == 0
            ? new List<FinancialLinkedAccount>()
            : await _financeDbContext.FinancialLinkedAccounts
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId && item.UserId == userId && accountIds.Contains(item.PersonalAccountId))
                .OrderBy(item => item.Name)
                .ToListAsync(cancellationToken);

        var transactions = await _financeDbContext.PersonalTransactions
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.UserId == userId)
            .OrderByDescending(item => item.OccurredAt)
            .ToListAsync(cancellationToken);

        var bills = await _financeDbContext.Bills
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.UserId == userId)
            .OrderBy(item => item.NextDueDate)
            .ToListAsync(cancellationToken);

        var goals = await _financeDbContext.Goals
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.UserId == userId)
            .OrderBy(item => item.TargetDate ?? DateTime.MaxValue)
            .ThenBy(item => item.Name)
            .ToListAsync(cancellationToken);

        var subscriptions = await _financeDbContext.Subscriptions
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.UserId == userId)
            .OrderBy(item => item.RenewalDate)
            .ToListAsync(cancellationToken);

        var fxQuotes = await _financeDbContext.FxQuotes
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId)
            .OrderByDescending(item => item.ExpiresAt)
            .ThenByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .Take(10)
            .ToListAsync(cancellationToken);

        var selfPartyId = personalProfile?.PartyId;
        var relatedParties = new List<PartyReadModel>();
        var partyRelationships = new List<PartyRelationshipReadModel>();

        if (selfPartyId.HasValue)
        {
            partyRelationships = await _financeDbContext.Set<PartyRelationshipReadModel>()
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId && item.IsActive
                    && (item.FromPartyId == selfPartyId.Value || item.ToPartyId == selfPartyId.Value))
                .OrderBy(item => item.RelationshipTypeCode)
                .ToListAsync(cancellationToken);

            var relatedPartyIds = partyRelationships
                .Select(item => item.FromPartyId == selfPartyId.Value ? item.ToPartyId : item.FromPartyId)
                .Distinct()
                .ToList();

            if (relatedPartyIds.Count > 0)
            {
                relatedParties = await _financeDbContext.Parties
                    .AsNoTracking()
                    .Where(item => item.TenantId == tenantId && relatedPartyIds.Contains(item.Id))
                    .OrderBy(item => item.DisplayName)
                    .ToListAsync(cancellationToken);
            }
        }

        var nativeNodes = await _financeDbContext.FinancialLifeGraphNodes
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.UserId == userId)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        var nativeEdges = await _financeDbContext.FinancialLifeGraphEdges
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.UserId == userId)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        return new FinancialLifeGraphSnapshot(
            tenantId,
            userId,
            personalProfile,
            household,
            householdMembers,
            accounts,
            linkedAccounts,
            transactions,
            bills,
            goals,
            subscriptions,
            fxQuotes,
            selfPartyId,
            relatedParties,
            partyRelationships,
            nativeNodes,
            nativeEdges);
    }

    private static FinancialLifeGraphResponse BuildGraph(FinancialLifeGraphSnapshot snapshot)
    {
        var nodes = new List<FinancialLifeGraphNodeResponse>();
        var edges = new List<FinancialLifeGraphEdgeResponse>();

        var userNodeId = BuildNodeId("user", snapshot.UserId);
        nodes.Add(new FinancialLifeGraphNodeResponse(
            userNodeId,
            "UserRoot",
            "Current User",
            "User",
            snapshot.UserId,
            SerializeMetadata(new
            {
                snapshot.TenantId,
                snapshot.UserId,
                snapshot.PersonalProfile?.PartyId,
                snapshot.PersonalProfile?.HouseholdId
            })));

        if (snapshot.Household != null)
        {
            var householdNodeId = BuildNodeId("household", snapshot.Household.Id);
            nodes.Add(new FinancialLifeGraphNodeResponse(
                householdNodeId,
                "Household",
                snapshot.Household.Name,
                nameof(Household),
                snapshot.Household.Id,
                SerializeMetadata(new
                {
                    MemberCount = snapshot.HouseholdMembers.Count,
                    snapshot.Household.CreatedAt
                })));
            edges.Add(new FinancialLifeGraphEdgeResponse(userNodeId, "BELONGS_TO_HOUSEHOLD", householdNodeId, null));

            foreach (var member in snapshot.HouseholdMembers)
            {
                var memberNodeId = BuildNodeId("household-member", member.Id);
                nodes.Add(new FinancialLifeGraphNodeResponse(
                    memberNodeId,
                    "HouseholdMember",
                    member.UserId == snapshot.UserId ? "You" : $"Member {member.UserId}",
                    nameof(HouseholdMember),
                    member.Id,
                    SerializeMetadata(new
                    {
                        member.UserId,
                        member.Role,
                        member.PermissionsJson
                    })));
                edges.Add(new FinancialLifeGraphEdgeResponse(householdNodeId, "HOUSEHOLD_HAS_MEMBER", memberNodeId, null));
            }
        }

        foreach (var account in snapshot.Accounts)
        {
            var accountNodeId = BuildNodeId("personal-account", account.Id);
            nodes.Add(new FinancialLifeGraphNodeResponse(
                accountNodeId,
                "PersonalAccount",
                account.Name,
                nameof(PersonalAccount),
                account.Id,
                SerializeMetadata(new
                {
                    account.AccountType,
                    account.Currency,
                    account.InstitutionName,
                    account.Status,
                    account.AccountSubtype,
                    account.Last4,
                    account.IsArchived
                })));
            edges.Add(new FinancialLifeGraphEdgeResponse(userNodeId, "OWNS_ACCOUNT", accountNodeId, null));
        }

        foreach (var linkedAccount in snapshot.LinkedAccounts)
        {
            var linkedAccountNodeId = BuildNodeId("linked-account", linkedAccount.Id);
            var parentAccountNodeId = BuildNodeId("personal-account", linkedAccount.PersonalAccountId);
            nodes.Add(new FinancialLifeGraphNodeResponse(
                linkedAccountNodeId,
                "FinancialLinkedAccount",
                linkedAccount.Name,
                nameof(FinancialLinkedAccount),
                linkedAccount.Id,
                SerializeMetadata(new
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
            edges.Add(new FinancialLifeGraphEdgeResponse(parentAccountNodeId, "USES_LINKED_ACCOUNT", linkedAccountNodeId, null));
        }

        foreach (var transaction in snapshot.Transactions)
        {
            var transactionNodeId = BuildNodeId("personal-transaction", transaction.Id);
            nodes.Add(new FinancialLifeGraphNodeResponse(
                transactionNodeId,
                "PersonalTransaction",
                transaction.Merchant ?? transaction.Description ?? $"Transaction {transaction.Id}",
                nameof(PersonalTransaction),
                transaction.Id,
                SerializeMetadata(new
                {
                    transaction.Amount,
                    transaction.Currency,
                    transaction.OccurredAt,
                    transaction.Category,
                    transaction.SourceType,
                    transaction.ClassificationMethod,
                    transaction.ReviewStatus
                })));

            if (transaction.PersonalAccountId.HasValue)
            {
                edges.Add(new FinancialLifeGraphEdgeResponse(
                    BuildNodeId("personal-account", transaction.PersonalAccountId.Value),
                    "HAS_TRANSACTION",
                    transactionNodeId,
                    null));
            }
            else
            {
                edges.Add(new FinancialLifeGraphEdgeResponse(userNodeId, "HAS_TRANSACTION", transactionNodeId, null));
            }
        }

        foreach (var bill in snapshot.Bills)
        {
            var billNodeId = BuildNodeId("bill", bill.Id);
            nodes.Add(new FinancialLifeGraphNodeResponse(
                billNodeId,
                "Bill",
                bill.Payee,
                nameof(Bill),
                bill.Id,
                SerializeMetadata(new
                {
                    bill.ExpectedAmount,
                    bill.Currency,
                    bill.NextDueDate,
                    bill.Frequency,
                    bill.Autopay,
                    bill.Status,
                    bill.LinkedOrderId,
                    bill.LinkedInvoiceId
                })));
            edges.Add(new FinancialLifeGraphEdgeResponse(userNodeId, "HAS_BILL", billNodeId, null));
        }

        foreach (var goal in snapshot.Goals)
        {
            var goalNodeId = BuildNodeId("goal", goal.Id);
            nodes.Add(new FinancialLifeGraphNodeResponse(
                goalNodeId,
                "Goal",
                goal.Name,
                nameof(Goal),
                goal.Id,
                SerializeMetadata(new
                {
                    goal.TargetAmount,
                    goal.ProgressAmount,
                    goal.Currency,
                    goal.TargetDate,
                    goal.Status
                })));
            edges.Add(new FinancialLifeGraphEdgeResponse(userNodeId, "HAS_GOAL", goalNodeId, null));
        }

        foreach (var subscription in snapshot.Subscriptions)
        {
            var subscriptionNodeId = BuildNodeId("subscription", subscription.Id);
            nodes.Add(new FinancialLifeGraphNodeResponse(
                subscriptionNodeId,
                "Subscription",
                subscription.Merchant,
                nameof(Subscription),
                subscription.Id,
                SerializeMetadata(new
                {
                    subscription.ExpectedAmount,
                    subscription.Currency,
                    subscription.RenewalDate,
                    subscription.Status,
                    subscription.DetectedBy
                })));
            edges.Add(new FinancialLifeGraphEdgeResponse(userNodeId, "HAS_SUBSCRIPTION", subscriptionNodeId, null));
        }

        foreach (var quote in snapshot.FxQuotes)
        {
            var fxNodeId = BuildNodeId("fx-quote", quote.Id);
            nodes.Add(new FinancialLifeGraphNodeResponse(
                fxNodeId,
                "FxQuote",
                $"{quote.BaseCurrency}/{quote.TargetCurrency}",
                nameof(Entities.Pricing.FxQuote),
                quote.Id,
                SerializeMetadata(new
                {
                    quote.BaseCurrency,
                    quote.TargetCurrency,
                    quote.Rate,
                    quote.ExpiresAt,
                    quote.Provider
                })));
            edges.Add(new FinancialLifeGraphEdgeResponse(userNodeId, "HAS_FX_CONTEXT", fxNodeId, null));
        }

        foreach (var party in snapshot.RelatedParties)
        {
            var partyNodeId = BuildNodeId("party", party.Id);
            var relationship = snapshot.PartyRelationships
                .FirstOrDefault(item => item.FromPartyId == party.Id || item.ToPartyId == party.Id);
            nodes.Add(new FinancialLifeGraphNodeResponse(
                partyNodeId,
                "Party",
                party.DisplayName,
                "Party",
                party.Id,
                SerializeMetadata(new
                {
                    party.Status,
                    party.CustomerTierCode,
                    RelationshipTypeCode = relationship?.RelationshipTypeCode,
                    relationship?.Notes
                })));
            edges.Add(new FinancialLifeGraphEdgeResponse(
                userNodeId,
                "RELATED_TO_PARTY",
                partyNodeId,
                relationship == null ? null : SerializeMetadata(new
                {
                    relationship.RelationshipTypeCode,
                    relationship.Notes
                })));
        }

        foreach (var node in snapshot.NativeNodes)
        {
            nodes.Add(new FinancialLifeGraphNodeResponse(
                BuildNodeId("native-node", node.Id),
                node.NodeType,
                node.DisplayName,
                node.SourceEntity ?? nameof(FinancialLifeGraphNode),
                node.SourceId ?? node.Id,
                string.IsNullOrWhiteSpace(node.PropertiesJson) ? null : node.PropertiesJson));
        }

        foreach (var edge in snapshot.NativeEdges)
        {
            edges.Add(new FinancialLifeGraphEdgeResponse(
                edge.FromNodeKey,
                edge.Predicate,
                edge.ToNodeKey,
                string.IsNullOrWhiteSpace(edge.PropertiesJson) ? null : edge.PropertiesJson));
        }

        var summary = BuildSummary(snapshot);
        var sourceCoverage = new List<FinancialLifeGraphSourceCoverageItemResponse>
        {
            new("PersonalAccount", snapshot.Accounts.Count),
            new("FinancialLinkedAccount", snapshot.LinkedAccounts.Count),
            new("PersonalTransaction", snapshot.Transactions.Count),
            new("Bill", snapshot.Bills.Count),
            new("Goal", snapshot.Goals.Count),
            new("Subscription", snapshot.Subscriptions.Count),
            new("FxQuote", snapshot.FxQuotes.Count),
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
                "Bill",
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
                "Subscription",
                item.Id,
                item.Merchant,
                item.ExpectedAmount,
                item.Currency,
                item.RenewalDate,
                (item.RenewalDate.Date - today).Days,
                item.Status)));

        items.AddRange(snapshot.Goals
            .Where(item => item.TargetDate.HasValue && item.TargetDate.Value.Date <= latestDate && item.Status != "Completed")
            .Select(item => new UpcomingObligationResponse(
                "Goal",
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

    private Guid GetCurrentUserId()
    {
        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            throw new InvalidOperationException("Authenticated user is required.");
        }

        return userId;
    }

    private static string BuildNodeId(string prefix, Guid id) => $"{prefix}:{id:D}";

    private static string SerializeMetadata(object value) => JsonSerializer.Serialize(value);

    private sealed record FinancialLifeGraphSnapshot(
        Guid TenantId,
        Guid UserId,
        PersonalProfile? PersonalProfile,
        Household? Household,
        IReadOnlyList<HouseholdMember> HouseholdMembers,
        IReadOnlyList<PersonalAccount> Accounts,
        IReadOnlyList<FinancialLinkedAccount> LinkedAccounts,
        IReadOnlyList<PersonalTransaction> Transactions,
        IReadOnlyList<Bill> Bills,
        IReadOnlyList<Goal> Goals,
        IReadOnlyList<Subscription> Subscriptions,
        IReadOnlyList<Entities.Pricing.FxQuote> FxQuotes,
        Guid? SelfPartyId,
        IReadOnlyList<PartyReadModel> RelatedParties,
        IReadOnlyList<PartyRelationshipReadModel> PartyRelationships,
        IReadOnlyList<FinancialLifeGraphNode> NativeNodes,
        IReadOnlyList<FinancialLifeGraphEdge> NativeEdges);
}
