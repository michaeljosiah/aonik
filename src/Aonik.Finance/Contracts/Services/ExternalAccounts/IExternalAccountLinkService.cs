using Aonik.Finance.Contracts.Models.ExternalAccounts;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Finance.Contracts.Services.ExternalAccounts;

public interface IExternalAccountLinkService
{
    Task<ExternalAccountLinkSessionResponse> CreateSessionAsync(
        CreateExternalAccountLinkSessionRequest request,
        CancellationToken cancellationToken = default);

    Task<ExternalAccountLinkExchangeResponse> ExchangeSessionAsync(
        ExchangeExternalAccountLinkSessionRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExternalAccountConnectionResponse>> ListConnectionsAsync(
        bool includeDisconnected = false,
        CancellationToken cancellationToken = default);

    Task<ExternalAccountConnectionResponse?> RefreshConnectionAsync(
        Guid connectionId,
        CancellationToken cancellationToken = default);

    Task<ExternalAccountConnectionResponse?> DisconnectConnectionAsync(
        Guid connectionId,
        CancellationToken cancellationToken = default);

    Task<ExternalAccountTransactionSyncResponse?> SyncConnectionTransactionsAsync(
        Guid connectionId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<ExternalAccountTransactionResponse>> ListTransactionsAsync(
        ListExternalAccountTransactionsRequest request,
        CancellationToken cancellationToken = default);

    Task ProcessPlaidWebhookAsync(
        PlaidExternalAccountWebhookRequest request,
        CancellationToken cancellationToken = default);

    // ── Manual Account CRUD ──────────────────────────────────────

    Task<ExternalAccountResponse> CreateAccountAsync(
        CreateExternalAccountRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExternalAccountResponse>> ListAccountsAsync(
        CancellationToken cancellationToken = default);

    // ── Manual Transaction CRUD ──────────────────────────────────

    Task<ExternalAccountTransactionResponse> CreateTransactionAsync(
        CreateExternalAccountTransactionRequest request,
        CancellationToken cancellationToken = default);

    // ── Transaction Attachments ──────────────────────────────────

    Task<ExternalAccountTransactionAttachmentResponse> AddTransactionAttachmentAsync(
        Guid transactionId,
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExternalAccountTransactionAttachmentResponse>> ListTransactionAttachmentsAsync(
        Guid transactionId,
        CancellationToken cancellationToken = default);

    Task DeleteTransactionAttachmentAsync(
        Guid attachmentId,
        CancellationToken cancellationToken = default);
}
