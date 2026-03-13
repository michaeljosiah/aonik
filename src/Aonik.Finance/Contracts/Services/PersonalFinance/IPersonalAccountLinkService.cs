using Aonik.Finance.Contracts.Models.PersonalFinance;

namespace Aonik.Finance.Contracts.Services.PersonalFinance;

public interface IPersonalAccountLinkService
{
    Task<AccountLinkSessionResponse> CreateSessionAsync(
        CreateAccountLinkSessionRequest request,
        CancellationToken cancellationToken = default);

    Task<AccountLinkExchangeResponse?> ExchangeSessionAsync(
        ExchangeAccountLinkSessionRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AccountLinkConnectionResponse>> ListConnectionsAsync(
        bool includeDisconnected = false,
        CancellationToken cancellationToken = default);

    Task<AccountLinkConnectionResponse?> RefreshConnectionAsync(
        Guid connectionId,
        CancellationToken cancellationToken = default);

    Task<AccountLinkConnectionResponse?> DisconnectConnectionAsync(
        Guid connectionId,
        CancellationToken cancellationToken = default);

    Task<AccountLinkTransactionSyncResponse?> SyncConnectionTransactionsAsync(
        Guid connectionId,
        CancellationToken cancellationToken = default);

    Task ProcessPlaidWebhookAsync(
        PlaidAccountLinkWebhookRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AccountLinkSummaryItemResponse>> GetSummaryAsync(
        bool includeArchived = false,
        CancellationToken cancellationToken = default);
}
