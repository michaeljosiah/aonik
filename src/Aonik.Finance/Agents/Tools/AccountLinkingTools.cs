using System.ComponentModel;
using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Finance.Agents.Tools;

/// <summary>
/// AI agent tools for account linking operations (Plaid and similar aggregators).
/// Read-only tools surface linked connections and sync health; mutating tools
/// (start a link session, refresh, sync, disconnect) rely on the <c>confirmAction</c>
/// frontend tool for human-in-the-loop approval.
/// </summary>
internal sealed class AccountLinkingTools
{
    private readonly IPersonalAccountLinkService _linkService;

    private AccountLinkingTools(IPersonalAccountLinkService linkService)
    {
        _linkService = linkService;
    }

    // ── Read Tools ────────────────────────────────────────────────

    [Description("Lists the user's linked external bank / aggregator connections (e.g. Plaid). Each connection includes provider, institution, consent status, last sync time, last sync status, any last error, and the accounts attached to it. Use this to answer 'what accounts have I linked?' and to diagnose sync problems. Set includeDisconnected to true to also include connections the user has previously disconnected.")]
    public async Task<IReadOnlyList<AccountLinkConnectionResponse>> ListLinkedAccounts(
        [Description("Whether to include disconnected connections (default: false)")] bool includeDisconnected = false,
        CancellationToken cancellationToken = default)
    {
        return await _linkService.ListConnectionsAsync(includeDisconnected, cancellationToken);
    }

    [Description("Gets a unified summary of every personal account — both manually created and linked via an aggregator — with sync status where applicable. Useful for showing the user one consolidated view of their accounts with sync health. Set includeArchived to true to include archived accounts.")]
    public async Task<IReadOnlyList<AccountLinkSummaryItemResponse>> GetAccountLinkSummary(
        [Description("Whether to include archived accounts (default: false)")] bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        return await _linkService.GetSummaryAsync(includeArchived, cancellationToken);
    }

    // ── Mutating Tools ────────────────────────────────────────────

    [Description("Starts a new account link session with an aggregator (e.g. Plaid) and returns a launch token the mobile/web client uses to open the provider's connect popup. Mode is usually 'connect' for new links or 'update' to re-authenticate an existing connection (in which case pass the existing connectionId). The session is short-lived and single-use. The actual link is not completed until the client exchanges the temporary code from the popup.")]
    public async Task<AccountLinkSessionResponse> CreateAccountLinkSession(
        [Description("Aggregator provider (e.g. 'plaid')")] string provider,
        [Description("Session mode: 'connect' to link a new institution, 'update' to re-authenticate an existing connection (default: 'connect')")] string mode = "connect",
        [Description("Optional: existing connection ID when mode is 'update'")] Guid? connectionId = null,
        [Description("Optional: ISO 3166-1 alpha-2 country code for institution filtering (e.g. 'US', 'GB', 'NG')")] string? countryCode = null,
        [Description("Optional: android package name when launching from an Android client")] string? androidPackageName = null,
        [Description("Optional: redirect URI for OAuth-based providers")] string? redirectUri = null,
        [Description("Optional: client display name shown inside the provider's popup")] string? clientName = null,
        [Description("Optional: user phone number for providers that pre-fill it")] string? phoneNumber = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateAccountLinkSessionRequest(
            provider,
            mode,
            connectionId,
            androidPackageName,
            redirectUri,
            countryCode,
            clientName,
            phoneNumber);
        return await _linkService.CreateSessionAsync(request, cancellationToken);
    }

    [Description("Refreshes metadata for a linked connection (institution info, account list, consent/error state). This does NOT pull new transactions — use SyncLinkedAccountTransactions for that. Useful after a provider webhook fires or when the user reports the link looks stale.")]
    public async Task<AccountLinkConnectionResponse?> RefreshLinkedAccount(
        [Description("The unique identifier (GUID) of the connection to refresh")] Guid connectionId,
        CancellationToken cancellationToken = default)
    {
        return await _linkService.RefreshConnectionAsync(connectionId, cancellationToken);
    }

    [Description("Triggers a manual transaction sync for a linked connection. Pulls new transactions from the provider and returns counts of transactions added, updated, removed, and skipped. Use when the user asks to refresh their transaction feed or suspects transactions are missing.")]
    public async Task<AccountLinkTransactionSyncResponse?> SyncLinkedAccountTransactions(
        [Description("The unique identifier (GUID) of the connection to sync")] Guid connectionId,
        CancellationToken cancellationToken = default)
    {
        return await _linkService.SyncConnectionTransactionsAsync(connectionId, cancellationToken);
    }

    [Description("Disconnects a linked connection and revokes the stored provider credentials. The connection record is retained for historical reference but no further syncs will occur. Transactions already imported are preserved. The user must re-link to restore sync.")]
    public async Task<string> DisconnectLinkedAccount(
        [Description("The unique identifier (GUID) of the connection to disconnect")] Guid connectionId,
        CancellationToken cancellationToken = default)
    {
        var result = await _linkService.DisconnectConnectionAsync(connectionId, cancellationToken);
        return result is null
            ? $"Connection {connectionId} was not found."
            : $"Connection {connectionId} ({result.InstitutionName}) has been disconnected.";
    }

    // ── Tool Factory ──────────────────────────────────────────────

    /// <summary>
    /// Creates <see cref="AITool"/> instances for all account linking tools.
    /// Mutating tools (CreateAccountLinkSession, RefreshLinkedAccount,
    /// SyncLinkedAccountTransactions, DisconnectLinkedAccount) rely on the
    /// <c>confirmAction</c> frontend tool for human-in-the-loop approval.
    /// </summary>
    public static IEnumerable<AITool> CreateAll(IServiceProvider serviceProvider)
    {
        var tools = new AccountLinkingTools(
            serviceProvider.GetRequiredService<IPersonalAccountLinkService>());

        // Read-only — safe for autonomous use
        yield return AIFunctionFactory.Create(tools.ListLinkedAccounts, name: "pf_list_linked_accounts");
        yield return AIFunctionFactory.Create(tools.GetAccountLinkSummary, name: "pf_get_account_link_summary");

        // Mutating — approval enforced via the confirmAction frontend tool
        yield return AIFunctionFactory.Create(tools.CreateAccountLinkSession, name: "pf_create_account_link_session");
        yield return AIFunctionFactory.Create(tools.RefreshLinkedAccount, name: "pf_refresh_linked_account");
        yield return AIFunctionFactory.Create(tools.SyncLinkedAccountTransactions, name: "pf_sync_linked_account_transactions");
        yield return AIFunctionFactory.Create(tools.DisconnectLinkedAccount, name: "pf_disconnect_linked_account");
    }
}
