using System.Diagnostics;
using System.Text.Json;
using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.PersonalFinance;
using Aonik.SharedKernel.Abstractions.UserBrief;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aonik.Agents.Services;

internal sealed class UserBriefProjector : IUserBriefProjector
{
    private readonly IUserBriefDataProvider _financeData;
    private readonly IUserBriefAiDataProvider _aiData;
    private readonly IUserBriefContextDataProvider _userContextData;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<UserBriefProjector> _logger;

    public UserBriefProjector(
        IUserBriefDataProvider financeData,
        IUserBriefAiDataProvider aiData,
        IUserBriefContextDataProvider userContextData,
        IServiceScopeFactory scopeFactory,
        ILogger<UserBriefProjector> logger)
    {
        _financeData = financeData;
        _aiData = aiData;
        _userContextData = userContextData;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<UserBrief> ProjectAsync(
        Guid tenantId,
        Guid userId,
        UserBriefOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = AiTelemetry.ActivitySource.StartActivity("aonik.user_brief.project", ActivityKind.Internal);
        activity?.SetTag("aonik.tenant_id", tenantId.ToString());
        activity?.SetTag("aonik.user_id", userId.ToString());

        options ??= new UserBriefOptions();

        // Concurrent data retrieval. AI data provider methods share a DbContext
        // so they must run sequentially; other providers run in parallel.
        var financeRequest = new UserBriefFinancialDataRequest(
            options.BillLookaheadDays,
            options.SpendPeriodStart,
            options.SpendPeriodEnd);

        var financeTask = TraceAsync(
            "aonik.user_brief.load_finance",
            () => _financeData.GetFinancialDataAsync(tenantId, userId, financeRequest, cancellationToken));
        var memoryTask = TraceAsync(
            "aonik.user_brief.load_memory",
            () => _aiData.GetCurrentMemoryEntriesAsync(tenantId, userId, cancellationToken));
        var userContextTask = TraceAsync(
            "aonik.user_brief.load_user_context",
            () => _userContextData.GetUserContextDataAsync(tenantId, userId, cancellationToken));
        // Conversation-history check runs alongside finance / memory / user
        // context tasks AND is itself launched in parallel with the AGUI
        // endpoint's history reconstruction. Using a fresh scope here
        // (rather than the request-scoped AgentsDbContext) means the read
        // can't trip EF Core's "second operation on this context" guard
        // when those concurrent paths fire simultaneously. The new scope
        // gets its tenant/user context seeded so EF query filters resolve
        // identically to the parent scope.
        var hasConversationHistoryTask = TraceAsync(
            "aonik.user_brief.load_conversation_history",
            async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                SeedScopeContext(scope.ServiceProvider, tenantId, userId);
                var db = scope.ServiceProvider.GetRequiredService<AgentsDbContext>();
                return await db.ConversationSummaries
                    .Where(s => s.TenantId == tenantId && s.UserId == userId)
                    .AnyAsync(cancellationToken);
            });

        await Task.WhenAll(financeTask, memoryTask, userContextTask, hasConversationHistoryTask);

        var financeData = await financeTask;
        var memoryEntries = await memoryTask;
        var userContextData = await userContextTask;
        var hasConversationHistory = await hasConversationHistoryTask;

        activity?.SetTag("aonik.user_brief.account_count", financeData.AccountCount);
        activity?.SetTag("aonik.user_brief.transaction_count", financeData.TransactionCount);
        activity?.SetTag("aonik.user_brief.memory_count", memoryEntries.Count);
        activity?.SetTag("aonik.user_brief.has_conversation_history", hasConversationHistory);

        var snapshot = financeData.CustomerInsightSnapshot;
        var currency = financeData.PrimaryCurrency;

        var user = new UserBriefUser(
            Name: ResolveName(userContextData, memoryEntries),
            Country: financeData.CorridorCountries.FirstOrDefault());

        var goals = userContextData.SetupProfile is { } setup
            ? setup.SelectedUseCases
                .Concat(setup.FinancialGoals)
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .Distinct(StringComparer.Ordinal)
                .ToList()
            : (IReadOnlyList<string>)[];

        UserBriefCash? cash = financeData.AccountCount == 0 && financeData.TransactionCount == 0
            ? null
            : new UserBriefCash(financeData.TotalBalance, currency);

        UserBriefPeriod? period = null;
        if (snapshot is not null)
        {
            var inflow = snapshot.TotalInflowsByCurrency.FirstOrDefault(x => x.Currency == currency)?.Amount ?? 0m;
            var outflow = snapshot.TotalOutflowsByCurrency.FirstOrDefault(x => x.Currency == currency)?.Amount ?? 0m;
            if (inflow != 0m || outflow != 0m)
            {
                period = new UserBriefPeriod(inflow, outflow, currency);
            }
        }

        var topCategories = snapshot is not null
            ? snapshot.TopCategorySpend
                .Take(4)
                .Select(x => new UserBriefAmount(x.Name, x.Amount))
                .ToList()
            : financeData.SpendSummaries
                .FirstOrDefault(s => s.Currency == currency)
                ?.TopCategories
                .Take(4)
                .Select(c => new UserBriefAmount(c.Category, c.Amount))
                .ToList()
              ?? [];

        var topMerchants = snapshot?.TopMerchantSpend
            .Take(5)
            .Select(x => new UserBriefAmount(x.Name, x.Amount))
            .ToList()
            ?? (IReadOnlyList<UserBriefAmount>)[];

        var signals = snapshot?.KeyBehaviourSignals
            .Select(x => new UserBriefSignal(x.Title, x.Severity))
            .ToList()
            ?? (IReadOnlyList<UserBriefSignal>)[];

        var risks = snapshot?.RiskFlags ?? (IReadOnlyList<string>)[];

        var cashflowRisk = DeriveCashflowRisk(financeData);
        var missingData = DeriveMissingData(financeData, hasConversationHistory);
        var (aiCanDo, aiNeedsApproval) = DerivePolicy(memoryEntries);

        using var assembleActivity = AiTelemetry.ActivitySource.StartActivity("aonik.user_brief.assemble", ActivityKind.Internal);
        assembleActivity?.SetTag("aonik.user_brief.top_category_count", topCategories.Count);
        assembleActivity?.SetTag("aonik.user_brief.top_merchant_count", topMerchants.Count);
        assembleActivity?.SetTag("aonik.user_brief.signal_count", signals.Count);
        assembleActivity?.SetTag("aonik.user_brief.risk_count", risks.Count);

        return new UserBrief(
            AsOf: DateTimeOffset.UtcNow,
            User: user,
            Goals: goals,
            Cash: cash,
            Period: period,
            TopCategories: topCategories,
            TopMerchants: topMerchants,
            Signals: signals,
            Risks: risks,
            CashflowRisk: cashflowRisk,
            MissingData: missingData,
            AiCanDo: aiCanDo,
            AiNeedsApproval: aiNeedsApproval);

        static async Task<T> TraceAsync<T>(string name, Func<Task<T>> operation)
        {
            using var childActivity = AiTelemetry.ActivitySource.StartActivity(name, ActivityKind.Internal);
            try
            {
                return await operation();
            }
            catch (Exception ex)
            {
                AiTelemetry.MarkError(childActivity, ex);
                throw;
            }
        }
    }

