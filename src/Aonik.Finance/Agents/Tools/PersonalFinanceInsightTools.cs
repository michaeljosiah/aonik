using System.ComponentModel;
using System.Text.Json;
using Aonik.Finance.Agents.StructuredOutputs;
using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.Extensions.AI;

namespace Aonik.Finance.Agents.Tools;

/// <summary>
/// Personal-finance insight tools — the Spec 025 analytical sub-agent triggers
/// (pf-insights / pf-forecast / pf-classify) and the customer-insight snapshot
/// reads/comparisons. The three sub-agent triggers route Simi's
/// "why / what next / clean my categories" questions to the CodeAct-powered
/// specialists (Spec 025 §5); all three return schema-bound JSON Simi
/// paraphrases before replying to the user. The shared sub-agent construction
/// machinery lives on <see cref="PersonalFinanceSubAgentToolGroup"/>. Registered by
/// <see cref="PersonalFinanceTools.CreateAll"/>.
/// </summary>
internal sealed class PersonalFinanceInsightTools : PersonalFinanceSubAgentToolGroup
{
    private readonly ICustomerInsightSnapshotReader _snapshotReader;

    public PersonalFinanceInsightTools(
        ICustomerInsightSnapshotReader snapshotReader,
        IChatClient chatClient,
        IServiceProvider serviceProvider,
        IAgentConfigurationService agentConfigurationService,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider)
        : base(chatClient, serviceProvider, agentConfigurationService, tenantProvider, currentUserProvider)
    {
        _snapshotReader = snapshotReader;
    }

    // ── Spec 025 Sub-Agent Tools ─────────────────────────────────
    //
    // Triggers that route Simi's "why / what next / clean my categories"
    // questions to the three CodeAct-powered analytical sub-agents
    // (Spec 025 §5). All three return schema-bound JSON Simi paraphrases
    // before replying to the user. Mutations stay on Simi's direct surface
    // (per-call confirmAction) — sub-agents are pure read-only by design.
    //
    // Descriptors are resolved from the service provider on demand instead
    // of cached in the constructor so the constructor signature does not
    // grow as more sub-agents are added.

    [Description("Runs the internal pf-insights specialist (Spec 025 §5.1) and returns schema-bound analysis JSON for 'why / what changed / walk-and-flag / rank' questions over historical spending and commitments. Use this for reasoning-heavy questions, subscription audits, anomaly detection, and ordered lists. Prefer one specialist per Simi turn.")]
    public async Task<InsightsAgentToolResponse> RunInsights(
        [Description("The user's question or planning goal as a natural-language string")] string userQuestion,
        [Description("Optional kind hint: 'explain' (why something happened), 'audit' (walk-and-flag a set), 'rank' (ordered list). Null lets the sub-agent decide based on the question.")] string? kind = null,
        [Description("Start of the analysis period (UTC). Null defaults to the start of the current month.")] DateTime? periodStart = null,
        [Description("End of the analysis period (UTC). Null defaults to today.")] DateTime? periodEnd = null,
        [Description("Optional account ID to scope the analysis to a single personal account")] Guid? personalAccountId = null,
        CancellationToken cancellationToken = default)
    {
        var request = new InsightsRequest(userQuestion, kind, periodStart, periodEnd, personalAccountId);
        var message = JsonSerializer.Serialize(request, InsightsStructuredOutputContract.SerializerOptions);

        // Snapshot the parent's user + tenant BEFORE any awaits so the
        // sub-agent's tools observe exactly the impersonated identity the
        // parent saw at the moment it decided to delegate — even if some
        // continuation downstream resets the scoped context. See
        // SubAgentImpersonation.cs for the full rationale.
        var snapshot = CaptureImpersonationSnapshot();

        try
        {
            // Agent construction is inside the try because the descriptor's
            // Build() resolves request-scoped state (ICodeActSandboxProvider,
            // tenant/user contexts) and any failure there used to escape as the
            // unactionable MAF "Error: Function failed." wrapper.
            var descriptor = ResolveSubAgentDescriptor("pf-insights");
            var agent = await BuildStructuredSubAgentAsync(descriptor, snapshot, cancellationToken);

            var response = await agent.RunAsync<InsightsResult>(
                message,
                session: null,
                serializerOptions: InsightsStructuredOutputContract.SerializerOptions,
                options: null,
                cancellationToken: cancellationToken);

            var analysisJson = JsonSerializer.Serialize(response.Result, InsightsStructuredOutputContract.SerializerOptions);
            return new InsightsAgentToolResponse(response.Result, analysisJson);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogSubAgentException("pf-insights", userQuestion, ex);
            return BuildInsightsErrorResponse(userQuestion, ex);
        }
    }

