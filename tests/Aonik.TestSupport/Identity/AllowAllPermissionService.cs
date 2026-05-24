using Aonik.SharedKernel.Abstractions;

namespace Aonik.TestSupport.Identity;

/// <summary>
/// Permissive <see cref="IPermissionService"/> — every permission check
/// returns true. Use in tests where the code under test enforces
/// permissions via <c>EnsurePermissionAsync(...)</c> and you just want
/// to exercise the happy path without authoring a permission seed.
///
/// For permission-rejection coverage, use
/// <see cref="DenyAllPermissionService"/> or write a per-test bespoke
/// implementation.
/// </summary>
public sealed class AllowAllPermissionService : IPermissionService
{
    public Task<bool> HasPermissionAsync(Guid userId, string permissionKey, CancellationToken ct = default) =>
        Task.FromResult(true);

    public Task<List<string>> GetUserPermissionsAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult(new List<string>());
}
