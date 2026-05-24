using Aonik.SharedKernel.Abstractions;

namespace Aonik.TestSupport.Identity;

/// <summary>
/// Restrictive <see cref="IPermissionService"/> — every permission
/// check returns false. Use when the test scenario is specifically
/// "operator lacks the required permission and the service must
/// reject the call".
/// </summary>
public sealed class DenyAllPermissionService : IPermissionService
{
    public Task<bool> HasPermissionAsync(Guid userId, string permissionKey, CancellationToken ct = default) =>
        Task.FromResult(false);

    public Task<List<string>> GetUserPermissionsAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult(new List<string>());
}
