using Aonik.Finance.Contracts.Models.PersonalFinance;

namespace Aonik.Finance.Contracts.Services.PersonalFinance;

/// <summary>
/// Customer-scoped CRUD over <c>CareEntity</c> (Spec 043). Every operation is
/// isolated to the current tenant + user; a non-owned id reads as not-found
/// (404, never 403 — existence is not revealed).
/// </summary>
public interface ICareEntityService
{
    Task<IReadOnlyList<CareEntityResponse>> ListAsync(
        string? kind = null,
        string? assetType = null,
        bool includeArchived = false,
        CancellationToken cancellationToken = default);

    Task<CareEntityResponse?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<CareEntityResponse> CreateAsync(
        CreateCareEntityRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Returns <c>null</c> when the entity is not owned by the current user.</summary>
    Task<CareEntityResponse?> UpdateAsync(
        Guid id,
        UpdateCareEntityRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Returns <c>false</c> when the entity is not owned by the current user.</summary>
    Task<bool> ArchiveAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
