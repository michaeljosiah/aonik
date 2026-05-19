using Aonik.Platform.Contracts.Models.Identity;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Platform.Contracts.Services.Identity;

public interface IAccessManagementService
{
    Task<PagedResult<AccessUserSummary>> ListUsersAsync(ListUsersRequest request, CancellationToken cancellationToken = default);
    Task<AccessUserDetail?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<InviteUserResponse> InviteUserAsync(InviteUserRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Spec 026 Part 1 — re-send the invite email for a placeholder that
    /// has not yet been accepted. Regenerates the token, increments the
    /// send counter, and writes a <c>UserInviteLog</c> row. Enforces the
    /// per-user / 24-hour soft rate limit.
    /// </summary>
    Task<ResendInviteResponse> ResendInviteAsync(Guid userId, CancellationToken cancellationToken = default);

    Task UpdateUserRolesAsync(Guid userId, UpdateUserRolesRequest request, CancellationToken cancellationToken = default);
    Task UpdateUserProfileAsync(Guid userId, UpdateUserProfileRequest request, CancellationToken cancellationToken = default);
    Task<CustomerPhotoUploadResponse?> UploadUserPhotoAsync(Guid userId, Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default);
    Task<CustomerPhotoDeleteResponse?> DeleteUserPhotoAsync(Guid userId, CancellationToken cancellationToken = default);
    Task ActivateUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task DeactivateUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Spec 026 Part 3 — adds a row to <c>AnkPlatformUserSessionBlocklist</c>,
    /// invalidates the per-user FusionCache entry, and writes an audit
    /// event. Subsequent requests with tokens issued before the revoke
    /// time return 401 within one cache window.
    /// </summary>
    Task<RevokeUserSessionsResponse> RevokeSessionsAsync(Guid userId, RevokeUserSessionsRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Spec 026 Part 2 — hard delete with PII redaction in audit logs
    /// and a delete of the corresponding IdP user record. Requires the
    /// operator to type the user's email back and supply a deletion
    /// reason. Records a <c>UserTombstone</c> for compliance review.
    /// </summary>
    Task<DeleteUserResponse> DeleteUserAsync(Guid userId, DeleteUserRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Spec 026 Part 2 — paged tombstone list, surfaced under the
    /// Compliance UI. Permission: <c>Users.Read</c> (compliance review
    /// is read-only; deletion itself is gated by <c>Users.Delete</c>).
    /// </summary>
    Task<PagedResult<UserTombstoneSummary>> ListTombstonesAsync(ListUsersRequest request, CancellationToken cancellationToken = default);

    Task<UserDiagnosticResult> DiagnoseUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserRepairResult> RepairUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<PagedResult<AccessRoleSummary>> ListRolesAsync(ListRolesRequest request, CancellationToken cancellationToken = default);
    Task<AccessRoleDetail?> GetRoleAsync(Guid roleId, CancellationToken cancellationToken = default);
    Task<AccessRoleDetail> CreateRoleAsync(CreateRoleRequest request, CancellationToken cancellationToken = default);
    Task<AccessRoleDetail> UpdateRoleAsync(Guid roleId, UpdateRoleRequest request, CancellationToken cancellationToken = default);
    Task DeleteRoleAsync(Guid roleId, CancellationToken cancellationToken = default);
    Task UpdateRolePermissionsAsync(Guid roleId, UpdateRolePermissionsRequest request, CancellationToken cancellationToken = default);
    Task<List<PermissionDefinition>> ListPermissionsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Spec 026 Part 1 — separate from <see cref="IAccessManagementService"/>
/// because the accept-invite endpoint is anonymous (the user is not yet
/// a known principal at the time they're consuming the token).
/// </summary>
public interface IInviteAcceptanceService
{
    Task<AcceptInviteResponse> AcceptInviteAsync(
        AcceptInviteRequest request,
        string externalIssuer,
        string externalSubject,
        string? externalTenantId,
        string? email,
        CancellationToken cancellationToken = default);
}
