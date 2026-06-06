using System.ComponentModel;
using System.Text.Json;
using Aonik.Finance.Agents.StructuredOutputs;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.Finance.Contracts.Models.Orders;
using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.Orders;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Finance.Contracts.Services.Pricing;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Finance.Agents.Tools;

/// <summary>
/// AI agent tools for personal finance operations.
/// Each method is exposed to the LLM via <see cref="AIFunctionFactory.Create"/>.
/// Read-only tools are safe for autonomous use; mutating tools rely on the
/// <c>confirmAction</c> frontend tool for human-in-the-loop approval.
/// </summary>
internal sealed class PersonalFinanceTools
{
    private readonly IPersonalAccountService _accountService;
    private readonly IPersonalTransactionService _transactionService;
    private readonly IBillService _billService;
    private readonly IBudgetService _budgetService;
    private readonly ICommitmentService _commitmentService;
    private readonly IPersonalFinanceInsightsService _insightsService;
    private readonly IDashboardService _dashboardService;
    private readonly IFxRateService _fxRateService;
    private readonly ITransactionClassificationService _classificationService;
    private readonly IStatementImportService _statementImportService;
    private readonly ITransactionAttachmentService _attachmentService;
    private readonly ICustomerInsightSnapshotReader _snapshotReader;
    private readonly IOrderService _orderService;
    private readonly FinanceDbContext _financeDbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IChatClient _chatClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly IAgentConfigurationService _agentConfigurationService;

    private PersonalFinanceTools(
        IPersonalAccountService accountService,
        IPersonalTransactionService transactionService,
        IBillService billService,
        IBudgetService budgetService,
        ICommitmentService commitmentService,
        IPersonalFinanceInsightsService insightsService,
        IDashboardService dashboardService,
        IFxRateService fxRateService,
        ITransactionClassificationService classificationService,
        IStatementImportService statementImportService,
        ITransactionAttachmentService attachmentService,
        ICustomerInsightSnapshotReader snapshotReader,
        IOrderService orderService,
        FinanceDbContext financeDbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        IChatClient chatClient,
        IServiceProvider serviceProvider,
        IAgentConfigurationService agentConfigurationService)
    {
        _accountService = accountService;
        _transactionService = transactionService;
        _billService = billService;
        _budgetService = budgetService;
        _commitmentService = commitmentService;
        _insightsService = insightsService;
        _dashboardService = dashboardService;
        _fxRateService = fxRateService;
        _classificationService = classificationService;
        _statementImportService = statementImportService;
        _attachmentService = attachmentService;
        _snapshotReader = snapshotReader;
        _orderService = orderService;
        _financeDbContext = financeDbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _chatClient = chatClient;
        _serviceProvider = serviceProvider;
        _agentConfigurationService = agentConfigurationService;
    }

    // ── Account Read Tools ────────────────────────────────────────

    [Description("Lists all personal financial accounts for the current user. Returns account names, types, balances, and statuses. Set includeArchived to true to include archived accounts.")]
    public async Task<IReadOnlyList<PersonalAccountResponse>> ListAccounts(
        [Description("Whether to include archived accounts (default: false)")] bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        return await _accountService.ListAccountsAsync(includeArchived, cancellationToken);
    }

    [Description("Retrieves a personal account by its unique identifier. Returns the full account details including balance, institution, and status.")]
    public async Task<PersonalAccountResponse?> GetAccount(
        [Description("The unique identifier (GUID) of the personal account")] Guid accountId,
        CancellationToken cancellationToken = default)
    {
        return await _accountService.GetAccountAsync(accountId, cancellationToken);
    }

    // ── Transaction Read Tools ────────────────────────────────────

    [Description("Lists personal transactions with optional filters. Supports filtering by date range, account, financial context (space), category, and free-text search. Results are paginated.")]
    public async Task<IReadOnlyList<PersonalTransactionResponse>> ListTransactions(
        [Description("Start date filter (UTC, inclusive)")] DateTime? from = null,
        [Description("End date filter (UTC, inclusive)")] DateTime? to = null,
        [Description("Filter by personal account ID")] Guid? personalAccountId = null,
        [Description("Filter by financial context (space) ID")] Guid? financialContextId = null,
        [Description("Filter by category name")] string? category = null,
        [Description("Free-text search in merchant/description")] string? search = null,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Page size (default: 50, max: 100)")] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var request = new ListPersonalTransactionsRequest(from, to, personalAccountId, financialContextId, category, search, page, pageSize);
        return await _transactionService.ListTransactionsAsync(request, cancellationToken);
    }

    [Description("Retrieves a personal transaction by its unique identifier. Returns full details including merchant, category, classification info, and notes.")]
    public async Task<PersonalTransactionResponse?> GetTransaction(
        [Description("The unique identifier (GUID) of the personal transaction")] Guid transactionId,
        CancellationToken cancellationToken = default)
    {
        return await _transactionService.GetTransactionAsync(transactionId, cancellationToken);
    }

    // ── Bill Read Tools ───────────────────────────────────────────

    [Description("Lists all bills for the current user. Optionally filter by status (e.g. 'Active', 'Archived').")]
    public async Task<IReadOnlyList<BillResponse>> ListBills(
        [Description("Optional status filter (e.g. 'Active', 'Archived')")] string? status = null,
        CancellationToken cancellationToken = default)
    {
        return await _billService.ListBillsAsync(status, cancellationToken);
    }

    [Description("Retrieves a bill by its unique identifier. Returns full details including payee, frequency, next due date, and linked references.")]
    public async Task<BillResponse?> GetBill(
        [Description("The unique identifier (GUID) of the bill")] Guid billId,
        CancellationToken cancellationToken = default)
    {
        return await _billService.GetBillAsync(billId, cancellationToken);
    }

    [Description("Gets upcoming bills due within a specified number of days. Useful for showing what payments are coming soon.")]
    public async Task<IReadOnlyList<BillResponse>> GetUpcomingBills(
        [Description("Number of days ahead to look for upcoming bills (default: 7)")] int daysAhead = 7,
        CancellationToken cancellationToken = default)
    {
        return await _billService.GetUpcomingBillsAsync(daysAhead, cancellationToken);
    }

    // ── Budget Read Tool ──────────────────────────────────────────

    [Description("Lists the user's budget categories for the current month. Each category returns its line-item ID, display name, allocated amount, and spent-to-date amount, plus a short spending history. Use this to answer 'what's in my budget', 'how much have I spent vs allocated', 'am I over budget on X', and similar questions.")]
    public async Task<IReadOnlyList<BudgetCategoryResponse>> ListBudgets(
        CancellationToken cancellationToken = default)
    {
        return await _budgetService.ListBudgetsAsync(cancellationToken);
    }