    [Description("Runs the internal pf-forecast specialist (Spec 025 §5.2) and returns schema-bound projection JSON for forward-looking questions: coverage on a future date ('will rent be okay'), savings ETA ('when do I hit my goal'), and parametric what-ifs ('what if I delay the energy bill'). The sub-agent does deterministic arithmetic — prefer this over reasoning about numbers in your own head. Prefer one specialist per Simi turn.")]
    public async Task<ForecastAgentToolResponse> RunForecast(
        [Description("The user's question or planning goal as a natural-language string")] string userQuestion,
        [Description("Optional reference date for the projection (UTC). Null defaults to today UTC.")] DateTime? asOfDate = null,
        [Description("Optional projection horizon in days. Null lets the sub-agent pick (typically 30-90 days).")] int? horizonDays = null,
        CancellationToken cancellationToken = default)
    {
        var request = new ForecastRequest(userQuestion, asOfDate, horizonDays);
        var message = JsonSerializer.Serialize(request, ForecastStructuredOutputContract.SerializerOptions);

        var snapshot = CaptureImpersonationSnapshot();

        try
        {
            var descriptor = ResolveSubAgentDescriptor("pf-forecast");
            var agent = await BuildStructuredSubAgentAsync(descriptor, snapshot, cancellationToken);

            var response = await agent.RunAsync<ForecastResult>(
                message,
                session: null,
                serializerOptions: ForecastStructuredOutputContract.SerializerOptions,
                options: null,
                cancellationToken: cancellationToken);

            var analysis = response.Result;
            var analysisJson = JsonSerializer.Serialize(analysis, ForecastStructuredOutputContract.SerializerOptions);
            return new ForecastAgentToolResponse(analysis, analysisJson);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogSubAgentException("pf-forecast", userQuestion, ex);
            return BuildForecastErrorResponse(userQuestion, ex);
        }
    }

    [Description("Runs the internal pf-classify specialist (Spec 025 §5.3) on the user's classification review queue and returns schema-bound proposed corrections per item, plus optional categorisation-rule recommendations where the merchant pattern is strong. The sub-agent only proposes — apply each user-accepted correction via pf_override_transaction_category and pf_create_categorisation_rule (with confirmAction). Prefer one specialist per Simi turn.")]
    public async Task<ClassifyAgentToolResponse> RunClassifyReview(
        [Description("The user's question or framing for the review (e.g. 'help me clean up my categories', 'what's still waiting to be classified')")] string userQuestion,
        [Description("Max number of queue items to review in this pass (default: 25). Simi can re-invoke for further pages.")] int? maxItems = null,
        [Description("Optional account ID to scope the queue to a single personal account")] Guid? personalAccountId = null,
        CancellationToken cancellationToken = default)
    {
        var request = new ClassifyRequest(userQuestion, maxItems, personalAccountId);
        var message = JsonSerializer.Serialize(request, ClassifyStructuredOutputContract.SerializerOptions);

        var snapshot = CaptureImpersonationSnapshot();

        ClassifyResult analysis;
        try
        {
            var descriptor = ResolveSubAgentDescriptor("pf-classify");
            var agent = await BuildStructuredSubAgentAsync(descriptor, snapshot, cancellationToken);

            var response = await agent.RunAsync<ClassifyResult>(
                message,
                session: null,
                serializerOptions: ClassifyStructuredOutputContract.SerializerOptions,
                options: null,
                cancellationToken: cancellationToken);
            analysis = response.Result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogSubAgentException("pf-classify", userQuestion, ex);
            return BuildClassifyErrorResponse(userQuestion, ex);
        }

        var analysisJson = JsonSerializer.Serialize(analysis, ClassifyStructuredOutputContract.SerializerOptions);
        return new ClassifyAgentToolResponse(analysis, analysisJson);
    }