    private static string? ResolveName(
        UserBriefContextData userContextData,
        IReadOnlyList<UserBriefMemoryEntryData> memoryEntries)
    {
        var preferred = TryUnquote(
            memoryEntries.FirstOrDefault(e => e.Key == "identity.preferred_name")?.ValueJson);

        return FirstNonEmpty(
            preferred,
            userContextData.FirstName,
            userContextData.FullName?.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault(),
            userContextData.Email?.Split('@', 2, StringSplitOptions.TrimEntries).FirstOrDefault());
    }

    /// <summary>
    /// Low: available > 2x upcoming obligations.
    /// Moderate: available > 1x upcoming obligations.
    /// High: available &lt; upcoming obligations.
    /// </summary>
    private static CashflowRisk DeriveCashflowRisk(UserBriefFinancialData data)
    {
        var upcomingTotal = data.UpcomingBills.Sum(b => b.Amount ?? 0m);
        var available = data.AvailableBalance;

        if (upcomingTotal == 0) return CashflowRisk.Low;
        if (available >= upcomingTotal * 2) return CashflowRisk.Low;
        if (available >= upcomingTotal) return CashflowRisk.Moderate;
        return CashflowRisk.High;
    }

    private static IReadOnlyList<string> DeriveMissingData(
        UserBriefFinancialData financeData,
        bool hasConversationHistory)
    {
        var missing = new List<string>();

        if (financeData.AccountCount == 0) missing.Add("accounts");
        if (financeData.TransactionCount == 0) missing.Add("transactions");
        if (financeData.ActiveGoals.Count == 0) missing.Add("goals");
        if (financeData.UpcomingBills.Count == 0 && financeData.ActiveSubscriptions.Count == 0)
            missing.Add("bills_and_subscriptions");
        if (financeData.CustomerInsightSnapshot is null) missing.Add("customer_insight_snapshot");
        if (!hasConversationHistory) missing.Add("conversation_history");

        return missing;
    }

    private static (IReadOnlyList<string> CanDo, IReadOnlyList<string> NeedsApproval) DerivePolicy(
        IReadOnlyList<UserBriefMemoryEntryData> memoryEntries)
    {
        _ = memoryEntries; // policy is not yet memory-derived; kept for future per-user overrides
        return (
            ["view_balances", "categorise_transactions", "generate_insights", "send_reminders"],
            ["initiate_payment", "create_order", "modify_bill", "cancel_subscription"]);
    }

    private static string? FirstNonEmpty(params string?[] candidates) =>
        candidates.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c))?.Trim();

    private static string? TryUnquote(string? json)
    {
        if (json is null) return null;
        try { return JsonSerializer.Deserialize<string>(json); }
        catch { return json; }
    }

    /// <summary>
    /// Seeds the tenant / user context on a freshly-created scope so that
    /// EF Core global query filters (which read these from
    /// <see cref="ITenantContext"/> / <see cref="ICurrentUserContext"/>)
    /// resolve identically to the parent scope. Mirrors the equivalent
    /// helper on <see cref="ChatThreadManager"/>.
    /// </summary>
    private static void SeedScopeContext(IServiceProvider services, Guid tenantId, Guid userId)
    {
        var tc = services.GetService<ITenantContext>();
        if (tc is not null)
        {
            tc.TenantId = tenantId;
            tc.ResolutionSource = "user-brief-history-scope";
        }

        var uc = services.GetService<ICurrentUserContext>();
        if (uc is not null)
        {
            uc.UserId = userId;
            uc.TenantId = tenantId;
        }
    }
}
