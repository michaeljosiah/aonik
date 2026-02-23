using Aonik.Platform.Contracts.Models.Identity;

namespace Aonik.Platform.Contracts.Services.Identity;

public interface IUserRoleService
{
    Task<UserRoleResponse> GetUserRolesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserRoleResponse> AssignRoleAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default);
    Task<UserRoleResponse> RemoveRoleAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default);
}
