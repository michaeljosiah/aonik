using Aonik.SharedKernel.Abstractions;

namespace Aonik.Platform.Services;

internal abstract class AdminServiceBase
{
    protected ICurrentUserProvider CurrentUserProvider { get; }
    protected IPermissionService PermissionService { get; }

    protected AdminServiceBase(
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