    // ── Sub-agent error handling ──────────────────────────────────
    //
    // Failures inside a sub-agent run (Microsoft.Agents.AI / Microsoft.Extensions.AI
    // exceptions, EF query errors thrown by a tool, structured-output schema
    // validation, etc.) used to bubble up to the parent agent as the generic
    // "Function failed" string with no detail — unactionable in the playground
    // and bad for the customer experience. We now catch them, log the full
    // exception, and synthesise a valid structured response that carries the
    // exception type + message in the warnings / reason codes. The parent agent
    // can read that and surface a helpful message; the original error still
    // shows up in logs for the developer.

    private static InsightsAgentToolResponse BuildInsightsErrorResponse(string userQuestion, Exception ex)
    {
        var message = FormatExceptionForResponse(ex);
        var emptyMetrics = JsonDocument.Parse("{}").RootElement;
        var analysis = new InsightsResult(
            SchemaVersion: InsightsStructuredOutputContract.SchemaVersion,
            Kind: "explain",
            Summary: $"The insights sub-agent crashed while answering '{userQuestion}'. Tell the user we hit an internal error and offer to retry or rephrase.",
            Confidence: 0m,
            ReasonCodes: ["sub_agent_exception"],
            Metrics: emptyMetrics,
            Entities: [],
            RecommendedActions: [],
            Warnings: [message]);
        var analysisJson = JsonSerializer.Serialize(analysis, InsightsStructuredOutputContract.SerializerOptions);
        return new InsightsAgentToolResponse(analysis, analysisJson);
    }

    private static ForecastAgentToolResponse BuildForecastErrorResponse(string userQuestion, Exception ex)
    {
        var message = FormatExceptionForResponse(ex);
        var analysis = new ForecastResult(
            SchemaVersion: ForecastStructuredOutputContract.SchemaVersion,
            Scenario: "Sub-agent crashed",
            Result: new ForecastVerdict(Verdict: "tight", Amount: 0m, Currency: "GBP"),
            Assumptions: [$"The forecast sub-agent crashed while answering '{userQuestion}'."],
            Breakdown: [],
            Options: [],
            Confidence: 0m,
            ReasonCodes: ["sub_agent_exception"],
            Warnings: [message]);
        var analysisJson = JsonSerializer.Serialize(analysis, ForecastStructuredOutputContract.SerializerOptions);
        return new ForecastAgentToolResponse(analysis, analysisJson);
    }

    private static ClassifyAgentToolResponse BuildClassifyErrorResponse(string userQuestion, Exception ex)
    {
        var message = FormatExceptionForResponse(ex);
        var analysis = new ClassifyResult(
            SchemaVersion: ClassifyStructuredOutputContract.SchemaVersion,
            Summary: $"The classify sub-agent crashed while answering '{userQuestion}'. Tell the user we hit an internal error and offer to retry.",
            ProposedCorrections: [],
            Confidence: 0m,
            ReasonCodes: ["sub_agent_exception"],
            Warnings: [message]);
        var analysisJson = JsonSerializer.Serialize(analysis, ClassifyStructuredOutputContract.SerializerOptions);
        return new ClassifyAgentToolResponse(analysis, analysisJson);
    }

    // ── Customer Insight Snapshot Read Tools ──────────────────────

    [Description("Lists historical customer insight snapshots for the current user, most recent first. Each entry is a lightweight summary: SnapshotId, Status (Current/Superseded/Failed), AsOfUtc (when it was generated), WindowStartUtc and WindowEndUtc (the 30-day analysis window it covers), Version, and IsPartial. Use this to discover which periods are available for multi-period spending comparisons, then call pf_compare_snapshots with 2-6 SnapshotIds.")]
    public async Task<IReadOnlyList<CustomerInsightSnapshotHistoryItemResponse>> ListSnapshotHistory(
        [Description("Maximum number of historical snapshots to return. Defaults to 12 (covers ~12 monthly snapshots). Maximum 50.")] int take = 12,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserProvider.GetCurrentUserId()
            ?? throw new InvalidOperationException("Authenticated user is required.");

        var limit = Math.Clamp(take, 1, 50);
        return await _snapshotReader.GetSnapshotHistoryAsync(userId, limit, cancellationToken);
    }

