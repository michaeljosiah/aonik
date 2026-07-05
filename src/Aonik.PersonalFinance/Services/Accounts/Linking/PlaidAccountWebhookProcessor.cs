using Aonik.Finance.Contracts.Models.Accounts;
using Aonik.Finance.Entities.Accounts;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aonik.Finance.Services.Accounts.Linking;

/// <summary>
/// Translates inbound Plaid webhook payloads into local
/// <see cref="AccountConnection"/> state changes (disconnects,
/// action-required prompts, transaction-sync nudges, etc.).
/// </summary>
internal sealed class PlaidAccountWebhookProcessor
{
    private readonly PersonalFinanceDbContext _financeDbContext;
    private readonly ITenantContext _tenantContext;
    private readonly AccountConnectionSyncOptions _syncOptions;
    private readonly ILogger _logger;

    public PlaidAccountWebhookProcessor(
        PersonalFinanceDbContext financeDbContext,
        ITenantContext tenantContext,
        IOptions<AccountConnectionSyncOptions> syncOptions,
        ILogger logger)
    {
        _financeDbContext = financeDbContext;
        _tenantContext = tenantContext;
        _syncOptions = syncOptions.Value;
        _logger = logger;
    }

    public async Task ProcessAsync(
        PlaidAccountWebhookRequest request,
        CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var originalTenantId = _tenantContext.TenantId;
        var originalResolutionSource = _tenantContext.ResolutionSource;

        try
        {
            if (string.IsNullOrWhiteSpace(request.ItemId))
            {
                _logger.LogWarning("External account Plaid webhook did not include item_id.");
                return;
            }

            var connection = await _financeDbContext.AccountConnections
                .FirstOrDefaultAsync(
                    item => item.Provider == "Plaid"
                        && item.ProviderConnectionReference == request.ItemId.Trim(),
                    cancellationToken);

            if (connection == null)
            {
                _logger.LogWarning("No external account connection found for Plaid item {ItemId}.", request.ItemId.Trim());
                return;
            }

            _tenantContext.TenantId = connection.TenantId;
            _tenantContext.ResolutionSource = "PlaidAccountWebhook";

            connection.LastWebhookReceivedAt = utcNow;

            var linkedAccounts = await _financeDbContext.Accounts
                .Where(item => item.AccountConnectionId == connection.Id)
                .OrderBy(item => item.Name)
                .ToListAsync(cancellationToken);

            ApplyWebhook(request, connection, linkedAccounts, utcNow);

            await _financeDbContext.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            _tenantContext.TenantId = originalTenantId;
            _tenantContext.ResolutionSource = originalResolutionSource;
        }
    }

    private void ApplyWebhook(
        PlaidAccountWebhookRequest request,
        AccountConnection connection,
        IReadOnlyList<Account> linkedAccounts,
        DateTime utcNow)
    {
        var webhookType = AccountLinkingNormalization.NormalizeWebhookValue(request.WebhookType);
        var webhookCode = AccountLinkingNormalization.NormalizeWebhookValue(request.WebhookCode);

        if (webhookType == "ITEM" && webhookCode == "USER_PERMISSION_REVOKED")
        {
            AccountConnectionStateMutator.ApplyLocalDisconnect(connection, linkedAccounts, utcNow, "UserPermissionRevoked");
            return;
        }

        if (webhookType == "ITEM" && (webhookCode == "PENDING_DISCONNECT" || webhookCode == "PENDING_EXPIRATION"))
        {
            var message = webhookCode == "PENDING_DISCONNECT"
                ? "Reconnect required before Plaid disconnects this linked account."
                : "Consent is about to expire. Reconnect this linked account to keep syncing.";

            AccountConnectionStateMutator.ApplyActionRequired(connection, linkedAccounts, webhookCode, message);
            return;
        }

        if (webhookType == "ITEM" && webhookCode == "ERROR")
        {
            var errorCode = AccountLinkingNormalization.NormalizeWebhookValue(request.Error?.ErrorCode);
            if (errorCode == "ITEM_LOGIN_REQUIRED" || errorCode == "PENDING_DISCONNECT")
            {
                var message = AccountLinkingNormalization.TrimNullable(request.Error?.DisplayMessage)
                    ?? AccountLinkingNormalization.TrimNullable(request.Error?.ErrorMessage)
                    ?? "Reconnect required to restore Plaid account access.";

                AccountConnectionStateMutator.ApplyActionRequired(connection, linkedAccounts, errorCode, message);
                return;
            }

            connection.LastSyncStatus = string.IsNullOrWhiteSpace(errorCode) ? webhookCode : errorCode;
            connection.LastError = AccountLinkingNormalization.LimitText(
                request.Error?.DisplayMessage ?? request.Error?.ErrorMessage,
                1000);
            return;
        }

        if (webhookType == "TRANSACTIONS"
            && (webhookCode == "SYNC_UPDATES_AVAILABLE"
                || webhookCode == "INITIAL_UPDATE"
                || webhookCode == "HISTORICAL_UPDATE"
                || webhookCode == "DEFAULT_UPDATE"
                || webhookCode == "TRANSACTIONS_REMOVED"))
        {
            connection.LastSyncStatus = webhookCode;
            connection.LastWebhookReceivedAt = utcNow;
            if (_syncOptions.EnableRecurringSync && connection.AutoSyncEnabled)
            {
                connection.NextScheduledSyncAt = utcNow;
            }
            if (!string.Equals(connection.Status, "ActionRequired", StringComparison.OrdinalIgnoreCase))
            {
                connection.LastError = null;
            }

            foreach (var linkedAccount in linkedAccounts)
            {
                linkedAccount.LastSyncStatus = webhookCode;
                linkedAccount.LastSyncedAt = utcNow;
                if (!string.Equals(linkedAccount.Status, "ActionRequired", StringComparison.OrdinalIgnoreCase))
                {
                    linkedAccount.LastError = null;
                }
            }
        }
    }
}
