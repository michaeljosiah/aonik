using Aonik.Application.Models.Identity;

namespace Aonik.Application.Services.Identity;

public interface IUserRoleService
{
    Task<UserRoleResponse> GetUserRolesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserRoleResponse> AssignRoleAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default);
    Task<UserRoleResponse> RemoveRoleAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default);
}
