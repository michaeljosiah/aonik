namespace Aonik.SharedKernel.Abstractions.Platform;

/// <summary>
/// Cross-module read access to Platform's User directory.
/// Returns display-name / email pairs scoped to the tenant for use by
/// PersonalFinance household lookups, audit log redaction, etc.
/// See <a href="../../../../docs/specifications/027.extract-personal-finance-module.html">Spec 027</a>.
/// </summary>
public interface IUserDirectoryReader
{
    /// <summary>
    /// Returns the email + status for each requested user id, scoped to the tenant.
    /// Unknown ids are silently omitted.
    /// </summary>
    Task<IReadOnlyList<UserDirectoryItem>> GetByIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the (UserId, TenantId) key for every user across ALL tenants.
    /// Deliberately cross-tenant: the PersonalFinance profile seed contributor
    /// (Spec 027 S5, #126) ensures every platform user has a PersonalProfile,
    /// so it must see users regardless of the ambient tenant context. The
    /// implementation opts into the sanctioned cross-tenant read explicitly.
    /// </summary>
    Task<IReadOnlyList<UserDirectoryKey>> GetAllUserKeysAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Cross-module projection of a User. Carries only what non-Platform consumers
/// actually read.
/// </summary>
public sealed record UserDirectoryItem(
    Guid UserId,
    string? Email,
    string Status);

/// <summary>
/// Cross-module identity key for a User: the (UserId, TenantId) pair that
/// scopes every tenant-owned entity. Returned by
/// <see cref="IUserDirectoryReader.GetAllUserKeysAsync"/> across all tenants.
/// </summary>
public sealed record UserDirectoryKey(
    Guid UserId,
    Guid TenantId);
