namespace Aonik.Application.Services.Identity;

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(Guid userId, string permissionKey, CancellationToken ct = default);
    Task<List<string>> GetUserPermissionsAsync(Guid userId, CancellationToken ct = default);
}
