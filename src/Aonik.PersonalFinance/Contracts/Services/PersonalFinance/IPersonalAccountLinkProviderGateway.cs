namespace Aonik.PersonalFinance.Contracts.Services;

public interface IPersonalAccountLinkProviderGateway
{
    string ProviderCode { get; }

    string DisplayName { get; }

    Task<AccountLinkProviderSessionResult> CreateSessionAsync(
        AccountLinkProviderSessionRequest request,
        CancellationToken cancellationToken = default);

    Task<AccountLinkProviderExchangeResult> ExchangeSessionAsync(
        AccountLinkProviderExchangeRequest request,
        CancellationToken cancellationToken = default);

    Task<AccountLinkProviderExchangeResult> RefreshConnectionAsync(
        AccountLinkProviderRefreshRequest request,
        CancellationToken cancellationToken = default);

    Task DisconnectConnectionAsync(
        AccountLinkProviderDisconnectRequest request,
        CancellationToken cancellationToken = default);

    Task<AccountLinkProviderTransactionsSyncResult> SyncTransactionsAsync(
        AccountLinkProviderTransactionsSyncRequest request,
        CancellationToken cancellationToken = default);
}

public record AccountLinkProviderSessionRequest(
    Guid TenantId,
    Guid UserId,
    Guid SessionId,
    Guid? ConnectionId,
    string? ExistingConnectionReference,
    string? ExistingSecretReference,
    string Mode,
    string? AndroidPackageName,
    string? RedirectUri,
    string? CountryCode,
    string? ClientName,
    string? PhoneNumber);

public record AccountLinkProviderSessionResult(
    string LaunchToken,
    string? ProviderSessionReference,
    DateTime ExpiresAt);

public record AccountLinkProviderExchangeRequest(
    Guid TenantId,
    Guid UserId,
    Guid SessionId,
    Guid? ConnectionId,
    string? ExistingConnectionReference,
    string SessionToken,
    string TemporaryCode,
    string Mode);

public record AccountLinkProviderRefreshRequest(
    Guid TenantId,
    Guid UserId,
    Guid ConnectionId,
    string ProviderConnectionReference,
    string SecretReference);

public record AccountLinkProviderDisconnectRequest(
    Guid TenantId,
    Guid UserId,
    Guid ConnectionId,
    string ProviderConnectionReference,
    string SecretReference);

public record AccountLinkProviderTransactionsSyncRequest(
    Guid TenantId,
    Guid UserId,
    Guid ConnectionId,
    string ProviderConnectionReference,
    string SecretReference,
    string? Cursor);

public record AccountLinkProviderAccountResult(
    string ProviderAccountReference,
    string Name,
    string AccountType,
    string? AccountSubtype,
    string Currency,
    string? Last4,
    string Status);

public record AccountLinkProviderExchangeResult(
    string ProviderConnectionReference,
    string SecretReference,
    string InstitutionName,
    string? InstitutionReference,
    string ConsentStatus,
    DateTime? LastSyncedAt,
    string LastSyncStatus,
    string? LastError,
    IReadOnlyList<AccountLinkProviderAccountResult> Accounts);

public record AccountLinkProviderTransactionResult(
    string ProviderTransactionReference,
    string ProviderAccountReference,
    DateTime OccurredAt,
    decimal Amount,
    string Currency,
    string? Merchant,
    string? Description,
    string? Category,
    string? SubCategory,
    bool Pending);

public record AccountLinkProviderTransactionsSyncResult(
    string? NextCursor,
    DateTime SyncedAt,
    string SyncStatus,
    string? LastError,
    IReadOnlyList<AccountLinkProviderTransactionResult> Transactions,
    IReadOnlyList<string> RemovedTransactionReferences);
