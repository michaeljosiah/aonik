using System.Security.Cryptography;
using System.Text;

using Aonik.Finance.Contracts.Services.PersonalFinance;

namespace Aonik.Finance.Services.PersonalFinance;

internal sealed class PlaidSimulatedAccountLinkProviderGateway : IPersonalAccountLinkProviderGateway
{
    public string ProviderCode => "Plaid";

    public string DisplayName => "Plaid";

    public Task<AccountLinkProviderSessionResult> CreateSessionAsync(
        AccountLinkProviderSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        var sessionKey = Guid.NewGuid().ToString("N");
        var launchToken = $"plaid_link_{request.SessionId:N}_{sessionKey}";

        var providerSessionReference = request.Mode == "update" && !string.IsNullOrWhiteSpace(request.ExistingConnectionReference)
            ? $"plaid-update-session-{request.ExistingConnectionReference}"
            : $"plaid-session-{request.SessionId:N}";

        var result = new AccountLinkProviderSessionResult(
            launchToken,
            providerSessionReference,
            DateTime.UtcNow.AddMinutes(30));

        return Task.FromResult(result);
    }

    public Task<AccountLinkProviderExchangeResult> ExchangeSessionAsync(
        AccountLinkProviderExchangeRequest request,
        CancellationToken cancellationToken = default)
    {
        var connectionReference = ResolveConnectionReference(
            request.Mode,
            request.ExistingConnectionReference,
            request.TemporaryCode);

        var result = BuildSyncResult(connectionReference, request.Mode == "update" ? "UpdateModeComplete" : "InitialSyncComplete");
        return Task.FromResult(result);
    }

    public Task<AccountLinkProviderExchangeResult> RefreshConnectionAsync(
        AccountLinkProviderRefreshRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = BuildSyncResult(request.ProviderConnectionReference, "RefreshComplete");
        return Task.FromResult(result);
    }

    public Task DisconnectConnectionAsync(
        AccountLinkProviderDisconnectRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<AccountLinkProviderTransactionsSyncResult> SyncTransactionsAsync(
        AccountLinkProviderTransactionsSyncRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            return Task.FromResult(new AccountLinkProviderTransactionsSyncResult(
                request.Cursor,
                DateTime.UtcNow,
                "TransactionsSyncComplete",
                null,
                [],
                []));
        }

        var accountReferenceSeed = ExtractAccountReferenceSeed(request.ProviderConnectionReference);
        var occurredAt = DateTime.UtcNow.Date.AddDays(-1);

        var transactions = new List<AccountLinkProviderTransactionResult>
        {
            new(
                $"txn_{accountReferenceSeed}_coffee",
                $"acct_{accountReferenceSeed}_current",
                occurredAt,
                -6.40m,
                "GBP",
                "Blue Bottle",
                "Morning coffee",
                TransactionCategoryReference.FoodAndDrink,
                null,
                false),
            new(
                $"txn_{accountReferenceSeed}_groceries",
                $"acct_{accountReferenceSeed}_current",
                occurredAt.AddDays(-1),
                -48.25m,
                "GBP",
                "Fresh Market",
                "Weekly groceries",
                TransactionCategoryReference.GeneralMerchandise,
                null,
                false)
        };

        return Task.FromResult(new AccountLinkProviderTransactionsSyncResult(
            $"cursor_{accountReferenceSeed}_1",
            DateTime.UtcNow,
            "TransactionsSyncComplete",
            null,
            transactions,
            []));
    }

    private static string ResolveConnectionReference(
        string mode,
        string? existingConnectionReference,
        string temporaryCode)
    {
        if (mode == "update" && !string.IsNullOrWhiteSpace(existingConnectionReference))
        {
            return existingConnectionReference.Trim();
        }

        var fingerprint = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(temporaryCode.Trim())))
            .ToLowerInvariant();

        return $"item_{fingerprint[..16]}";
    }

    private static AccountLinkProviderExchangeResult BuildSyncResult(
        string connectionReference,
        string syncStatus)
    {
        var accountReferenceSeed = ExtractAccountReferenceSeed(connectionReference);
        var lastSyncedAt = DateTime.UtcNow;

        var accounts = new List<AccountLinkProviderAccountResult>
        {
            new(
                $"acct_{accountReferenceSeed}_current",
                "Everyday current",
                "bank",
                "current",
                "GBP",
                "1842",
                "Connected"),
            new(
                $"acct_{accountReferenceSeed}_savings",
                "Rainy day saver",
                "bank",
                "savings",
                "GBP",
                "8801",
                "Connected")
        };

        return new AccountLinkProviderExchangeResult(
            connectionReference,
            $"vault://financial-connections/plaid/{connectionReference}",
            "Plaid Sandbox Bank",
            $"ins_{accountReferenceSeed[..Math.Min(accountReferenceSeed.Length, 12)]}",
            "Granted",
            lastSyncedAt,
            syncStatus,
            null,
            accounts);
    }

    private static string ExtractAccountReferenceSeed(string connectionReference)
    {
        var normalized = connectionReference.Trim();
        if (normalized.StartsWith("item_", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[5..];
        }

        if (normalized.Length >= 10)
        {
            return normalized[..10].ToLowerInvariant();
        }

        return normalized.PadRight(10, '0').ToLowerInvariant();
    }
}
