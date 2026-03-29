namespace Aonik.SharedKernel.Abstractions;

/// <summary>
/// Cross-module result for PartyAccount queries.
/// </summary>
public record PartyAccountResult(
    Guid Id,
    Guid TenantId,
    Guid PartyId,
    string AccountType,
    string MaskedIdentifier,
    string? ProviderRef,
    string VerificationStatus,
    string? Currency,
    string? Country,
    string MetadataJson,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>
/// Cross-module contract for managing PartyAccount entities.
/// The authoritative implementation lives in the Platform module.
/// </summary>
public interface IPartyAccountService
{
    /// <summary>
    /// Finds an existing PartyAccount by tenant, party, type, and masked identifier,
    /// or creates one if it does not exist. Returns the PartyAccount ID.
    /// </summary>
    Task<Guid> FindOrCreatePartyAccountAsync(
        Guid tenantId,
        Guid partyId,
        string accountType,
        string maskedIdentifier,
        string? providerRef,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new PartyAccount. Returns the result with the assigned ID.
    /// </summary>
    Task<PartyAccountResult> CreatePartyAccountAsync(
        Guid tenantId,
        Guid partyId,
        string accountType,
        string maskedIdentifier,
        string? providerRef,
        string verificationStatus,
        string? currency,
        string? country,
        string? metadataJson,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all PartyAccounts for a tenant.
    /// </summary>
    Task<IReadOnlyList<PartyAccountResult>> ListPartyAccountsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single PartyAccount by ID within a tenant.
    /// </summary>
    Task<PartyAccountResult?> GetPartyAccountAsync(
        Guid tenantId,
        Guid accountId,
        CancellationToken cancellationToken = default);
}
