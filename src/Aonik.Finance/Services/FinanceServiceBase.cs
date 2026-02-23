using Aonik.SharedKernel.Abstractions;

namespace Aonik.Finance.Services;

/// <summary>
/// Base class for Finance module services that require permission checks.
/// Mirrors the AdminServiceBase pattern used in Platform and Application layers.
/// </summary>
internal abstract class FinanceServiceBase
{
    protected ICurrentUserProvider CurrentUserProvider { get; }
    protected IPermissionService PermissionService { get; }

    protected FinanceServiceBase(
        ICurrentUserProvider currentUserProvider,
        IPermissionService permissionService)
    {
        CurrentUserProvider = currentUserProvider;
        PermissionService = permissionService;
    }

    protected async Task EnsurePermissionAsync(string permissionKey, CancellationToken cancellationToken)
    {
        var userId = CurrentUserProvider.GetCurrentUserId();
        if (!userId.HasValue)
        {
            throw new InvalidOperationException("Authenticated user is required.");
        }

        var hasPermission = await PermissionService.HasPermissionAsync(userId.Value, permissionKey, cancellationToken);
        if (!hasPermission)
        {
            throw new InvalidOperationException($"Permission {permissionKey} is required.");
        }
    }
}
