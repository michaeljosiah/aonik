using Aonik.Finance.Entities;
using Aonik.Finance.Entities.Billing;
using Aonik.Finance.Entities.Orders;
using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.PersonalFinance;

internal sealed class FinancialLifeGraphLoader
{
    private readonly FinanceDbContext _financeDbContext;

    public FinancialLifeGraphLoader(FinanceDbContext financeDbContext)
    {
        _financeDbContext = financeDbContext;
    }

    public async Task<FinancialLifeGraphSnapshot> LoadCoreSnapshotAsync(
        Guid tenantId,
        Guid userId,
        int transactionWindowDays,
        CancellationToken cancellationToken = default)
    {
        var personalProfile = await _financeDbContext.PersonalProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                profile => profile.TenantId == tenantId && profile.UserId == userId,
                cancellationToken);

        Household? household = null;
        List<HouseholdMember> householdMembers = new();
        Dictionary<Guid, string> householdMemberDisplayNames = new();

        if (personalProfile?.HouseholdId is Guid householdId)
        {
            household = await _financeDbContext.Households
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == householdId, cancellationToken);

            householdMembers = await _financeDbContext.HouseholdMembers
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId && item.HouseholdId == householdId)
                .OrderBy(item => item.CreatedAt)
                .ToListAsync(cancellationToken);

            householdMemberDisplayNames = await BuildHouseholdMemberDisplayNamesAsync(tenantId, userId, householdMembers, cancellationToken);
        }

        var accounts = await _financeDbContext.PersonalAccounts
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.UserId == userId)
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

        var accountIds = accounts.Select(item => item.Id).ToList();

        var linkedAccounts = accountIds.Count == 0
            ? []
            : await _financeDbContext.PersonalLinkedAccounts
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId && item.UserId == userId && accountIds.Contains(item.PersonalAccountId))
                .OrderBy(item => item.Name)
                .ToListAsync(cancellationToken);

        var transactionCutoff = DateTime.UtcNow.Date.AddDays(-transactionWindowDays);
        var transactions = await _financeDbContext.PersonalTransactions
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.UserId == userId && item.OccurredAt >= transactionCutoff)
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

        var linkedOrderIds = bills
            .Where(item => item.LinkedOrderId.HasValue)
            .Select(item => item.LinkedOrderId!.Value)
            .Distinct()
            .ToList();

        var linkedInvoiceIds = bills
            .Where(item => item.LinkedInvoiceId.HasValue)
            .Select(item => item.LinkedInvoiceId!.Value)
            .Distinct()
            .ToList();

        var orders = linkedOrderIds.Count == 0
            ? []
            : await _financeDbContext.Orders
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId && linkedOrderIds.Contains(item.Id))
                .ToListAsync(cancellationToken);

        var invoices = linkedInvoiceIds.Count == 0
            ? []
            : await _financeDbContext.Invoices
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId && linkedInvoiceIds.Contains(item.Id))
                .ToListAsync(cancellationToken);

        var paymentIntents = await _financeDbContext.PaymentIntents
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && (linkedOrderIds.Contains(item.OrderId)
                    || (item.InvoiceId.HasValue && linkedInvoiceIds.Contains(item.InvoiceId.Value))))
            .ToListAsync(cancellationToken);

        var selfPartyId = personalProfile?.PartyId;
        var relatedParties = new List<PartyReadModel>();
        var partyRelationships = new List<PartyRelationshipReadModel>();

        if (selfPartyId.HasValue)
        {
            partyRelationships = await _financeDbContext.PartyRelationships
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId
                    && item.IsActive
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
            .Where(item => item.TenantId == tenantId && item.UserId == userId && item.Status == Contracts.Models.PersonalFinance.FinancialLifeGraphEntityStatus.Active)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        var nativeEdges = await _financeDbContext.FinancialLifeGraphEdges
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.UserId == userId && item.Status == Contracts.Models.PersonalFinance.FinancialLifeGraphEntityStatus.Active)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        return new FinancialLifeGraphSnapshot(
            tenantId,
            userId,
            personalProfile,
            household,
            householdMembers,
            householdMemberDisplayNames,
            accounts,
            linkedAccounts,
            transactions,
            bills,
            goals,
            subscriptions,
            [],
            orders,
            invoices,
            paymentIntents,
            selfPartyId,
            relatedParties,
            partyRelationships,
            nativeNodes,
            nativeEdges);
    }

    public async Task<IReadOnlyList<Entities.Pricing.FxQuote>> LoadFxQuotesAsync(
        Guid tenantId,
        IReadOnlyList<string> relevantAccountCurrencies,
        CancellationToken cancellationToken = default)
    {
        if (relevantAccountCurrencies.Count < 2)
        {
            return [];
        }

        return await _financeDbContext.FxQuotes
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && !string.Equals(item.BaseCurrency, item.TargetCurrency, StringComparison.OrdinalIgnoreCase)
                && relevantAccountCurrencies.Contains(item.BaseCurrency)
                && relevantAccountCurrencies.Contains(item.TargetCurrency))
            .OrderByDescending(item => item.ExpiresAt)
            .ThenByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .Take(10)
            .ToListAsync(cancellationToken);
    }

    public static List<string> GetRelevantAccountCurrencies(
        IReadOnlyList<PersonalAccount> accounts,
        IReadOnlyList<PersonalLinkedAccount> linkedAccounts)
    {
        return accounts
            .Select(item => item.Currency)
            .Concat(linkedAccounts.Select(item => item.Currency))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<Dictionary<Guid, string>> BuildHouseholdMemberDisplayNamesAsync(
        Guid tenantId,
        Guid currentUserId,
        IReadOnlyList<HouseholdMember> householdMembers,
        CancellationToken cancellationToken)
    {
        var memberUserIds = householdMembers
            .Select(item => item.UserId)
            .Distinct()
            .ToList();

        var memberProfiles = await _financeDbContext.PersonalProfiles
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && memberUserIds.Contains(item.UserId))
            .ToListAsync(cancellationToken);

        var partyIds = memberProfiles
            .Select(item => item.PartyId)
            .Distinct()
            .ToList();

        var partyLookup = partyIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _financeDbContext.Parties
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId && partyIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.DisplayName, cancellationToken);

        var userLookup = await _financeDbContext.Users
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && memberUserIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Email, cancellationToken);

        var profileLookup = memberProfiles.ToDictionary(item => item.UserId, item => item.PartyId);
        var result = new Dictionary<Guid, string>();

        foreach (var member in householdMembers)
        {
            if (member.UserId == currentUserId)
            {
                result[member.Id] = "You";
                continue;
            }

            if (profileLookup.TryGetValue(member.UserId, out var partyId)
                && partyLookup.TryGetValue(partyId, out var displayName)
                && !string.IsNullOrWhiteSpace(displayName))
            {
                result[member.Id] = displayName.Trim();
                continue;
            }

            if (userLookup.TryGetValue(member.UserId, out var email)
                && !string.IsNullOrWhiteSpace(email))
            {
                result[member.Id] = email.Trim();
                continue;
            }

            result[member.Id] = $"Member {member.UserId}";
        }

        return result;
    }
}
