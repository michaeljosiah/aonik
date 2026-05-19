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
}

/// <summary>
/// Cross-module projection of a User. Carries only what non-Platform consumers
/// actually read.
/// </summary>
public sealed record UserDirectoryItem(
    Guid UserId,
    string? Email,
    string Status);
