using Aonik.Finance.Entities.PersonalFinance;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions.Finance;
using Aonik.SharedKernel.Abstractions.Platform;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.PersonalFinance;

internal sealed class FinancialLifeGraphLoader
{
    private readonly PersonalFinanceDbContext _financeDbContext;
    private readonly ICustomerOrderHistoryReader _orderReader;
    private readonly ICustomerInvoiceHistoryReader _invoiceReader;
    private readonly ICustomerPaymentHistoryReader _paymentReader;
    private readonly IFxQuoteReader _fxQuoteReader;
    private readonly IPartyReader _partyReader;
    private readonly IUserDirectoryReader _userDirectoryReader;

    public FinancialLifeGraphLoader(
        PersonalFinanceDbContext financeDbContext,
        ICustomerOrderHistoryReader orderReader,
        ICustomerInvoiceHistoryReader invoiceReader,
        ICustomerPaymentHistoryReader paymentReader,
        IFxQuoteReader fxQuoteReader,
        IPartyReader partyReader,
        IUserDirectoryReader userDirectoryReader)
    {
        _financeDbContext = financeDbContext;
        _orderReader = orderReader;
        _invoiceReader = invoiceReader;
        _paymentReader = paymentReader;
        _fxQuoteReader = fxQuoteReader;
        _partyReader = partyReader;
        _userDirectoryReader = userDirectoryReader;
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
        List<PersonalAccount> householdAccounts = new();

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

            foreach (var member in householdMembers)
            {
                HouseholdMembershipRules.NormalizeLegacyMember(member);
            }

            householdMembers = householdMembers
                .Where(HouseholdMembershipRules.IsAccepted)
                .ToList();

            householdMemberDisplayNames = await BuildHouseholdMemberDisplayNamesAsync(tenantId, userId, householdMembers, cancellationToken);

            householdAccounts = await _financeDbContext.PersonalAccounts
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId && item.HouseholdId == householdId)
                .OrderBy(item => item.Name)
                .ToListAsync(cancellationToken);
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

        // Spec 027 Phase 3: orders / invoices / payments served via SharedKernel
        // readers so the snapshot record stays free of Aonik.Finance.Entities.
        var orders = await _orderReader.GetByIdsAsync(tenantId, linkedOrderIds, cancellationToken);
        var invoices = await _invoiceReader.GetByIdsAsync(tenantId, linkedInvoiceIds, cancellationToken);
        var paymentIntents = await _paymentReader.GetForOrderOrInvoiceAsync(
            tenantId, linkedOrderIds, linkedInvoiceIds, cancellationToken);

        var selfPartyId = personalProfile?.PartyId;
        IReadOnlyList<PartyHistoryItem> relatedParties = [];
        IReadOnlyList<PartyRelationshipHistoryItem> partyRelationships = [];

        if (selfPartyId.HasValue)
        {
            partyRelationships = await _partyReader.GetRelationshipsForPartyAsync(
                tenantId, selfPartyId.Value, cancellationToken);

            var relatedPartyIds = partyRelationships
                .Select(item => item.FromPartyId == selfPartyId.Value ? item.ToPartyId : item.FromPartyId)
                .Distinct()
                .ToList();

            if (relatedPartyIds.Count > 0)
            {
                var loaded = await _partyReader.GetByIdsAsync(tenantId, relatedPartyIds, cancellationToken);
                relatedParties = [.. loaded.OrderBy(item => item.DisplayName)];
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
            householdAccounts,
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

    public Task<IReadOnlyList<FxQuoteHistoryItem>> LoadFxQuotesAsync(
        Guid tenantId,
        IReadOnlyList<string> relevantAccountCurrencies,
        CancellationToken cancellationToken = default)
        => _fxQuoteReader.GetRecentForCurrenciesAsync(tenantId, relevantAccountCurrencies, 10, cancellationToken);

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
            : (await _partyReader.GetByIdsAsync(tenantId, partyIds, cancellationToken))
                .ToDictionary(item => item.PartyId, item => item.DisplayName);

        var userLookup = (await _userDirectoryReader.GetByIdsAsync(tenantId, memberUserIds, cancellationToken))
            .ToDictionary(item => item.UserId, item => item.Email);

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
