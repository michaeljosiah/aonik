using System.ComponentModel;
using Aonik.Finance.Contracts.Models.Ledger;
using Aonik.Finance.Contracts.Services.Ledger;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Finance.Agents.Tools;

/// <summary>
/// AI agent tools for ledger operations.
/// Read-only queries are safe for autonomous use; mutating tools (CreateLedger,
/// CreateAccount) rely on the <c>confirmAction</c> frontend tool for approval.
/// </summary>
internal sealed class LedgerTools
{
    private readonly ILedgerService _ledgerService;

    private LedgerTools(ILedgerService ledgerService) => _ledgerService = ledgerService;

    [Description("Lists all ledgers for the current tenant. Returns ledger IDs, base currencies, and creation dates.")]
    public async Task<IReadOnlyList<LedgerResponse>> ListLedgers(
        CancellationToken cancellationToken = default)
    {
        return await _ledgerService.ListLedgersAsync(cancellationToken);
    }

    [Description("Lists ledger accounts, optionally filtered by a specific ledger. Returns account names, codes, types, and currencies.")]
    public async Task<IReadOnlyList<LedgerAccountResponse>> ListAccounts(
        [Description("Optional ledger ID to filter accounts. Pass null to list accounts across all ledgers.")] Guid? ledgerId,
        CancellationToken cancellationToken = default)
    {
        var request = new ListLedgerAccountsRequest(ledgerId);
        return await _ledgerService.ListAccountsAsync(request, cancellationToken);
    }

    [Description("Lists journal entries, optionally filtered by a specific ledger. Returns the most recent entries (up to a server-side page limit) with dates, statuses, references, and debit/credit lines — not necessarily every entry.")]
    public async Task<IReadOnlyList<JournalEntryResponse>> ListJournalEntries(
        [Description("Optional ledger ID to filter journal entries. Pass null to list entries across all ledgers.")] Guid? ledgerId,
        CancellationToken cancellationToken = default)
    {
        var request = new ListJournalEntriesRequest(ledgerId);
        return await _ledgerService.ListJournalEntriesAsync(request, cancellationToken);
    }

    [Description("Creates a new ledger with a specified base currency. Returns the created ledger details.")]
    public async Task<LedgerResponse> CreateLedger(
        [Description("ISO 4217 currency code for the ledger's base currency (e.g. USD, NGN)")] string baseCurrency,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateLedgerRequest(baseCurrency);
        return await _ledgerService.CreateLedgerAsync(request, cancellationToken);
    }

    [Description("Creates a new account within a ledger. Returns the created account details.")]
    public async Task<LedgerAccountResponse> CreateAccount(
        [Description("The ledger ID to create the account in")] Guid ledgerId,
        [Description("Display name for the account")] string name,
        [Description("Unique account code (e.g. 1001, 2001)")] string code,
        [Description("Account type: Asset, Liability, Equity, Revenue, or Expense")] string accountType,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateLedgerAccountRequest(ledgerId, name, code, accountType);
        return await _ledgerService.CreateAccountAsync(request, cancellationToken);
    }

    /// <summary>
    /// Creates <see cref="AITool"/> instances for all ledger tools.
    /// Mutating tools (CreateLedger, CreateAccount) are wrapped with
    /// the <c>confirmAction</c> frontend tool for human-in-the-loop approval.
    /// </summary>
    public static IEnumerable<AITool> CreateAll(IServiceProvider serviceProvider)
    {
        var tools = new LedgerTools(serviceProvider.GetRequiredService<ILedgerService>());

        // Read-only — safe for autonomous use
        yield return AIFunctionFactory.Create(tools.ListLedgers, name: "finance_list_ledgers");
        yield return AIFunctionFactory.Create(tools.ListAccounts, name: "finance_list_accounts");
        yield return AIFunctionFactory.Create(tools.ListJournalEntries, name: "finance_list_journal_entries");

        // Mutating — approval enforced via the confirmAction frontend tool
        yield return AIFunctionFactory.Create(tools.CreateLedger, name: "finance_create_ledger");
        yield return AIFunctionFactory.Create(tools.CreateAccount, name: "finance_create_account");
    }
}
