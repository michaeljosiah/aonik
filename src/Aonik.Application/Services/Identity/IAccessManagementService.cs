using Aonik.Application.Models.Identity;

namespace Aonik.Application.Services.Identity;

public interface IAccessManagementService
{
    Task<PagedResult<AccessUserSummary>> ListUsersAsync(ListUsersRequest request, CancellationToken cancellationToken = default);
    Task<AccessUserDetail?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task InviteUserAsync(InviteUserRequest request, CancellationToken cancellationToken = default);
    Task UpdateUserRolesAsync(Guid userId, UpdateUserRolesRequest request, CancellationToken cancellationToken = default);
    Task UpdateUserProfileAsync(Guid userId, UpdateUserProfileRequest request, CancellationToken cancellationToken = default);
    Task ActivateUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task DeactivateUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<PagedResult<AccessRoleSummary>> ListRolesAsync(ListRolesRequest request, CancellationToken cancellationToken = default);
    Task<AccessRoleDetail?> GetRoleAsync(Guid roleId, CancellationToken cancellationToken = default);
    Task<AccessRoleDetail> CreateRoleAsync(CreateRoleRequest request, CancellationToken cancellationToken = default);
    Task<AccessRoleDetail> UpdateRoleAsync(Guid roleId, UpdateRoleRequest request, CancellationToken cancellationToken = default);
    Task DeleteRoleAsync(Guid roleId, CancellationToken cancellationToken = default);
    Task UpdateRolePermissionsAsync(Guid roleId, UpdateRolePermissionsRequest request, CancellationToken cancellationToken = default);
    Task<List<PermissionDefinition>> ListPermissionsAsync(CancellationToken cancellationToken = default);
}
