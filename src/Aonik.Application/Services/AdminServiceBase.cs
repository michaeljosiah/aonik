using Aonik.SharedKernel.Abstractions;

namespace Aonik.Application.Services;

/// <summary>
/// Base class for Application-layer admin services that require permission checks.
/// Non-Platform services inherit from this during migration. When these services move
/// to their own modules (Finance, AI, etc.), each module will have its own base class.
/// </summary>
public abstract class AdminServiceBase
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
            throw new PermissionDeniedException(permissionKey, "Authenticated user is required.");
        }

        var hasPermission = await PermissionService.HasPermissionAsync(userId.Value, permissionKey, cancellationToken);
        if (!hasPermission)
        {
            throw new PermissionDeniedException(permissionKey);
        }
    }
}
