using System.ComponentModel;
using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;

namespace Aonik.Finance.Agents.Tools;

/// <summary>
/// Personal-finance account tools (read + mutating). Registered by
/// <see cref="PersonalFinanceTools.CreateAll"/>.
/// </summary>
internal sealed class PersonalFinanceAccountTools
{
    private readonly IPersonalAccountService _accountService;

    public PersonalFinanceAccountTools(IPersonalAccountService accountService)
    {
        _accountService = accountService;
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
}
