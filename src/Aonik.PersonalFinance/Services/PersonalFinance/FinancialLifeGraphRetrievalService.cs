using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aonik.Finance.Services.PersonalFinance;

internal sealed class FinancialLifeGraphRetrievalService : IFinancialLifeGraphRetrievalService
{
    private const int MaxStatementWindowDays = 365;
    private const int MaxBillHistoryWindowDays = 730;

    private readonly PersonalFinanceDbContext _dbContext;
    private readonly IPartyReader _partyReader;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<FinancialLifeGraphRetrievalService> _logger;

    public FinancialLifeGraphRetrievalService(
        PersonalFinanceDbContext dbContext,
        IPartyReader partyReader,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        ILogger<FinancialLifeGraphRetrievalService> logger)
    {
        _dbContext = dbContext;
        _partyReader = partyReader;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    public async Task<GraphRetrievalResult<BillPaymentHistoryResponse>> GetBillPaymentHistoryAsync(
        string nodeKey,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        const string toolName = "GetBillPaymentHistory";

        if (!TryParseNodeKey(nodeKey, FinancialLifeGraphNodeKeys.Bill, out var entityId))
        {
            return Failure<BillPaymentHistoryResponse>(nodeKey, toolName,
                $"Invalid node key '{nodeKey}'. Expected format: bill:{{guid}}");
        }

        var (tenantId, userId) = GetScopingIds();

        var bill = await _dbContext.Bills
            .AsNoTracking()
            .FirstOrDefaultAsync(item =>
                item.TenantId == tenantId
                && item.UserId == userId
                && item.Id == entityId,
                cancellationToken);

        if (bill == null)
        {
            LogRetrievalCall(toolName, nodeKey, false);
            return Failure<BillPaymentHistoryResponse>(nodeKey, toolName,
                $"Bill not found for node key '{nodeKey}'.");
        }

        var effectiveTo = to ?? DateTime.UtcNow;
        var effectiveFrom = from ?? effectiveTo.AddDays(-MaxBillHistoryWindowDays);

        if ((effectiveTo - effectiveFrom).TotalDays > MaxBillHistoryWindowDays)
        {
            effectiveFrom = effectiveTo.AddDays(-MaxBillHistoryWindowDays);
        }

        // Find transactions matching this bill's payee and account
        var paymentsQuery = _dbContext.PersonalTransactions
            .AsNoTracking()
            .Where(item =>
                item.TenantId == tenantId
                && item.UserId == userId
                && item.OccurredAt >= effectiveFrom
                && item.OccurredAt <= effectiveTo
                && item.Amount < 0); // Bill payments are outflows

        if (bill.PaidFromAccountId.HasValue)
        {
            paymentsQuery = paymentsQuery.Where(item => item.PersonalAccountId == bill.PaidFromAccountId.Value);
        }

        if (!string.IsNullOrWhiteSpace(bill.Payee))
        {
            paymentsQuery = paymentsQuery.Where(item =>
                item.Merchant != null && item.Merchant.ToLower().Contains(bill.Payee.ToLower()));
        }

        var payments = await paymentsQuery
            .OrderByDescending(item => item.OccurredAt)
            .ToListAsync(cancellationToken);

        var paymentItems = payments.Select(item => new BillPaymentHistoryItemResponse(
            item.Id,
            Math.Abs(item.Amount),
            item.Currency,
            item.OccurredAt,
            item.ReviewStatus,
            item.SourceType)).ToList();

        var result = new BillPaymentHistoryResponse(
            bill.Id,
            bill.Payee,
            bill.ExpectedAmount,
            bill.Currency,
            bill.Frequency,
            paymentItems.Count,
            paymentItems.Sum(item => item.Amount),
            paymentItems);

        LogRetrievalCall(toolName, nodeKey, true);
        return Success(nodeKey, toolName, result);
    }

    public async Task<GraphRetrievalResult<GoalContributionHistoryResponse>> GetGoalContributionHistoryAsync(
        string nodeKey,
        CancellationToken cancellationToken = default)
    {
        const string toolName = "GetGoalContributionHistory";

        if (!TryParseNodeKey(nodeKey, FinancialLifeGraphNodeKeys.Goal, out var entityId))
        {
            return Failure<GoalContributionHistoryResponse>(nodeKey, toolName,
                $"Invalid node key '{nodeKey}'. Expected format: goal:{{guid}}");
        }

        var (tenantId, userId) = GetScopingIds();

        var goal = await _dbContext.Goals
            .AsNoTracking()
            .FirstOrDefaultAsync(item =>
                item.TenantId == tenantId
                && item.UserId == userId
                && item.Id == entityId,
                cancellationToken);

        if (goal == null)
        {
            LogRetrievalCall(toolName, nodeKey, false);
            return Failure<GoalContributionHistoryResponse>(nodeKey, toolName,
                $"Goal not found for node key '{nodeKey}'.");
        }

        // Find transactions that appear to be contributions to this goal's funding account
        var contributions = new List<GoalContributionItemResponse>();
        if (goal.FundingAccountId.HasValue)
        {
            var transfers = await _dbContext.PersonalTransactions
                .AsNoTracking()
                .Where(item =>
                    item.TenantId == tenantId
                    && item.UserId == userId
                    && item.PersonalAccountId == goal.FundingAccountId.Value
                    && item.Amount > 0
                    && item.Category != null
                    && item.Category.ToLower().Contains("transfer"))
                .OrderByDescending(item => item.OccurredAt)
                .ToListAsync(cancellationToken);

            contributions = transfers.Select(item => new GoalContributionItemResponse(
                item.Id,
                item.Amount,
                item.Currency,
                item.OccurredAt,
                item.Description)).ToList();
        }

        var result = new GoalContributionHistoryResponse(
            goal.Id,
            goal.Name,
            goal.TargetAmount,
            goal.ProgressAmount,
            goal.Currency,
            goal.TargetDate,
            goal.Status,
            contributions.Count,
            contributions.Sum(item => item.Amount),
            contributions);

        LogRetrievalCall(toolName, nodeKey, true);
        return Success(nodeKey, toolName, result);
    }

    public async Task<GraphRetrievalResult<AccountStatementResponse>> GetAccountStatementAsync(
        string nodeKey,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        const string toolName = "GetAccountStatement";

        if (!TryParseNodeKey(nodeKey, FinancialLifeGraphNodeKeys.PersonalAccount, out var entityId))
        {
            return Failure<AccountStatementResponse>(nodeKey, toolName,
                $"Invalid node key '{nodeKey}'. Expected format: personal-account:{{guid}}");
        }

        if ((to - from).TotalDays > MaxStatementWindowDays)
        {
            return Failure<AccountStatementResponse>(nodeKey, toolName,
                $"Statement window exceeds maximum of {MaxStatementWindowDays} days. Requested: {(to - from).TotalDays:F0} days.");
        }

        if (to < from)
        {
            return Failure<AccountStatementResponse>(nodeKey, toolName,
                "'to' date must be after 'from' date.");
        }

        var (tenantId, userId) = GetScopingIds();

        var account = await _dbContext.PersonalAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(item =>
                item.TenantId == tenantId
                && item.UserId == userId
                && item.Id == entityId,
                cancellationToken);

        if (account == null)
        {
            LogRetrievalCall(toolName, nodeKey, false);
            return Failure<AccountStatementResponse>(nodeKey, toolName,
                $"Account not found for node key '{nodeKey}'.");
        }

        var transactions = await _dbContext.PersonalTransactions
            .AsNoTracking()
            .Where(item =>
                item.TenantId == tenantId
                && item.UserId == userId
                && item.PersonalAccountId == entityId
                && item.OccurredAt >= from
                && item.OccurredAt <= to)
            .OrderBy(item => item.OccurredAt)
            .ToListAsync(cancellationToken);

        var totalInflow = transactions.Where(item => item.Amount > 0).Sum(item => item.Amount);
        var totalOutflow = transactions.Where(item => item.Amount < 0).Sum(item => Math.Abs(item.Amount));
        var netChange = totalInflow - totalOutflow;

        var runningBalance = 0m;
        var statementItems = transactions.Select(item =>
        {
            runningBalance += item.Amount;
            return new AccountStatementItemResponse(
                item.Id,
                item.Merchant,
                item.Description,
                item.Amount,
                item.Currency,
                item.OccurredAt,
                item.Category,
                runningBalance);
        }).ToList();

        var result = new AccountStatementResponse(
            account.Id,
            account.Name,
            account.AccountType,
            account.Currency,
            from,
            to,
            transactions.Count,
            totalInflow,
            totalOutflow,
            netChange,
            statementItems);

        LogRetrievalCall(toolName, nodeKey, true);
        return Success(nodeKey, toolName, result);
    }

    public async Task<GraphRetrievalResult<PartyObligationSummaryResponse>> GetPartyObligationSummaryAsync(
        string nodeKey,
        CancellationToken cancellationToken = default)
    {
        const string toolName = "GetPartyObligationSummary";

        if (!TryParseNodeKey(nodeKey, FinancialLifeGraphNodeKeys.Party, out var entityId))
        {
            return Failure<PartyObligationSummaryResponse>(nodeKey, toolName,
                $"Invalid node key '{nodeKey}'. Expected format: party:{{guid}}");
        }

        var (tenantId, userId) = GetScopingIds();

        var parties = await _partyReader.GetByIdsAsync(tenantId, [entityId], cancellationToken);
        var party = parties.FirstOrDefault();

        if (party == null)
        {
            LogRetrievalCall(toolName, nodeKey, false);
            return Failure<PartyObligationSummaryResponse>(nodeKey, toolName,
                $"Party not found for node key '{nodeKey}'.");
        }

        var relationships = await _partyReader.GetRelationshipsForPartyAsync(tenantId, entityId, cancellationToken);
        var relationship = relationships.FirstOrDefault();

        // Find bills that match the party's display name as payee
        var bills = await _dbContext.Bills
            .AsNoTracking()
            .Where(item =>
                item.TenantId == tenantId
                && item.UserId == userId
                && item.Payee.ToLower().Contains(party.DisplayName.ToLower())
                && item.Status != "Cancelled")
            .ToListAsync(cancellationToken);

        // Find subscriptions that match
        var subscriptions = await _dbContext.Subscriptions
            .AsNoTracking()
            .Where(item =>
                item.TenantId == tenantId
                && item.UserId == userId
                && item.Merchant.ToLower().Contains(party.DisplayName.ToLower())
                && item.Status != "Cancelled")
            .ToListAsync(cancellationToken);

        var obligations = new List<PartyObligationItemResponse>();
        var totalMonthlyEstimate = 0m;
        string? primaryCurrency = null;

        foreach (var bill in bills)
        {
            obligations.Add(new PartyObligationItemResponse(
                "Bill", bill.Id, bill.Payee, bill.ExpectedAmount, bill.Currency,
                bill.Frequency, bill.NextDueDate));

            if (bill.ExpectedAmount.HasValue)
            {
                totalMonthlyEstimate += EstimateMonthlyAmount(bill.ExpectedAmount.Value, bill.Frequency);
            }

            primaryCurrency ??= bill.Currency;
        }

        foreach (var subscription in subscriptions)
        {
            obligations.Add(new PartyObligationItemResponse(
                "Subscription", subscription.Id, subscription.Merchant,
                subscription.ExpectedAmount, subscription.Currency,
                null, subscription.RenewalDate));

            totalMonthlyEstimate += subscription.ExpectedAmount;
            primaryCurrency ??= subscription.Currency;
        }

        var result = new PartyObligationSummaryResponse(
            party.PartyId,
            party.DisplayName,
            relationship?.RelationshipTypeCode,
            obligations.Count,
            obligations,
            totalMonthlyEstimate,
            primaryCurrency);

        LogRetrievalCall(toolName, nodeKey, true);
        return Success(nodeKey, toolName, result);
    }

    private (Guid TenantId, Guid UserId) GetScopingIds()
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            throw new InvalidOperationException("Authenticated user is required.");
        }
        return (tenantId, userId);
    }

    private static bool TryParseNodeKey(string nodeKey, string expectedPrefix, out Guid entityId)
    {
        entityId = Guid.Empty;
        if (!FinancialLifeGraphNodeKeys.TryParse(nodeKey, out var prefix, out var id))
        {
            return false;
        }

        if (!prefix.Equals(expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        entityId = id;
        return true;
    }

    private static decimal EstimateMonthlyAmount(decimal amount, string frequency)
    {
        return frequency.ToUpperInvariant() switch
        {
            "DAILY" => amount * 30,
            "WEEKLY" => amount * 4.33m,
            "BIWEEKLY" or "FORTNIGHTLY" => amount * 2.17m,
            "MONTHLY" => amount,
            "QUARTERLY" => amount / 3,
            "SEMIANNUALLY" or "SEMI-ANNUALLY" => amount / 6,
            "ANNUALLY" or "YEARLY" => amount / 12,
            _ => amount
        };
    }

    private void LogRetrievalCall(string toolName, string nodeKey, bool success)
    {
        _logger.LogInformation(
            "Graph retrieval: Tool={ToolName} NodeKey={NodeKey} Success={Success}",
            toolName, nodeKey, success);
    }

    private static GraphRetrievalResult<T> Success<T>(string nodeKey, string toolName, T data)
    {
        return new GraphRetrievalResult<T>(true, nodeKey, toolName, data, null);
    }

    private static GraphRetrievalResult<T> Failure<T>(string nodeKey, string toolName, string errorReason)
    {
        return new GraphRetrievalResult<T>(false, nodeKey, toolName, default, errorReason);
    }
}
