using Aonik.SharedKernel.Abstractions;

namespace Aonik.Ai.Services;

/// <summary>
/// Base class for AI module services that require permission checks.
/// Module-specific replacement for Application-layer AdminServiceBase.
/// </summary>
internal abstract class AiServiceBase
{
    protected ICurrentUserProvider CurrentUserProvider { get; }
    protected IPermissionService PermissionService { get; }

    protected AiServiceBase(
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
