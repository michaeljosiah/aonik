using Aonik.Finance.Contracts.Models.Accounts;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Finance.Contracts.Services.Accounts;

public interface IAccountLinkService
{
    Task<AccountLinkSessionResponse> CreateSessionAsync(
        CreateAccountLinkSessionRequest request,
        CancellationToken cancellationToken = default);

    Task<AccountLinkExchangeResponse> ExchangeSessionAsync(
        ExchangeAccountLinkSessionRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AccountConnectionResponse>> ListConnectionsAsync(
        bool includeDisconnected = false,
        CancellationToken cancellationToken = default);

    Task<AccountConnectionResponse?> RefreshConnectionAsync(
        Guid connectionId,
        CancellationToken cancellationToken = default);

    Task<AccountConnectionResponse?> DisconnectConnectionAsync(
        Guid connectionId,
        CancellationToken cancellationToken = default);

    Task<AccountTransactionSyncResponse?> SyncConnectionTransactionsAsync(
        Guid connectionId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<AccountTransactionResponse>> ListTransactionsAsync(
        ListAccountTransactionsRequest request,
        CancellationToken cancellationToken = default);

    Task ProcessPlaidWebhookAsync(
        PlaidAccountWebhookRequest request,
        CancellationToken cancellationToken = default);

    // ── Manual Account CRUD ──────────────────────────────────────

    Task<AccountResponse> CreateAccountAsync(
        CreateAccountRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AccountResponse>> ListAccountsAsync(
        CancellationToken cancellationToken = default);

    // ── Manual Transaction CRUD ──────────────────────────────────

    Task<AccountTransactionResponse> CreateTransactionAsync(
        CreateAccountTransactionRequest request,
        CancellationToken cancellationToken = default);

    // ── Transaction Attachments ──────────────────────────────────

    Task<AccountTransactionAttachmentResponse> AddTransactionAttachmentAsync(
        Guid transactionId,
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AccountTransactionAttachmentResponse>> ListTransactionAttachmentsAsync(
        Guid transactionId,
        CancellationToken cancellationToken = default);

    Task DeleteTransactionAttachmentAsync(
        Guid attachmentId,
        CancellationToken cancellationToken = default);
}
