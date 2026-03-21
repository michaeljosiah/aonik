using System.ComponentModel;
using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Finance.Agents.Tools;

/// <summary>
/// AI agent tools for personal finance operations.
/// Each method is exposed to the LLM via <see cref="AIFunctionFactory.Create"/>.
/// Read-only tools are safe for autonomous use; mutating tools are wrapped with
/// <see cref="ApprovalRequiredAIFunction"/> to enforce human-in-the-loop approval.
/// </summary>
internal sealed class PersonalFinanceTools
{
    private readonly IPersonalAccountService _accountService;
    private readonly IPersonalTransactionService _transactionService;
    private readonly IBillService _billService;
    private readonly IPersonalFinanceInsightsService _insightsService;
    private readonly IDashboardService _dashboardService;

    private PersonalFinanceTools(
        IPersonalAccountService accountService,
        IPersonalTransactionService transactionService,
        IBillService billService,
        IPersonalFinanceInsightsService insightsService,
        IDashboardService dashboardService)
    {
        _accountService = accountService;
        _transactionService = transactionService;
        _billService = billService;
        _insightsService = insightsService;
        _dashboardService = dashboardService;
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

    [Description("Lists personal transactions with optional filters. Supports filtering by date range, account, category, and free-text search. Results are paginated.")]
    public async Task<IReadOnlyList<PersonalTransactionResponse>> ListTransactions(
        [Description("Start date filter (UTC, inclusive)")] DateTime? from = null,
        [Description("End date filter (UTC, inclusive)")] DateTime? to = null,
        [Description("Filter by personal account ID")] Guid? personalAccountId = null,
        [Description("Filter by category name")] string? category = null,
        [Description("Free-text search in merchant/description")] string? search = null,
        [Description("Page number (default: 1)")] int page = 1,
        [Description("Page size (default: 50, max: 100)")] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var request = new ListPersonalTransactionsRequest(from, to, personalAccountId, category, search, page, pageSize);
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

    // ── Tool Factory ──────────────────────────────────────────────

    /// <summary>
    /// Creates <see cref="AITool"/> instances for all personal finance tools.
    /// Mutating tools (CreateAccount, ArchiveAccount, CreateManualTransaction,
    /// CreateBill, ArchiveBill) are wrapped with <see cref="ApprovalRequiredAIFunction"/>
    /// for human-in-the-loop approval.
    /// </summary>
    public static IEnumerable<AITool> CreateAll(IServiceProvider serviceProvider)
    {
        var tools = new PersonalFinanceTools(
            serviceProvider.GetRequiredService<IPersonalAccountService>(),
            serviceProvider.GetRequiredService<IPersonalTransactionService>(),
            serviceProvider.GetRequiredService<IBillService>(),
            serviceProvider.GetRequiredService<IPersonalFinanceInsightsService>(),
            serviceProvider.GetRequiredService<IDashboardService>());

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

        // Mutating — require approval before execution
        yield return new ApprovalRequiredAIFunction(
            AIFunctionFactory.Create(tools.CreateAccount, name: "pf_create_account"));
        yield return new ApprovalRequiredAIFunction(
            AIFunctionFactory.Create(tools.ArchiveAccount, name: "pf_archive_account"));
        yield return new ApprovalRequiredAIFunction(
            AIFunctionFactory.Create(tools.CreateManualTransaction, name: "pf_create_transaction"));
        yield return new ApprovalRequiredAIFunction(
            AIFunctionFactory.Create(tools.CreateBill, name: "pf_create_bill"));
        yield return new ApprovalRequiredAIFunction(
            AIFunctionFactory.Create(tools.ArchiveBill, name: "pf_archive_bill"));
    }
}