    // ── Insights Read Tools ───────────────────────────────────────

    [Description("Gets a spending summary for a given period. Returns total income, total expenses, net savings, and transaction count. Optionally scoped to a specific account.")]
    public async Task<SpendingSummaryResponse> GetSpendingSummary(
        [Description("Start of the analysis period (UTC)")] DateTime periodStart,
        [Description("End of the analysis period (UTC)")] DateTime periodEnd,
        [Description("Optional: scope to a specific personal account ID")] Guid? personalAccountId = null,
        CancellationToken cancellationToken = default)
    {
        return await _insightsService.GetSpendingSummaryAsync(periodStart, periodEnd, personalAccountId, cancellationToken);
    }

    [Description("Gets spending broken down by category for a given period. Returns each category's total amount and percentage of overall spending. If the period contains spending in multiple currencies and no specific account is supplied, the result defaults to the dominant spend currency for that window so the breakdown remains coherent.")]
    public async Task<IReadOnlyList<CategorySpendingItemResponse>> GetCategoryBreakdown(
        [Description("Start of the analysis period (UTC)")] DateTime periodStart,
        [Description("End of the analysis period (UTC)")] DateTime periodEnd,
        [Description("Optional: scope to a specific personal account ID")] Guid? personalAccountId = null,
        CancellationToken cancellationToken = default)
    {
        return await _insightsService.GetCategoryBreakdownAsync(periodStart, periodEnd, personalAccountId, cancellationToken);
    }

    [Description("Gets spending broken down by merchant for a given period. Returns the top merchants by total amount spent. If the period contains spending in multiple currencies and no specific account is supplied, the result defaults to the dominant spend currency for that window so the ranking remains coherent.")]
    public async Task<IReadOnlyList<MerchantSpendingItemResponse>> GetMerchantBreakdown(
        [Description("Start of the analysis period (UTC)")] DateTime periodStart,
        [Description("End of the analysis period (UTC)")] DateTime periodEnd,
        [Description("Optional: scope to a specific personal account ID")] Guid? personalAccountId = null,
        [Description("Number of top merchants to return (default: 10)")] int top = 10,
        CancellationToken cancellationToken = default)
    {
        return await _insightsService.GetMerchantBreakdownAsync(periodStart, periodEnd, personalAccountId, top, cancellationToken);
    }

    [Description("Gets spending broken down by personal account for a given period. Returns each account's total expense amount and transaction count, sorted by amount. Use this for 'which account has my biggest spend' or per-account expense comparisons.")]
    public async Task<IReadOnlyList<AccountSpendingItemResponse>> GetAccountBreakdown(
        [Description("Start of the analysis period (UTC)")] DateTime periodStart,
        [Description("End of the analysis period (UTC)")] DateTime periodEnd,
        CancellationToken cancellationToken = default)
    {
        return await _insightsService.GetAccountBreakdownAsync(periodStart, periodEnd, cancellationToken);
    }

    [Description("Gets the all-time spend history with a specific merchant. Returns transaction count, average spend, and total spent for that merchant (already formatted with the merchant's transaction currency symbol). Use this for 'how much have I spent at <merchant>' or 'how often do I shop at <merchant>' questions.")]
    public async Task<MerchantHistoryResponse> GetMerchantHistory(
        [Description("The merchant name to look up (exact match, case-sensitive)")] string merchantName,
        CancellationToken cancellationToken = default)
    {
        return await _insightsService.GetMerchantHistoryAsync(merchantName, cancellationToken);
    }

    // ── Dashboard Read Tool ───────────────────────────────────────

    [Description("Gets the personal finance dashboard overview. Returns aggregated metrics (net worth, available to spend, assets, bills due), upcoming bills, recent orders, and a monthly spending breakdown.")]
    public async Task<DashboardResponse> GetDashboard(
        CancellationToken cancellationToken = default)
    {
        return await _dashboardService.GetDashboardAsync(cancellationToken);
    }

    // ── FX Rate Read Tool ─────────────────────────────────────────

