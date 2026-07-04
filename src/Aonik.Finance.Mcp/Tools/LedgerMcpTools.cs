using System.ComponentModel;
using Aonik.Finance.Contracts.Models.Ledger;
using Aonik.Finance.Contracts.Services.Ledger;
using ModelContextProtocol.Server;

namespace Aonik.Finance.Mcp.Tools;

/// <summary>
/// MCP tools for ledger operations.
/// Domain services are injected via DI into method parameters.
/// </summary>
[McpServerToolType]
public static class LedgerMcpTools
{
    [McpServerTool(Name = "finance_list_ledgers"), Description("Lists all ledgers for the current tenant.")]
    public static async Task<IReadOnlyList<LedgerResponse>> ListLedgers(
        ILedgerService ledgerService,
        CancellationToken cancellationToken = default)
    {
        return await ledgerService.ListLedgersAsync(cancellationToken);
    }

    [McpServerTool(Name = "finance_list_accounts"), Description("Lists ledger accounts, optionally filtered by ledger ID.")]
    public static async Task<IReadOnlyList<LedgerAccountResponse>> ListAccounts(
        ILedgerService ledgerService,
        [Description("Optional ledger ID to filter accounts by")] Guid? ledgerId = null,
        CancellationToken cancellationToken = default)
    {
        var request = new ListLedgerAccountsRequest(ledgerId);
        return await ledgerService.ListAccountsAsync(request, cancellationToken);
    }

    [McpServerTool(Name = "finance_list_journal_entries"), Description("Lists journal entries, optionally filtered by ledger ID. Returns the most recent entries up to a server-side page limit — not necessarily every entry.")]
    public static async Task<IReadOnlyList<JournalEntryResponse>> ListJournalEntries(
        ILedgerService ledgerService,
        [Description("Optional ledger ID to filter entries by")] Guid? ledgerId = null,
        CancellationToken cancellationToken = default)
    {
        var request = new ListJournalEntriesRequest(ledgerId);
        return await ledgerService.ListJournalEntriesAsync(request, cancellationToken);
    }

    [McpServerTool(Name = "finance_create_ledger"), Description("Creates a new ledger with the specified base currency.")]
    public static async Task<LedgerResponse> CreateLedger(
        ILedgerService ledgerService,
        [Description("ISO 4217 base currency code for the ledger (e.g. USD, NGN)")] string baseCurrency,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateLedgerRequest(baseCurrency);
        return await ledgerService.CreateLedgerAsync(request, cancellationToken);
    }

    [McpServerTool(Name = "finance_create_account"), Description("Creates a new ledger account within a specified ledger.")]
    public static async Task<LedgerAccountResponse> CreateAccount(
        ILedgerService ledgerService,
        [Description("The ledger ID to create the account in")] Guid ledgerId,
        [Description("Human-readable account name")] string name,
        [Description("Unique account code (e.g. 1000, 2000)")] string code,
        [Description("Account type (e.g. Asset, Liability, Revenue, Expense, Equity)")] string accountType,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateLedgerAccountRequest(ledgerId, name, code, accountType);
        return await ledgerService.CreateAccountAsync(request, cancellationToken);
    }
}