    [Description("Compares spending and financial health across 2-6 historical customer insight snapshots for month-over-month (or longer) trend analysis. Pass SnapshotIds from pf_list_snapshot_history. Returns a compact per-period summary containing: analysis window, total inflows/outflows by currency, essential vs discretionary spend, top 5 categories by amount with share and transaction count, top 5 merchants by amount, budget pressure counts (active/overspent/projected-overspend), cashflow stress level, budget pressure level, and key signal titles. Use this to answer 'how does this month compare to last month', 'is my spending trending up or down', or 'which categories are growing fastest'. The compact shape is designed for LLM consumption — do not ask for the raw snapshot document.")]
    public async Task<IReadOnlyList<SnapshotComparisonSummary>> CompareSnapshots(
        [Description("List of snapshot IDs to include in the comparison. Order them chronologically (oldest first) for natural trend reading. 2-6 IDs.")] IReadOnlyList<Guid> snapshotIds,
        [Description("How many top categories to return per period. Defaults to 5, maximum 10.")] int topCategories = 5,
        [Description("How many top merchants to return per period. Defaults to 5, maximum 10.")] int topMerchants = 5,
        [Description("How many key signals to return per period. Defaults to 5, maximum 10.")] int topSignals = 5,
        CancellationToken cancellationToken = default)
    {
        if (snapshotIds is null || snapshotIds.Count == 0)
        {
            throw new ArgumentException("At least one snapshotId is required.", nameof(snapshotIds));
        }

        if (snapshotIds.Count > 6)
        {
            throw new ArgumentException("At most 6 snapshotIds may be compared at once.", nameof(snapshotIds));
        }

        var userId = CurrentUserProvider.GetCurrentUserId()
            ?? throw new InvalidOperationException("Authenticated user is required.");

        var catLimit = Math.Clamp(topCategories, 1, 10);
        var merLimit = Math.Clamp(topMerchants, 1, 10);
        var sigLimit = Math.Clamp(topSignals, 1, 10);

        var summaries = new List<SnapshotComparisonSummary>(snapshotIds.Count);
        foreach (var snapshotId in snapshotIds)
        {
            var snapshot = await _snapshotReader.GetSnapshotAsync(snapshotId, cancellationToken);
            if (snapshot is null || snapshot.UserId != userId)
            {
                throw new InvalidOperationException($"Snapshot {snapshotId} not found.");
            }

            summaries.Add(BuildComparisonSummary(snapshot, catLimit, merLimit, sigLimit));
        }

        return summaries;
    }