    [Description("Gets historical FX rate data for a currency pair over the past N days. Returns daily rate points and a buy/hold/wait timing signal. Use this to fetch real rate data before calling the display_fx_rate_chart frontend tool.")]
    public async Task<FxRateHistoryResult> GetFxRateHistory(
        [Description("ISO 4217 base currency code (e.g., 'GBP')")] string baseCurrency,
        [Description("ISO 4217 target currency code (e.g., 'NGN')")] string targetCurrency,
        [Description("Number of days of history to fetch (default: 7)")] int days = 7,
        CancellationToken cancellationToken = default)
    {
        return await _fxRateService.GetRateHistoryAsync(baseCurrency, targetCurrency, days, cancellationToken);
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

        try
        {
            // Agent construction is inside the try because the descriptor's
            // Build() resolves request-scoped state (ICodeActSandboxProvider,
            // tenant/user contexts) and any failure there used to escape as the
            // unactionable MAF "Error: Function failed." wrapper.
            var descriptor = ResolveSubAgentDescriptor("pf-insights");
            var agent = await BuildStructuredSubAgentAsync(descriptor, cancellationToken);

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

        try
        {
            var descriptor = ResolveSubAgentDescriptor("pf-forecast");
            var agent = await BuildStructuredSubAgentAsync(descriptor, cancellationToken);

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

        ClassifyResult analysis;
        try
        {
            var descriptor = ResolveSubAgentDescriptor("pf-classify");
            var agent = await BuildStructuredSubAgentAsync(descriptor, cancellationToken);

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

    private void LogSubAgentException(string subAgentName, string userQuestion, Exception ex)
    {
        var loggerFactory = _serviceProvider.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
        var logger = loggerFactory.CreateLogger("PersonalFinanceTools.SubAgent");
        logger.LogError(
            ex,
            "Sub-agent {SubAgent} failed for question '{Question}': {Message}",
            subAgentName,
            userQuestion,
            ex.Message);
    }

    private static string FormatExceptionForResponse(Exception ex)
    {
        // Keep the message short enough that Simi can paraphrase it without
        // hitting context-window pressure, but include the type + inner-
        // exception chain so the playground reveals enough to act on.
        var lines = new List<string> { $"{ex.GetType().Name}: {ex.Message}" };
        var inner = ex.InnerException;
        var depth = 0;
        while (inner is not null && depth < 3)
        {
            lines.Add($"  caused by {inner.GetType().Name}: {inner.Message}");
            inner = inner.InnerException;
            depth++;
        }
        var joined = string.Join('\n', lines);
        return joined.Length > 1200 ? joined[..1200] + "..." : joined;
    }

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

    private IDomainAgentDescriptor ResolveSubAgentDescriptor(string name)
    {
        var descriptor = _serviceProvider
            .GetServices<IDomainAgentDescriptor>()
            .FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.Ordinal));

        return descriptor
            ?? throw new InvalidOperationException(
                $"The '{name}' sub-agent descriptor is not registered in DI. Check FinanceModule.ConfigureServices.");
    }

    // ── Account Mutating Tools ────────────────────────────────────

    [Description("Creates a new personal financial account. Requires a name, account type (e.g. 'Checking', 'Savings', 'CreditCard'), and currency. Optionally specify institution, last 4 digits, and subtype.")]
    public async Task<PersonalAccountResponse> CreateAccount(
        [Description("Display name for the account (e.g. 'Main Checking')")] string name,
        [Description("Account type (e.g. 'Checking', 'Savings', 'CreditCard', 'Investment', 'Loan')")] string accountType,
        [Description("ISO 4217 currency code (e.g. USD, NGN, GBP)")] string currency,
        [Description("Optional: name of the financial institution")] string? institutionName = null,
        [Description("Optional: external reference ID from an aggregator")] string? externalReference = null,
        [Description("Optional: account subtype for further classification")] string? accountSubtype = null,
        [Description("Optional: last 4 digits of the account number")] string? last4 = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreatePersonalAccountRequest(name, accountType, currency, institutionName, externalReference, accountSubtype, last4);
        return await _accountService.CreateAccountAsync(request, cancellationToken);
    }

    [Description("Archives a personal account. The account will no longer appear in default listings but remains in the system for historical reference.")]
    public async Task<string> ArchiveAccount(
        [Description("The unique identifier (GUID) of the account to archive")] Guid accountId,
        CancellationToken cancellationToken = default)
    {
        await _accountService.ArchiveAccountAsync(accountId, cancellationToken);
        return $"Account {accountId} has been archived successfully.";
    }

    // ── Transaction Mutating Tools ────────────────────────────────

    [Description("Creates a manual personal transaction. Use this for transactions not imported from a bank (e.g. cash payments, manual adjustments). Requires date, amount, and currency.")]
    public async Task<PersonalTransactionResponse> CreateManualTransaction(
        [Description("Date/time when the transaction occurred (UTC)")] DateTime occurredAt,
        [Description("Transaction amount (positive for income, negative for expense)")] decimal amount,
        [Description("ISO 4217 currency code (e.g. USD, NGN)")] string currency,
        [Description("Optional: personal account ID to associate with")] Guid? personalAccountId = null,
        [Description("Optional: merchant name")] string? merchant = null,
        [Description("Optional: description of the transaction")] string? description = null,
        [Description("Optional: spending category (e.g. 'Groceries', 'Transport', 'Entertainment')")] string? category = null,
        [Description("Optional: additional notes")] string? notes = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateManualPersonalTransactionRequest(
            personalAccountId, occurredAt, amount, currency, merchant, description, category, notes, null);
        return await _transactionService.CreateManualTransactionAsync(request, cancellationToken);
    }

    // ── Bill Mutating Tools ───────────────────────────────────────

    [Description("Creates a new recurring bill. Specify the payee, frequency (e.g. 'Monthly', 'Weekly', 'Yearly'), next due date, expected amount, and currency.")]
    public async Task<BillResponse> CreateBill(
        [Description("Name of the payee (e.g. 'Netflix', 'Electricity Company')")] string payee,
        [Description("Billing frequency (e.g. 'Monthly', 'Weekly', 'Biweekly', 'Yearly')")] string frequency,
        [Description("Next due date in UTC")] DateTime nextDueDate,
        [Description("Expected payment amount")] decimal? expectedAmount,
        [Description("ISO 4217 currency code (e.g. USD, NGN)")] string currency,
        [Description("Whether this bill is on autopay")] bool autopay = false,
        [Description("Optional: account ID to pay from")] Guid? paidFromAccountId = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateBillRequest(payee, frequency, nextDueDate, expectedAmount, currency, autopay, paidFromAccountId);
        return await _billService.CreateBillAsync(request, cancellationToken);
    }

    [Description("Updates fields on an existing bill. Only the parameters you provide are changed; unspecified fields keep their current values. Use this to reschedule a bill (nextDueDate), adjust an amount, rename a payee, toggle autopay, change the currency, switch the paying account, or change lifecycle status (e.g. 'Active', 'Paid', 'Overdue'). To stop a bill entirely, use pf_archive_bill instead. Requires confirmAction approval.")]
    public async Task<BillResponse> UpdateBill(
        [Description("The unique identifier (GUID) of the bill to update")] Guid billId,
        [Description("Optional: new payee name")] string? payee = null,
        [Description("Optional: new billing frequency (e.g. 'Monthly', 'Weekly', 'Biweekly', 'Yearly')")] string? frequency = null,
        [Description("Optional: new next due date in UTC")] DateTime? nextDueDate = null,
        [Description("Optional: new expected payment amount. Omit to keep the current amount.")] decimal? expectedAmount = null,
        [Description("Optional: new ISO 4217 currency code (e.g. USD, NGN)")] string? currency = null,
        [Description("Optional: enable or disable autopay")] bool? autopay = null,
        [Description("Optional: new account ID to pay from. Omit to keep the current source account.")] Guid? paidFromAccountId = null,
        [Description("Optional: new lifecycle status (e.g. 'Active', 'Paid', 'Overdue'). Use pf_archive_bill to archive a bill rather than setting this to 'Archived'.")] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var existing = await _billService.GetBillAsync(billId, cancellationToken)
            ?? throw new InvalidOperationException($"Bill {billId} not found.");

        var request = new UpdateBillRequest(
            payee ?? existing.Payee,
            frequency ?? existing.Frequency,
            nextDueDate ?? existing.NextDueDate,
            expectedAmount ?? existing.ExpectedAmount,
            currency ?? existing.Currency,
            autopay ?? existing.Autopay,
            paidFromAccountId ?? existing.PaidFromAccountId,
            status ?? existing.Status);

        return await _billService.UpdateBillAsync(billId, request, cancellationToken);
    }

    [Description("Archives a bill, marking it as no longer active. The bill remains in the system for historical reference.")]
    public async Task<string> ArchiveBill(
        [Description("The unique identifier (GUID) of the bill to archive")] Guid billId,
        CancellationToken cancellationToken = default)
    {
        await _billService.ArchiveBillAsync(billId, cancellationToken);
        return $"Bill {billId} has been archived successfully.";
    }

    // ── Budget Mutating Tools ─────────────────────────────────────

    [Description("Adds a new budget line to the current month's budget. Pass a categoryId from the known template set (e.g. 'groceries', 'housing', 'transport', 'utilities', 'eating-out', 'bills', 'subscriptions', 'entertainment', 'savings', 'health', 'travel') when possible; leave null for a generic line. The new line starts with a zero allocation — use pf_update_budget_amount to set the limit. Requires confirmAction approval.")]
    public async Task<BudgetCategoryResponse> CreateBudget(
        [Description("Optional template category ID (e.g. 'groceries'). Null creates an unnamed/generic line.")] string? categoryId = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateBudgetRequest(categoryId);
        return await _budgetService.CreateBudgetAsync(request, cancellationToken);
    }

    [Description("Updates the allocated limit for a budget line in the current month. Pass the budget line ID (from pf_list_budgets line-items) and the new total allocation. Returns the refreshed budget list. Requires confirmAction approval.")]
    public async Task<IReadOnlyList<BudgetCategoryResponse>> UpdateBudgetAmount(
        [Description("The unique identifier (GUID) of the budget line to update")] Guid budgetLineId,
        [Description("The new total allocation for this budget line (in the budget's currency)")] decimal totalAllocated,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateBudgetAmountRequest(totalAllocated);
        return await _budgetService.UpdateBudgetAmountAsync(budgetLineId, request, cancellationToken);
    }

    [Description("Permanently removes a budget line from the current month's budget. This is a hard delete — the line and its allocation are gone. Returns the refreshed budget list. Requires confirmAction approval.")]
    public async Task<IReadOnlyList<BudgetCategoryResponse>> DeleteBudget(
        [Description("The unique identifier (GUID) of the budget line to delete")] Guid budgetLineId,
        CancellationToken cancellationToken = default)
    {
        return await _budgetService.DeleteBudgetAsync(budgetLineId, cancellationToken);
    }

    // ── Commitment Read Tools ─────────────────────────────────────

    [Description("Lists all recurring commitments (bills, subscriptions, debt repayments) for the current user. Supports filtering by type ('Bill', 'Subscription', 'DebtRepayment'), status ('Active', 'Paused', 'Cancelled'), and verification status ('Detected', 'Confirmed', 'Rejected'). Returns paginated results with summary totals.")]
    public async Task<CommitmentListResponse> ListCommitments(
        [Description("Filter by commitment type: 'Bill', 'Subscription', or 'DebtRepayment'. Null returns all.")] string? type = null,
        [Description("Filter by lifecycle status: 'Active', 'Paused', 'Cancelled', 'Archived'. Null returns all.")] string? status = null,
        [Description("Filter by verification status: 'Detected', 'Confirmed', 'Rejected'. Null returns all.")] string? verificationStatus = null,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Page size (default: 20, max: 100)")] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var filter = new CommitmentListFilter(
            Type: type,
            Status: status,
            VerificationStatus: verificationStatus,
            Page: page,
            PageSize: pageSize);
        return await _commitmentService.ListCommitmentsAsync(filter, cancellationToken);
    }

    [Description("Gets full details of a single commitment by ID. Works across all commitment types (bills, subscriptions, debt repayments).")]
    public async Task<CommitmentDetail?> GetCommitment(
        [Description("The unique identifier (GUID) of the commitment")] Guid commitmentId,
        CancellationToken cancellationToken = default)
    {
        return await _commitmentService.GetCommitmentAsync(commitmentId, cancellationToken);
    }

    [Description("Lists all detected (unreviewed) commitments that the system found from transaction patterns. These need user review to confirm or reject.")]
    public async Task<IReadOnlyList<CommitmentItem>> ListDetectedCommitments(
        CancellationToken cancellationToken = default)
    {
        return await _commitmentService.ListDetectedAsync(cancellationToken);
    }

    // ── Classification Read Tool ──────────────────────────────────

    [Description("Lists transactions in the classification review queue — those with no category assigned or with a pending (unreviewed) suggestion. Each item returns the transaction ID, merchant/description, amount, current category/sub-category (if any), confidence, classification method, and review status. Use this to answer 'what transactions need categorising' or to drive a bulk clean-up flow.")]
    public async Task<IReadOnlyList<ClassificationReviewItemResponse>> ListClassificationReviewQueue(
        [Description("Optional: scope to a specific personal account ID")] Guid? personalAccountId = null,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Page size (default: 50, max: 200)")] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var request = new ClassificationReviewQueueRequest(personalAccountId, page, pageSize);
        return await _classificationService.GetReviewQueueAsync(request, cancellationToken);
    }

    // ── Import & Attachment Read Tools ────────────────────────────

    [Description("Lists the user's CSV/OFX statement imports. Each entry shows filename, target account, format, status ('Uploaded', 'Parsed', 'Applied', 'Failed'), and row counts (total/parsed/imported/duplicate/failed). Use this to answer 'how did my import go', 'did my statement upload work', or to find an import ID before previewing or applying it. File uploads happen via the frontend — this tool only lists existing imports.")]
    public async Task<IReadOnlyList<StatementImportResponse>> ListStatementImports(
        CancellationToken cancellationToken = default)
    {
        return await _statementImportService.ListImportsAsync(cancellationToken);
    }

    [Description("Lists the parsed rows of a specific statement import so the user can preview what will be created before applying. Each row returns parse status ('Parsed', 'Duplicate', 'Failed'), raw and normalised values (date, amount, description, merchant, currency), and any error messages. Use this before calling pf_apply_statement_import so the user can confirm the import is sane.")]
    public async Task<IReadOnlyList<StatementImportRowResponse>> ListStatementImportRows(
        [Description("The unique identifier (GUID) of the statement import to preview")] Guid statementImportId,
        CancellationToken cancellationToken = default)
    {
        return await _statementImportService.ListImportRowsAsync(statementImportId, cancellationToken);
    }

    [Description("Lists the receipt/document attachments linked to a specific transaction. Returns file name, mime type, signed URL, optional thumbnail URL, size, and upload time for each attachment. Use this for 'show me the receipt for transaction X' or to check whether a transaction already has proof attached.")]
    public async Task<IReadOnlyList<TransactionAttachmentResponse>> ListTransactionAttachments(
        [Description("The unique identifier (GUID) of the transaction")] Guid transactionId,
        CancellationToken cancellationToken = default)
    {
        return await _attachmentService.GetAttachmentsAsync(transactionId, cancellationToken);
    }

    // ── Customer Insight Snapshot Read Tools ──────────────────────

    [Description("Lists historical customer insight snapshots for the current user, most recent first. Each entry is a lightweight summary: SnapshotId, Status (Current/Superseded/Failed), AsOfUtc (when it was generated), WindowStartUtc and WindowEndUtc (the 30-day analysis window it covers), Version, and IsPartial. Use this to discover which periods are available for multi-period spending comparisons, then call pf_compare_snapshots with 2-6 SnapshotIds.")]
    public async Task<IReadOnlyList<CustomerInsightSnapshotHistoryItemResponse>> ListSnapshotHistory(
        [Description("Maximum number of historical snapshots to return. Defaults to 12 (covers ~12 monthly snapshots). Maximum 50.")] int take = 12,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserProvider.GetCurrentUserId()
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

        var userId = _currentUserProvider.GetCurrentUserId()
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

    // ── Commitment Mutating Tools ───────────────────────────────

    [Description("Promotes a personal transaction into a tracked recurring commitment. Creates a PersonalRecurringBill, Subscription, or DebtRepayment based on the specified type.")]
    public async Task<CommitmentDetail> CreateCommitmentFromTransaction(
        [Description("The transaction ID (GUID) to promote")] Guid transactionId,
        [Description("Commitment type: 'Bill', 'Subscription', or 'DebtRepayment'")] string commitmentType,
        [Description("Display name for the commitment (e.g. payee or merchant name)")] string displayName,
        [Description("Billing frequency: 'Monthly', 'Weekly', 'Yearly', 'Quarterly'")] string frequency,
        [Description("Next expected due date in UTC")] DateTime nextDueDate,
        [Description("ISO 4217 currency code (e.g. USD, GBP, NGN)")] string currency,
        [Description("Expected recurring amount")] decimal? expectedAmount = null,
        [Description("Whether this commitment is on autopay")] bool autopay = false,
        [Description("Optional: account ID payments come from")] Guid? paidFromAccountId = null,
        [Description("Optional: free-text notes")] string? notes = null,
        [Description("Optional: debt type for DebtRepayment (e.g. 'Mortgage', 'PersonalLoan', 'CreditCardRepayment')")] string? debtType = null,
        [Description("Optional: external account or loan reference")] string? accountReference = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateCommitmentFromTransactionRequest(
            transactionId, commitmentType, displayName, frequency,
            nextDueDate, expectedAmount, currency, paidFromAccountId,
            autopay, notes, debtType, accountReference);
        return await _commitmentService.CreateFromTransactionAsync(request, cancellationToken);
    }

    [Description("Confirms a detected commitment, marking it as verified by the user. Only works on commitments with VerificationStatus = 'Detected'.")]
    public async Task<CommitmentDetail> ConfirmCommitment(
        [Description("The unique identifier (GUID) of the detected commitment to confirm")] Guid commitmentId,
        CancellationToken cancellationToken = default)
    {
        return await _commitmentService.ConfirmDetectedAsync(commitmentId, cancellationToken);
    }

    [Description("Rejects a detected commitment, indicating it is not a real recurring obligation. Only works on commitments with VerificationStatus = 'Detected'.")]
    public async Task<string> RejectCommitment(
        [Description("The unique identifier (GUID) of the detected commitment to reject")] Guid commitmentId,
        [Description("Optional reason for rejection")] string? reason = null,
        CancellationToken cancellationToken = default)
    {
        await _commitmentService.RejectDetectedAsync(commitmentId, reason, cancellationToken);
        return $"Commitment {commitmentId} has been rejected.";
    }

    // ── Classification Mutating Tools ─────────────────────────────

    [Description("Manually sets the category for a specific transaction (a confident, user-authored correction — not an AI guess). Clears any pending review status and locks in confidence. Optionally also creates a categorisation rule from this correction so future similar transactions are auto-classified. Requires confirmAction approval.")]
    public async Task<ClassificationReviewItemResponse> OverrideTransactionCategory(
        [Description("The unique identifier (GUID) of the transaction to recategorise")] Guid transactionId,
        [Description("The correct category to apply (e.g. 'Groceries', 'Eating Out', 'Transport')")] string category,
        [Description("Optional free-text notes about the correction")] string? notes = null,
        [Description("If true, also create a rule from this correction so future matching transactions auto-classify (default: false)")] bool createRuleFromCorrection = false,
        CancellationToken cancellationToken = default)
    {
        var request = new OverrideTransactionClassificationRequest(
            Category: category,
            Notes: notes,
            CreateRuleFromCorrection: createRuleFromCorrection,
            RulePattern: null,
            RulePriority: 100,
            RuleMatchType: "contains");
        return await _classificationService.OverrideClassificationAsync(transactionId, request, cancellationToken);
    }

    [Description("Creates a personal categorisation rule that auto-classifies future transactions matching a text pattern. Does NOT retroactively reclassify existing transactions. Requires confirmAction approval.")]
    public async Task<CategorisationRuleResponse> CreateCategorisationRule(
        [Description("Text pattern to match against the transaction's merchant, description, or notes (e.g. 'tesco', 'uber')")] string pattern,
        [Description("Target category to assign when the rule matches (e.g. 'Groceries', 'Transport')")] string category,
        [Description("Match mode: 'contains' (default), 'exact', 'startswith', 'endswith', 'regex', 'amount_range'")] string matchType = "contains",
        [Description("Whether pattern matching is case-sensitive (default: false)")] bool caseSensitive = false,
        [Description("Optional sub-category refinement")] string? subCategory = null,
        [Description("Rule priority — higher values evaluate first (default: 100)")] int priority = 100,
        [Description("Optional: only apply when transaction amount is at or above this value")] decimal? minAmount = null,
        [Description("Optional: only apply when transaction amount is at or below this value")] decimal? maxAmount = null,
        [Description("Optional: scope the rule to a specific personal account ID (null = all accounts)")] Guid? appliesToAccountId = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateCategorisationRuleRequest(
            Pattern: pattern,
            Category: category,
            SubCategory: subCategory,
            Priority: priority,
            MatchType: matchType,
            CaseSensitive: caseSensitive,
            MinAmount: minAmount,
            MaxAmount: maxAmount,
            AppliesToAccountId: appliesToAccountId,
            Scope: "User");
        return await _classificationService.CreateRuleAsync(request, cancellationToken);
    }

    // ── Import & Attachment Mutating Tools ────────────────────────

    [Description("Commits the parsed rows of a statement import as real personal transactions. Skips duplicates and failed rows automatically. Only works on imports with status 'Parsed' — for 'Uploaded' or 'Failed' imports, tell the user what's wrong instead. Returns final counts and the applied status. Requires confirmAction approval; before calling, preview rows with pf_list_statement_import_rows and summarise totals/duplicates/failures for the user.")]
    public async Task<StatementImportApplyResponse> ApplyStatementImport(
        [Description("The unique identifier (GUID) of the parsed statement import to commit")] Guid statementImportId,
        CancellationToken cancellationToken = default)
    {
        return await _statementImportService.ApplyImportAsync(statementImportId, cancellationToken);
    }

    [Description("Permanently removes a receipt/document attachment from a transaction. The file is deleted from blob storage. Requires confirmAction approval — name the file and the associated transaction in the confirmation summary.")]
    public async Task<string> DeleteTransactionAttachment(
        [Description("The unique identifier (GUID) of the attachment to delete")] Guid attachmentId,
        CancellationToken cancellationToken = default)
    {
        await _attachmentService.DeleteAttachmentAsync(attachmentId, cancellationToken);
        return $"Attachment {attachmentId} has been deleted.";
    }

    // ── Order Read Tools ──────────────────────────────────────────

    [Description("Lists the current user's payment orders (bill payments, transfers, remittances), most recent first. Returns compact summaries — do NOT dump full payloads on the user; use this to answer questions like 'what's the status of my recent payments', 'did my transfer go through', or 'show me pending orders'. Filter by status ('Draft', 'Submitted', 'Processing', 'Completed', 'Settled', 'Cancelled', 'Failed') or orderType ('BillPayment', 'Transfer'). Results are automatically scoped to the current user's party — orders belonging to other users in the tenant are never returned.")]
    public async Task<IReadOnlyList<OrderSummary>> ListOrders(
        [Description("Optional order status filter. Examples: 'Submitted', 'Processing', 'Completed', 'Cancelled', 'Failed'.")] string? status = null,
        [Description("Optional order type filter. Examples: 'BillPayment', 'Transfer'.")] string? orderType = null,
        [Description("Page size (1-50). Defaults to 20.")] int pageSize = 20,
        [Description("Page number (1-based). Defaults to 1.")] int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var partyId = await ResolveCurrentPartyIdAsync(cancellationToken);
        if (partyId is null)
        {
            return Array.Empty<OrderSummary>();
        }

        var limit = Math.Clamp(pageSize, 1, 50);
        var page = pageNumber < 1 ? 1 : pageNumber;

        var result = await _orderService.ListOrdersAsync(
            new ListOrdersRequest(
                PageNumber: page,
                PageSize: limit,
                Status: status,
                OrderType: orderType,
                Search: null,
                PayerPartyId: partyId),
            cancellationToken);

        return result.Items
            .Select(item => new OrderSummary(
                OrderId: item.OrderId,
                OrderType: item.OrderType,
                Status: item.Status,
                OriginCurrency: item.OriginCurrency,
                TotalAmountIn: item.TotalAmountIn,
                DestinationCurrency: item.DestinationCurrency,
                TotalAmountOut: item.TotalAmountOut,
                CreatedAt: item.CreatedAt,
                UpdatedAt: item.UpdatedAt))
            .ToArray();
    }

    [Description("Retrieves the summary of a single order by its unique identifier — compact shape only (status, amounts, item count, top receiver). Use this when the user asks about a specific order; do not dump the full payload. Ownership is verified: only returns data when the order belongs to the current user's party.")]
    public async Task<OrderDetailSummary?> GetOrder(
        [Description("The unique identifier (GUID) of the order")] Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var partyId = await ResolveCurrentPartyIdAsync(cancellationToken);
        if (partyId is null)
        {
            return null;
        }

        BillPaymentOrderResponse order;
        try
        {
            order = await _orderService.GetOrderAsync(orderId, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        if (order.PayerPartyId != partyId.Value)
        {
            return null;
        }

        var firstItem = order.Items.OrderBy(item => item.ItemIndex).FirstOrDefault();

        return new OrderDetailSummary(
            OrderId: order.OrderId,
            OrderType: order.OrderType,
            Status: order.Status,
            OriginCountry: order.OriginCountry,
            OriginCurrency: order.OriginCurrency,
            TotalAmountIn: order.TotalAmountIn,
            TotalFeesAmount: order.TotalFeesAmount,
            TotalAmountOut: order.TotalAmountOut,
            DestinationCurrency: order.DestinationCurrency,
            PurposeCode: order.PurposeCode,
            ItemCount: order.Items.Count,
            PrimaryReceiverName: firstItem?.ReceiverName,
            PrimaryBillerName: firstItem?.BillerName,
            CreatedAt: order.CreatedAt,
            SubmittedAt: order.SubmittedAt);
    }

    // ── Order Mutating Tools ──────────────────────────────────────

    [Description("Cancels a payment order that has not yet settled. No-op for orders already in 'Cancelled', 'Completed', or 'Failed' state (returns the current summary). Ownership is verified before cancellation. Requires confirmAction approval — in the confirmation summary include order type, recipient/biller, amount, and the reason.")]
    public async Task<OrderDetailSummary> CancelOrder(
        [Description("The unique identifier (GUID) of the order to cancel")] Guid orderId,
        [Description("Optional reason for cancellation, e.g. 'User requested cancellation' or 'Wrong amount'.")] string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var partyId = await ResolveCurrentPartyIdAsync(cancellationToken)
            ?? throw new InvalidOperationException("Current user is not linked to a party and cannot cancel orders.");

        var existing = await _orderService.GetOrderAsync(orderId, cancellationToken);
        if (existing.PayerPartyId != partyId)
        {
            throw new InvalidOperationException($"Order {orderId} not found.");
        }

        var cancelled = await _orderService.CancelOrderAsync(orderId, reason, cancellationToken);

        var firstItem = cancelled.Items.OrderBy(item => item.ItemIndex).FirstOrDefault();
        return new OrderDetailSummary(
            OrderId: cancelled.OrderId,
            OrderType: cancelled.OrderType,
            Status: cancelled.Status,
            OriginCountry: cancelled.OriginCountry,
            OriginCurrency: cancelled.OriginCurrency,
            TotalAmountIn: cancelled.TotalAmountIn,
            TotalFeesAmount: cancelled.TotalFeesAmount,
            TotalAmountOut: cancelled.TotalAmountOut,
            DestinationCurrency: cancelled.DestinationCurrency,
            PurposeCode: cancelled.PurposeCode,
            ItemCount: cancelled.Items.Count,
            PrimaryReceiverName: firstItem?.ReceiverName,
            PrimaryBillerName: firstItem?.BillerName,
            CreatedAt: cancelled.CreatedAt,
            SubmittedAt: cancelled.SubmittedAt);
    }

    private async Task<Guid?> ResolveCurrentPartyIdAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUserProvider.GetCurrentUserId();
        if (userId is null)
        {
            return null;
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        return await _financeDbContext.UserParties
            .AsNoTracking()
            .Where(link => link.TenantId == tenantId && link.UserId == userId.Value)
            .OrderByDescending(link => link.Id)
            .Select(link => (Guid?)link.PartyId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    // ── Tool Factory ──────────────────────────────────────────────

    /// <summary>
    /// Creates <see cref="AITool"/> instances for all personal finance tools.
    /// Mutating tools (CreateAccount, ArchiveAccount, CreateManualTransaction,
    /// CreateBill, UpdateBill, ArchiveBill, CreateBudget, UpdateBudgetAmount,
    /// DeleteBudget, CreateCommitmentFromTransaction, ConfirmCommitment,
    /// RejectCommitment, OverrideTransactionCategory, CreateCategorisationRule,
    /// ApplyStatementImport, DeleteTransactionAttachment, CancelOrder) are gated
    /// server-side by the <c>IToolApprovalGate</c> (Spec 032), classified by
    /// PersonalFinanceToolApprovalManifest (all Medium/Low — PersonalFinance moves no money).
    /// </summary>
    public static IEnumerable<AITool> CreateAll(IServiceProvider serviceProvider)
    {
        var tools = new PersonalFinanceTools(
            serviceProvider.GetRequiredService<IPersonalAccountService>(),
            serviceProvider.GetRequiredService<IPersonalTransactionService>(),
            serviceProvider.GetRequiredService<IBillService>(),
            serviceProvider.GetRequiredService<IBudgetService>(),
            serviceProvider.GetRequiredService<ICommitmentService>(),
            serviceProvider.GetRequiredService<IPersonalFinanceInsightsService>(),
            serviceProvider.GetRequiredService<IDashboardService>(),
            serviceProvider.GetRequiredService<IFxRateService>(),
            serviceProvider.GetRequiredService<ITransactionClassificationService>(),
            serviceProvider.GetRequiredService<IStatementImportService>(),
            serviceProvider.GetRequiredService<ITransactionAttachmentService>(),
            serviceProvider.GetRequiredService<ICustomerInsightSnapshotReader>(),
            serviceProvider.GetRequiredService<IOrderService>(),
            serviceProvider.GetRequiredService<FinanceDbContext>(),
            serviceProvider.GetRequiredService<ITenantProvider>(),
            serviceProvider.GetRequiredService<ICurrentUserProvider>(),
            serviceProvider.GetRequiredService<IChatClient>(),
            serviceProvider,
            serviceProvider.GetRequiredService<IAgentConfigurationService>());

        // Read-only — safe for autonomous use
        yield return AIFunctionFactory.Create(tools.ListAccounts, name: "pf_list_accounts");
        yield return AIFunctionFactory.Create(tools.GetAccount, name: "pf_get_account");
        yield return AIFunctionFactory.Create(tools.ListTransactions, name: "pf_list_transactions");
        yield return AIFunctionFactory.Create(tools.GetTransaction, name: "pf_get_transaction");
        yield return AIFunctionFactory.Create(tools.ListBills, name: "pf_list_bills");
        yield return AIFunctionFactory.Create(tools.GetBill, name: "pf_get_bill");
        yield return AIFunctionFactory.Create(tools.GetUpcomingBills, name: "pf_get_upcoming_bills");
        yield return AIFunctionFactory.Create(tools.ListBudgets, name: "pf_list_budgets");
        yield return AIFunctionFactory.Create(tools.GetSpendingSummary, name: "pf_get_spending_summary");
        yield return AIFunctionFactory.Create(tools.GetCategoryBreakdown, name: "pf_get_category_breakdown");
        yield return AIFunctionFactory.Create(tools.GetMerchantBreakdown, name: "pf_get_merchant_breakdown");
        yield return AIFunctionFactory.Create(tools.GetAccountBreakdown, name: "pf_get_account_breakdown");
        yield return AIFunctionFactory.Create(tools.GetMerchantHistory, name: "pf_get_merchant_history");
        yield return AIFunctionFactory.Create(tools.GetDashboard, name: "pf_get_dashboard");
        yield return AIFunctionFactory.Create(tools.GetFxRateHistory, name: "pf_get_fx_rate_history");

        // Spec 025 §5 — three sub-agent triggers replace the legacy
        // pf_run_spending_intelligence / pf_run_obligation_planning pair.
        // The legacy descriptors stay registered in DI but no longer appear
        // in Simi's tool catalogue (Phase 6 removes them entirely).
        yield return AIFunctionFactory.Create(tools.RunInsights, name: "pf_run_insights");
        yield return AIFunctionFactory.Create(tools.RunForecast, name: "pf_run_forecast");
        yield return AIFunctionFactory.Create(tools.RunClassifyReview, name: "pf_run_classify_review");

        yield return AIFunctionFactory.Create(tools.ListCommitments, name: "pf_list_commitments");
        yield return AIFunctionFactory.Create(tools.GetCommitment, name: "pf_get_commitment");
        yield return AIFunctionFactory.Create(tools.ListDetectedCommitments, name: "pf_list_detected_commitments");
        yield return AIFunctionFactory.Create(tools.ListClassificationReviewQueue, name: "pf_list_classification_review_queue");
        yield return AIFunctionFactory.Create(tools.ListStatementImports, name: "pf_list_statement_imports");
        yield return AIFunctionFactory.Create(tools.ListStatementImportRows, name: "pf_list_statement_import_rows");
        yield return AIFunctionFactory.Create(tools.ListTransactionAttachments, name: "pf_list_transaction_attachments");
        yield return AIFunctionFactory.Create(tools.ListSnapshotHistory, name: "pf_list_snapshot_history");
        yield return AIFunctionFactory.Create(tools.CompareSnapshots, name: "pf_compare_snapshots");
        yield return AIFunctionFactory.Create(tools.ListOrders, name: "pf_list_orders");
        yield return AIFunctionFactory.Create(tools.GetOrder, name: "pf_get_order");

        // Mutating — gated server-side by the IToolApprovalGate (PersonalFinanceToolApprovalManifest)
        yield return AIFunctionFactory.Create(tools.CreateAccount, name: "pf_create_account");
        yield return AIFunctionFactory.Create(tools.ArchiveAccount, name: "pf_archive_account");
        yield return AIFunctionFactory.Create(tools.CreateManualTransaction, name: "pf_create_transaction");
        yield return AIFunctionFactory.Create(tools.CreateBill, name: "pf_create_bill");
        yield return AIFunctionFactory.Create(tools.UpdateBill, name: "pf_update_bill");
        yield return AIFunctionFactory.Create(tools.ArchiveBill, name: "pf_archive_bill");
        yield return AIFunctionFactory.Create(tools.CreateBudget, name: "pf_create_budget");
        yield return AIFunctionFactory.Create(tools.UpdateBudgetAmount, name: "pf_update_budget_amount");
        yield return AIFunctionFactory.Create(tools.DeleteBudget, name: "pf_delete_budget");
        yield return AIFunctionFactory.Create(tools.CreateCommitmentFromTransaction, name: "pf_create_commitment_from_transaction");
        yield return AIFunctionFactory.Create(tools.ConfirmCommitment, name: "pf_confirm_commitment");
        yield return AIFunctionFactory.Create(tools.RejectCommitment, name: "pf_reject_commitment");
        yield return AIFunctionFactory.Create(tools.OverrideTransactionCategory, name: "pf_override_transaction_category");
        yield return AIFunctionFactory.Create(tools.CreateCategorisationRule, name: "pf_create_categorisation_rule");
        yield return AIFunctionFactory.Create(tools.ApplyStatementImport, name: "pf_apply_statement_import");
        yield return AIFunctionFactory.Create(tools.DeleteTransactionAttachment, name: "pf_delete_transaction_attachment");
        yield return AIFunctionFactory.Create(tools.CancelOrder, name: "pf_cancel_order");
    }

    // ── Per-Sub-Agent Read-Only Tool Slices (Spec 025) ───────────
    //
    // These slices feed the three CodeAct-powered analytical sub-agents
    // introduced in `docs/specifications/025.personal-finance-agent-split-and-codeact.html`.
    // Each whitelist is pure read-only: mutations stay on Simi's direct
    // surface, where the server-side approval gate (Spec 032) gates every
    // change (CodeAct's whole-block approval semantics therefore never
    // trigger inside a sub-agent sandbox).
    //
    // Tool definitions and `[Description]` strings remain authored once in
    // this class — the slice methods just filter `CreateAll` by name so the
    // sub-agent whitelists can never drift from Simi's catalogue.

    private static readonly HashSet<string> InsightsSubAgentToolNames = new(StringComparer.Ordinal)
    {
        // Spec 025 §5.1 — explain / audit / rank.
        "pf_get_category_breakdown",
        "pf_get_merchant_breakdown",
        "pf_get_account_breakdown",
        "pf_get_merchant_history",
        "pf_list_transactions",
        "pf_get_transaction",
        "pf_list_commitments",
        "pf_get_commitment",
        "pf_list_detected_commitments",
        "pf_list_snapshot_history",
        "pf_compare_snapshots",
        "pf_get_spending_summary",
        "pf_get_upcoming_bills",
    };

    private static readonly HashSet<string> ForecastSubAgentToolNames = new(StringComparer.Ordinal)
    {
        // Spec 025 §5.2 — projections / what-if / scenarios.
        "pf_get_dashboard",
        "pf_get_spending_summary",
        "pf_get_upcoming_bills",
        "pf_list_commitments",
        "pf_list_budgets",
        "pf_list_snapshot_history",
        "pf_compare_snapshots",
        "pf_get_fx_rate_history",
    };

    private static readonly HashSet<string> ClassifySubAgentToolNames = new(StringComparer.Ordinal)
    {
        // Spec 025 §5.3 — categorisation queue review at scale.
        "pf_list_classification_review_queue",
        "pf_get_transaction",
        "pf_list_transactions",
        "pf_get_merchant_history",
        "pf_get_category_breakdown",
    };

    /// <summary>
    /// Read-only tool slice for the <c>pf-insights</c> sub-agent (Spec 025 §5.1).
    /// Composes data-fetching operations for explain/audit/rank questions over
    /// the user's historical spending and commitments. Never exposes mutating
    /// tools — sub-agents are read-only by design.
    /// </summary>
    public static IEnumerable<AITool> CreateForInsightsSubAgent(IServiceProvider serviceProvider)
        => CreateAll(serviceProvider).Where(tool => InsightsSubAgentToolNames.Contains(tool.Name));

    /// <summary>
    /// Read-only tool slice for the <c>pf-forecast</c> sub-agent (Spec 025 §5.2).
    /// Composes data-fetching operations for forward projections and what-if
    /// scenarios. Never exposes mutating tools.
    /// </summary>
    public static IEnumerable<AITool> CreateForForecastSubAgent(IServiceProvider serviceProvider)
        => CreateAll(serviceProvider).Where(tool => ForecastSubAgentToolNames.Contains(tool.Name));

    /// <summary>
    /// Read-only tool slice for the <c>pf-classify</c> sub-agent (Spec 025 §5.3).
    /// Composes data-fetching operations for walking the classification review
    /// queue and proposing per-item corrections. Never exposes mutating tools —
    /// Simi handles the per-action `pf_override_transaction_category` and
    /// `pf_create_categorisation_rule` calls via the existing `confirmAction`
    /// flow after the sub-agent has surfaced proposals.
    /// </summary>
    public static IEnumerable<AITool> CreateForClassifySubAgent(IServiceProvider serviceProvider)
        => CreateAll(serviceProvider).Where(tool => ClassifySubAgentToolNames.Contains(tool.Name));

    private async Task<ChatClientAgent> BuildStructuredSubAgentAsync(
        IDomainAgentDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        var config = await _agentConfigurationService.GetResolvedAsync(descriptor.Name, cancellationToken);

        string? instructionsOverride = null;
        HashSet<string>? allowedToolNames = null;

        if (config is not null)
        {
            instructionsOverride = !string.IsNullOrWhiteSpace(config.InstructionsText)
                ? config.InstructionsText
                : null;

            if (!string.IsNullOrWhiteSpace(config.ToolsetIdsJson) && config.ToolsetIdsJson != "[]")
            {
                try
                {
                    var toolNames = JsonSerializer.Deserialize<List<string>>(config.ToolsetIdsJson);
                    if (toolNames is { Count: > 0 })
                    {
                        allowedToolNames = new HashSet<string>(toolNames, StringComparer.Ordinal);
                    }
                }
                catch (JsonException)
                {
                    allowedToolNames = null;
                }
            }
        }

        var builtAgent = config is null
            ? descriptor.Build(_chatClient, _serviceProvider)
            : descriptor.Build(_chatClient, _serviceProvider, instructionsOverride, allowedToolNames);

        return builtAgent as ChatClientAgent
            ?? throw new InvalidOperationException($"The agent '{descriptor.Name}' must be a ChatClientAgent.");
    }
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

// ── Order summary DTOs ────────────────────────────────────────
//
// Compact shapes for pf_list_orders / pf_get_order / pf_cancel_order.
// The full BillPaymentOrderResponse is large (items, service fields,
// pricing snapshots, party roles, history) — these keep LLM output
// small and force summary-oriented user messages.

public record OrderSummary(
    Guid OrderId,
    string OrderType,
    string Status,
    string OriginCurrency,
    decimal TotalAmountIn,
    string? DestinationCurrency,
    decimal? TotalAmountOut,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record OrderDetailSummary(
    Guid OrderId,
    string OrderType,
    string Status,
    string OriginCountry,
    string OriginCurrency,
    decimal TotalAmountIn,
    decimal TotalFeesAmount,
    decimal TotalAmountOut,
    string? DestinationCurrency,
    string? PurposeCode,
    int ItemCount,
    string? PrimaryReceiverName,
    string? PrimaryBillerName,
    DateTime CreatedAt,
    DateTime? SubmittedAt);
