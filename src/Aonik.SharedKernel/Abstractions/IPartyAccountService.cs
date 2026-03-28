namespace Aonik.SharedKernel.Abstractions;

/// <summary>
/// Cross-module result for ExternalAccount queries.
/// </summary>
public record ExternalAccountResult(
    Guid Id,
    Guid TenantId,
    Guid PartyId,
    string ExternalAccountType,
    string MaskedIdentifier,
    string? ProviderRef,
    string VerificationStatus,
    string? Currency,
    string? Country,
    string MetadataJson,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>
/// Cross-module contract for managing ExternalAccount entities.
/// The authoritative implementation lives in the Platform module.
/// </summary>
public interface IExternalAccountService
{
    /// <summary>
    /// Finds an existing ExternalAccount by tenant, party, type, and masked identifier,
    /// or creates one if it does not exist. Returns the ExternalAccount ID.
    /// </summary>
    Task<Guid> FindOrCreateExternalAccountAsync(
        Guid tenantId,
        Guid partyId,
        string externalAccountType,
        string maskedIdentifier,
        string? providerRef,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new ExternalAccount. Returns the result with the assigned ID.
    /// </summary>
    Task<ExternalAccountResult> CreateExternalAccountAsync(
        Guid tenantId,
        Guid partyId,
        string externalAccountType,
        string maskedIdentifier,
        string? providerRef,
        string verificationStatus,
        string? currency,
        string? country,
        string? metadataJson,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all ExternalAccounts for a tenant.
    /// </summary>
    Task<IReadOnlyList<ExternalAccountResult>> ListExternalAccountsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single ExternalAccount by ID within a tenant.
    /// </summary>
    Task<ExternalAccountResult?> GetExternalAccountAsync(
        Guid tenantId,
        Guid accountId,
        CancellationToken cancellationToken = default);
}