    private static SnapshotComparisonSummary BuildComparisonSummary(
        CustomerInsightSnapshotResponse response,
        int topCategories,
        int topMerchants,
        int topSignals)
    {
        var doc = response.Snapshot;
        var metrics = doc?.Metrics;

        var incomes = metrics is null
            ? Array.Empty<CurrencyAmount>()
            : metrics.Income.TotalInflowsByCurrency
                .Select(m => new CurrencyAmount(m.Currency, m.Amount))
                .ToArray();

        var outflows = metrics is null
            ? Array.Empty<CurrencyAmount>()
            : metrics.Expense.TotalOutflowsByCurrency
                .Select(m => new CurrencyAmount(m.Currency, m.Amount))
                .ToArray();

        var essential = metrics is null
            ? Array.Empty<CurrencyAmount>()
            : metrics.Expense.EssentialSpendEstimateByCurrency
                .Select(m => new CurrencyAmount(m.Currency, m.Amount))
                .ToArray();

        var discretionary = metrics is null
            ? Array.Empty<CurrencyAmount>()
            : metrics.Expense.DiscretionarySpendEstimateByCurrency
                .Select(m => new CurrencyAmount(m.Currency, m.Amount))
                .ToArray();

        var categories = metrics is null
            ? Array.Empty<CategoryLine>()
            : metrics.Categories.TopCategoriesByAmount
                .Take(topCategories)
                .Select(c => new CategoryLine(
                    c.Category,
                    c.Currency,
                    c.Amount,
                    c.ShareOfSpend,
                    c.TransactionCount,
                    c.PreviousPeriodAmount,
                    c.DeltaPercentage))
                .ToArray();

        var merchants = metrics is null
            ? Array.Empty<MerchantLine>()
            : metrics.Merchants.TopMerchantsByAmount
                .Take(topMerchants)
                .Select(m => new MerchantLine(m.Merchant, m.Currency, m.Amount, m.TransactionCount))
                .ToArray();

        var budgets = metrics?.Budgets;
        var activeBudgetCount = budgets?.ActiveBudgetCount ?? 0;
        var overspentCount = budgets?.OverspentCategories.Count ?? 0;
        var projectedOverspendCount = budgets?.ProjectedPressureCategories.Count ?? 0;

        var risk = doc?.Risk;
        var cashflowStress = risk?.CashflowStressLevel ?? "Unknown";
        var budgetPressure = risk?.BudgetPressureLevel ?? "Unknown";

        var signals = doc?.Signals is null
            ? Array.Empty<SignalLine>()
            : doc.Signals
                .OrderByDescending(s => SeverityRank(s.Severity))
                .Take(topSignals)
                .Select(s => new SignalLine(s.Title, s.Category, s.Severity, s.Confidence))
                .ToArray();

        var windowDays = doc?.AnalysisWindow?.OperationalWindowDays
            ?? (int)Math.Round((response.WindowEndUtc - response.WindowStartUtc).TotalDays);

        return new SnapshotComparisonSummary(
            response.Id,
            response.Status,
            response.AsOfUtc,
            response.WindowStartUtc,
            response.WindowEndUtc,
            windowDays,
            response.Version,
            incomes,
            outflows,
            essential,
            discretionary,
            categories,
            merchants,
            activeBudgetCount,
            overspentCount,
            projectedOverspendCount,
            cashflowStress,
            budgetPressure,
            signals);
    }

    private static int SeverityRank(string? severity) => severity switch
    {
        "Critical" => 4,
        "High" => 3,
        "Moderate" => 2,
        "Low" => 1,
        _ => 0,
    };
}

// ── Snapshot comparison DTOs ──────────────────────────────────
//
// Compact per-period shape for pf_compare_snapshots. Strips the full
// CustomerInsightSnapshotDocument down to the fields the LLM actually needs
// to reason about month-over-month trends so responses stay within budget.

public record SnapshotComparisonSummary(
    Guid SnapshotId,
    string Status,
    DateTime AsOfUtc,
    DateTime WindowStartUtc,
    DateTime WindowEndUtc,
    int WindowDays,
    int Version,
    IReadOnlyList<CurrencyAmount> TotalInflowsByCurrency,
    IReadOnlyList<CurrencyAmount> TotalOutflowsByCurrency,
    IReadOnlyList<CurrencyAmount> EssentialSpendByCurrency,
    IReadOnlyList<CurrencyAmount> DiscretionarySpendByCurrency,
    IReadOnlyList<CategoryLine> TopCategories,
    IReadOnlyList<MerchantLine> TopMerchants,
    int ActiveBudgetCount,
    int OverspentBudgetCount,
    int ProjectedOverspendCount,
    string CashflowStressLevel,
    string BudgetPressureLevel,
    IReadOnlyList<SignalLine> KeySignals);

public record CurrencyAmount(string Currency, decimal Amount);

public record CategoryLine(
    string Category,
    string Currency,
    decimal Amount,
    decimal ShareOfSpend,
    int TransactionCount,
    decimal PreviousPeriodAmount,
    decimal? DeltaPercentage);

public record MerchantLine(
    string Merchant,
    string Currency,
    decimal Amount,
    int TransactionCount);

public record SignalLine(
    string Title,
    string Category,
    string Severity,
    string Confidence);
