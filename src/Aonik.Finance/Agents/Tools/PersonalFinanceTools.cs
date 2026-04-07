using System.ComponentModel;
using System.Text.Json;
using Aonik.Agents.Contracts.Services;
using Aonik.Finance.Agents.StructuredOutputs;
using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Finance.Contracts.Services.Pricing;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

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
    private readonly ICommitmentService _commitmentService;
    private readonly IPersonalFinanceInsightsService _insightsService;
    private readonly IDashboardService _dashboardService;
    private readonly IFxRateService _fxRateService;
    private readonly IChatClient _chatClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly IAgentConfigurationService _agentConfigurationService;
    private readonly IDomainAgentDescriptor? _spendingIntelligenceDescriptor;
    private readonly IDomainAgentDescriptor? _obligationPlanningDescriptor;

    private PersonalFinanceTools(
        IPersonalAccountService accountService,
        IPersonalTransactionService transactionService,
        IBillService billService,
        ICommitmentService commitmentService,
        IPersonalFinanceInsightsService insightsService,
        IDashboardService dashboardService,
        IFxRateService fxRateService,
        IChatClient chatClient,
        IServiceProvider serviceProvider,
        IAgentConfigurationService agentConfigurationService,
        IDomainAgentDescriptor? spendingIntelligenceDescriptor,
        IDomainAgentDescriptor? obligationPlanningDescriptor)
    {
        _accountService = accountService;
        _transactionService = transactionService;
        _billService = billService;
        _commitmentService = commitmentService;
        _insightsService = insightsService;
        _dashboardService = dashboardService;
        _fxRateService = fxRateService;
        _chatClient = chatClient;
        _serviceProvider = serviceProvider;
        _agentConfigurationService = agentConfigurationService;
        _spendingIntelligenceDescriptor = spendingIntelligenceDescriptor;
        _obligationPlanningDescriptor = obligationPlanningDescriptor;
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

    [Description("Gets spending broken down by category for a given period. Returns each category's total amount and percentage of overall spending.")]
    public async Task<IReadOnlyList<CategorySpendingItemResponse>> GetCategoryBreakdown(
        [Description("Start of the analysis period (UTC)")] DateTime periodStart,
        [Description("End of the analysis period (UTC)")] DateTime periodEnd,
        [Description("Optional: scope to a specific personal account ID")] Guid? personalAccountId = null,
        CancellationToken cancellationToken = default)
    {
        return await _insightsService.GetCategoryBreakdownAsync(periodStart, periodEnd, personalAccountId, cancellationToken);
    }

    [Description("Gets spending broken down by merchant for a given period. Returns the top merchants by total amount spent.")]
    public async Task<IReadOnlyList<MerchantSpendingItemResponse>> GetMerchantBreakdown(
        [Description("Start of the analysis period (UTC)")] DateTime periodStart,
        [Description("End of the analysis period (UTC)")] DateTime periodEnd,
        [Description("Optional: scope to a specific personal account ID")] Guid? personalAccountId = null,
        [Description("Number of top merchants to return (default: 10)")] int top = 10,
        CancellationToken cancellationToken = default)
    {
        return await _insightsService.GetMerchantBreakdownAsync(periodStart, periodEnd, personalAccountId, top, cancellationToken);
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

    // ── Sub-Agent Tools ──────────────────────────────────────────

    [Description("Runs the internal spending-intelligence specialist and returns schema-bound analysis JSON plus the parsed structured result. Use this for reasoning-heavy questions about spending patterns, budget pressure, and where the user should focus first.")]
    public async Task<SpendingIntelligenceAgentToolResponse> RunSpendingIntelligence(
        [Description("The user question or planning goal that needs analysis")] string userQuestion,
        [Description("Start of the analysis period (UTC)")] DateTime periodStart,
        [Description("End of the analysis period (UTC)")] DateTime periodEnd,
        [Description("Optional account ID to scope the analysis to")] Guid? personalAccountId = null,
        [Description("Whether to include the narrative insight")] bool includeNarrative = true,
        [Description("Whether to include snapshot-backed signals")] bool includeSnapshotSignals = true,
        [Description("Whether to include budget pressure signals")] bool includeBudgetSignals = true,
        CancellationToken cancellationToken = default)
    {
        if (_spendingIntelligenceDescriptor is null)
            throw new InvalidOperationException("The spending intelligence agent is not registered.");

        var agent = await BuildStructuredSubAgentAsync(
            _spendingIntelligenceDescriptor,
            cancellationToken);

        var request = new SpendingIntelligenceRequest(
            userQuestion,
            periodStart,
            periodEnd,
            personalAccountId,
            includeNarrative,
            includeSnapshotSignals,
            includeBudgetSignals);

        var message = JsonSerializer.Serialize(request, SpendingIntelligenceStructuredOutputContract.SerializerOptions);
        var response = await agent.RunAsync<SpendingIntelligenceResult>(
            message,
            session: null,
            serializerOptions: SpendingIntelligenceStructuredOutputContract.SerializerOptions,
            options: null,
            cancellationToken: cancellationToken);

        var analysis = response.Result;
        var analysisJson = JsonSerializer.Serialize(
            analysis,
            SpendingIntelligenceStructuredOutputContract.SerializerOptions);

        return new SpendingIntelligenceAgentToolResponse(analysis, analysisJson);
    }

    [Description("Runs the internal obligation-planning specialist and returns schema-bound analysis for due-soon bills, recurring obligations, coverage pressure, and prioritised next steps.")]
    public async Task<ObligationPlanningAgentToolResponse> RunObligationPlanning(
        [Description("The user question or planning goal that needs analysis")] string userQuestion,
        [Description("Number of days ahead to inspect for obligations (default: 30)")] int withinDays = 30,
        [Description("Whether to include snapshot-backed coverage signals")] bool includeSnapshotSignals = true,
        [Description("Whether to include household context if available")] bool includeHouseholdContext = true,
        CancellationToken cancellationToken = default)
    {
        if (_obligationPlanningDescriptor is null)
            throw new InvalidOperationException("The obligation planning agent is not registered.");

        var agent = await BuildStructuredSubAgentAsync(
            _obligationPlanningDescriptor,
            cancellationToken);

        var request = new ObligationPlanningRequest(
            userQuestion,
            withinDays,
            includeSnapshotSignals,
            includeHouseholdContext);

        var message = JsonSerializer.Serialize(request, ObligationPlanningStructuredOutputContract.SerializerOptions);
        var response = await agent.RunAsync<ObligationPlanningResult>(
            message,
            session: null,
            serializerOptions: ObligationPlanningStructuredOutputContract.SerializerOptions,
            options: null,
            cancellationToken: cancellationToken);

        var analysis = response.Result;
        var analysisJson = JsonSerializer.Serialize(
            analysis,
            ObligationPlanningStructuredOutputContract.SerializerOptions);

        return new ObligationPlanningAgentToolResponse(analysis, analysisJson);
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

    [Description("Archives a bill, marking it as no longer active. The bill remains in the system for historical reference.")]
    public async Task<string> ArchiveBill(
        [Description("The unique identifier (GUID) of the bill to archive")] Guid billId,
        CancellationToken cancellationToken = default)
    {
        await _billService.ArchiveBillAsync(billId, cancellationToken);
        return $"Bill {billId} has been archived successfully.";
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

    // ── Tool Factory ──────────────────────────────────────────────

    /// <summary>
    /// Creates <see cref="AITool"/> instances for all personal finance tools.
    /// Mutating tools (CreateAccount, ArchiveAccount, CreateManualTransaction,
    /// CreateBill, ArchiveBill, CreateCommitmentFromTransaction, ConfirmCommitment,
    /// RejectCommitment) rely on the <c>confirmAction</c> frontend tool for
    /// human-in-the-loop approval.
    /// </summary>
    public static IEnumerable<AITool> CreateAll(IServiceProvider serviceProvider)
    {
        var descriptors = serviceProvider.GetServices<IDomainAgentDescriptor>();
        var tools = new PersonalFinanceTools(
            serviceProvider.GetRequiredService<IPersonalAccountService>(),
            serviceProvider.GetRequiredService<IPersonalTransactionService>(),
            serviceProvider.GetRequiredService<IBillService>(),
            serviceProvider.GetRequiredService<ICommitmentService>(),
            serviceProvider.GetRequiredService<IPersonalFinanceInsightsService>(),
            serviceProvider.GetRequiredService<IDashboardService>(),
            serviceProvider.GetRequiredService<IFxRateService>(),
            serviceProvider.GetRequiredService<IChatClient>(),
            serviceProvider,
            serviceProvider.GetRequiredService<IAgentConfigurationService>(),
            descriptors.FirstOrDefault(x => x.Name == "pf-spending-intelligence-agent"),
            descriptors.FirstOrDefault(x => x.Name == "pf-obligation-planning-agent"));

        // Read-only — safe for autonomous use
        yield return AIFunctionFactory.Create(tools.ListAccounts, name: "pf_list_accounts");
        yield return AIFunctionFactory.Create(tools.GetAccount, name: "pf_get_account");
        yield return AIFunctionFactory.Create(tools.ListTransactions, name: "pf_list_transactions");
        yield return AIFunctionFactory.Create(tools.GetTransaction, name: "pf_get_transaction");
        yield return AIFunctionFactory.Create(tools.ListBills, name: "pf_list_bills");
        yield return AIFunctionFactory.Create(tools.GetBill, name: "pf_get_bill");
        yield return AIFunctionFactory.Create(tools.GetUpcomingBills, name: "pf_get_upcoming_bills");
        yield return AIFunctionFactory.Create(tools.GetSpendingSummary, name: "pf_get_spending_summary");
        yield return AIFunctionFactory.Create(tools.GetCategoryBreakdown, name: "pf_get_category_breakdown");
        yield return AIFunctionFactory.Create(tools.GetMerchantBreakdown, name: "pf_get_merchant_breakdown");
        yield return AIFunctionFactory.Create(tools.GetDashboard, name: "pf_get_dashboard");
        yield return AIFunctionFactory.Create(tools.GetFxRateHistory, name: "pf_get_fx_rate_history");
        yield return AIFunctionFactory.Create(tools.RunSpendingIntelligence, name: "pf_run_spending_intelligence");
        yield return AIFunctionFactory.Create(tools.RunObligationPlanning, name: "pf_run_obligation_planning");
        yield return AIFunctionFactory.Create(tools.ListCommitments, name: "pf_list_commitments");
        yield return AIFunctionFactory.Create(tools.GetCommitment, name: "pf_get_commitment");
        yield return AIFunctionFactory.Create(tools.ListDetectedCommitments, name: "pf_list_detected_commitments");

        // Mutating — approval enforced via the confirmAction frontend tool
        yield return AIFunctionFactory.Create(tools.CreateAccount, name: "pf_create_account");
        yield return AIFunctionFactory.Create(tools.ArchiveAccount, name: "pf_archive_account");
        yield return AIFunctionFactory.Create(tools.CreateManualTransaction, name: "pf_create_transaction");
        yield return AIFunctionFactory.Create(tools.CreateBill, name: "pf_create_bill");
        yield return AIFunctionFactory.Create(tools.ArchiveBill, name: "pf_archive_bill");
        yield return AIFunctionFactory.Create(tools.CreateCommitmentFromTransaction, name: "pf_create_commitment_from_transaction");
        yield return AIFunctionFactory.Create(tools.ConfirmCommitment, name: "pf_confirm_commitment");
        yield return AIFunctionFactory.Create(tools.RejectCommitment, name: "pf_reject_commitment");
    }

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
