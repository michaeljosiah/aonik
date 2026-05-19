namespace Aonik.Platform.Contracts.Services.Identity;

/// <summary>
/// Spec 026 Part 3 — fast lookup of "was this user revoked AFTER the
/// token was issued?" for the JWT auth pipeline. Implementations cache
/// the most-recent blocklist row per user in FusionCache so the hot
/// path is O(1). Implementations also expose a write path used by the
/// access-management service and the deactivate flow.
/// </summary>
public interface IUserSessionBlocklist
{
    /// <summary>
    /// Returns <c>true</c> when <paramref name="tokenIssuedUtc"/> is
    /// earlier than the most-recent revoke for the user (i.e. the
    /// token must be rejected). Returns <c>false</c> when the user
    /// has never been revoked, or the last revoke is older than the
    /// token's <c>iat</c>.
    /// </summary>
    Task<bool> IsRevokedAsync(
        Guid tenantId,
        Guid userId,
        DateTime tokenIssuedUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a blocklist row and invalidates the FusionCache entry.
    /// Returns the timestamps for the audit log / response.
    /// </summary>
    Task<UserSessionRevocation> RevokeAsync(
        Guid tenantId,
        Guid userId,
        Guid? revokedByUserId,
        string reason,
        CancellationToken cancellationToken = default);
}

public sealed record UserSessionRevocation(
    Guid TenantId,
    Guid UserId,
    DateTime RevokedUtc,
    DateTime ExpiresUtc,
    Guid? RevokedByUserId,
    string Reason);
