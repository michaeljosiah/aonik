using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aonik.Finance.Contracts.Models.Accounts;

public record CreateAccountLinkSessionRequest(
    string Provider,
    string Mode = "connect",
    Guid? ConnectionId = null,
    string? CountryCode = null,
    string? ClientName = null);

public record ExchangeAccountLinkSessionRequest(
    Guid SessionId,
    string TemporaryCode);

public record ListAccountTransactionsRequest(
    Guid? AccountId = null,
    Guid? ConnectionId = null,
    string? ReconciliationStatus = null,
    DateTime? From = null,
    DateTime? To = null,
    int PageNumber = 1,
    int PageSize = 50);

public record AccountLinkSessionResponse(
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

public record LinkedAccountResponse(
    Guid LinkedAccountId,
    Guid AccountId,
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

public record AccountConnectionResponse(
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
    IReadOnlyList<LinkedAccountResponse> LinkedAccounts,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record AccountLinkExchangeResponse(
    Guid SessionId,
    AccountConnectionResponse Connection);

public record AccountLinkActionResponse(
    string Action,
    AccountConnectionResponse Connection);

public record AccountTransactionResponse(
    Guid TransactionId,
    Guid AccountId,
    Guid? AccountConnectionId,
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

public record AccountTransactionSyncResponse(
    Guid ConnectionId,
    int TransactionsAdded,
    int TransactionsUpdated,
    int TransactionsRemoved,
    int TransactionsSkipped,
    string SyncStatus,
    string? NextCursor,
    DateTime SyncedAt);

public record AccountLinkActionRequiredErrorResponse(
    string Error,
    string Message,
    string RequiredAction,
    bool RequiresReconnect,
    Guid ConnectionId,
    string Provider,
    string? ProviderErrorCode);

public record AccountLinkWebhookResponse(string Status);

public class PlaidAccountWebhookRequest
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
    public PlaidAccountWebhookError? Error { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalData { get; set; }
}

public class PlaidAccountWebhookError
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

public record CreateAccountRequest(
    string Name,
    string AccountType,
    string Currency,
    string? Country,
    string? InstitutionName,
    string? Last4,
    string? Notes);

public record AccountResponse(
    Guid AccountId,
    string AccountType,
    string MaskedIdentifier,
    string? ProviderRef,
    string VerificationStatus,
    string? Currency,
    string? Country,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

// ── Manual Transaction CRUD ──────────────────────────────────────

public record CreateAccountTransactionRequest(
    Guid AccountId,
    DateTime OccurredAt,
    decimal Amount,
    string Currency,
    string? Counterparty,
    string? Description,
    string? Reference,
    string? Category,
    string? Notes);

// ── Transaction Attachments ──────────────────────────────────────

public record AccountTransactionAttachmentResponse(
    Guid AttachmentId,
    string FileName,
    string ContentType,
    string Url,
    long FileSizeBytes,
    DateTime CreatedAt);
