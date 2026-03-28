using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aonik.Finance.Contracts.Models.ExternalAccounts;

public record CreateExternalAccountLinkSessionRequest(
    string Provider,
    string Mode = "connect",
    Guid? ConnectionId = null,
    string? CountryCode = null,
    string? ClientName = null);

public record ExchangeExternalAccountLinkSessionRequest(
    Guid SessionId,
    string TemporaryCode);

public record ListExternalAccountTransactionsRequest(
    Guid? ExternalAccountId = null,
    Guid? ConnectionId = null,
    string? ReconciliationStatus = null,
    DateTime? From = null,
    DateTime? To = null,
    int PageNumber = 1,
    int PageSize = 50);

public record ExternalAccountLinkSessionResponse(
    Guid SessionId,
    string Provider,
    string ProviderDisplayName,
    string Mode,
    string Status,
    Guid? ConnectionId,
    string LaunchToken,
    DateTime ExpiresAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record ExternalAccountLinkedAccountResponse(
    Guid LinkedAccountId,
    Guid ExternalAccountId,
    string Name,
    string AccountType,
    string? AccountSubtype,
    string Currency,
    string? Last4,
    string Status,
    DateTime? LastSyncedAt,
    string? LastSyncStatus,
    string? LastError,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record ExternalAccountConnectionResponse(
    Guid ConnectionId,
    string Provider,
    string ProviderDisplayName,
    string InstitutionName,
    string? InstitutionReference,
    string Status,
    string ConsentStatus,
    bool AutoSyncEnabled,
    DateTime? LastSyncedAt,
    string? LastSyncStatus,
    string? LastError,
    DateTime? DisconnectedAt,
    IReadOnlyList<ExternalAccountLinkedAccountResponse> LinkedAccounts,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record ExternalAccountLinkExchangeResponse(
    Guid SessionId,
    ExternalAccountConnectionResponse Connection);

public record ExternalAccountLinkActionResponse(
    string Action,
    ExternalAccountConnectionResponse Connection);

public record ExternalAccountTransactionResponse(
    Guid TransactionId,
    Guid ExternalAccountId,
    Guid? ExternalAccountConnectionId,
    DateTime OccurredAt,
    decimal Amount,
    string Currency,
    string? Counterparty,
    string? Description,
    string? Reference,
    string? Category,
    bool Pending,
    string ReconciliationStatus,
    Guid? MatchedLedgerEntryId,
    Guid? MatchedPayoutId,
    DateTime? ReconciledAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record ExternalAccountTransactionSyncResponse(
    Guid ConnectionId,
    int TransactionsAdded,
    int TransactionsUpdated,
    int TransactionsRemoved,
    int TransactionsSkipped,
    string SyncStatus,
    string? NextCursor,
    DateTime SyncedAt);

public record ExternalAccountLinkActionRequiredErrorResponse(
    string Error,
    string Message,
    string RequiredAction,
    bool RequiresReconnect,
    Guid ConnectionId,
    string Provider,
    string? ProviderErrorCode);

public record ExternalAccountLinkWebhookResponse(string Status);

public class PlaidExternalAccountWebhookRequest
{
    [JsonPropertyName("webhook_type")]
    public string WebhookType { get; set; } = string.Empty;

    [JsonPropertyName("webhook_code")]
    public string WebhookCode { get; set; } = string.Empty;

    [JsonPropertyName("item_id")]
    public string? ItemId { get; set; }

    [JsonPropertyName("environment")]
    public string? Environment { get; set; }

    [JsonPropertyName("error")]
    public PlaidExternalAccountWebhookError? Error { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalData { get; set; }
}

public class PlaidExternalAccountWebhookError
{
    [JsonPropertyName("error_type")]
    public string? ErrorType { get; set; }

    [JsonPropertyName("error_code")]
    public string? ErrorCode { get; set; }

    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("display_message")]
    public string? DisplayMessage { get; set; }
}

// ── Manual Account CRUD ──────────────────────────────────────────

public record CreateExternalAccountRequest(
    string Name,
    string ExternalAccountType,
    string Currency,
    string? Country,
    string? InstitutionName,
    string? Last4,
    string? Notes);

public record ExternalAccountResponse(
    Guid ExternalAccountId,
    string ExternalAccountType,
    string MaskedIdentifier,
    string? ProviderRef,
    string VerificationStatus,
    string? Currency,
    string? Country,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

// ── Manual Transaction CRUD ──────────────────────────────────────

public record CreateExternalAccountTransactionRequest(
    Guid ExternalAccountId,
    DateTime OccurredAt,
    decimal Amount,
    string Currency,
    string? Counterparty,
    string? Description,
    string? Reference,
    string? Category,
    string? Notes);

// ── Transaction Attachments ──────────────────────────────────────

public record ExternalAccountTransactionAttachmentResponse(
    Guid AttachmentId,
    string FileName,
    string ContentType,
    string Url,
    long FileSizeBytes,
    DateTime CreatedAt);
