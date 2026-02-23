using Aonik.SharedKernel.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Aonik.Infrastructure.Authorization;

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IPermissionService _permissionService;
    private readonly ILogger<PermissionAuthorizationHandler> _logger;

    // CRITICAL: Registered as Scoped, can inject scoped dependencies directly
    public PermissionAuthorizationHandler(
        IHttpContextAccessor httpContextAccessor,
        IPermissionService permissionService,
        ILogger<PermissionAuthorizationHandler> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _permissionService = permissionService;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        // Get user ID from HttpContext.Items (set by OnTokenValidated)
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext?.Items["AonikUserId"] is not Guid userId)
        {
            _logger.LogWarning("Permission check failed: User ID not found in context");
            return; // Fail (do not call context.Succeed)
        }

        // Cache permissions in HttpContext for request lifetime
        var cacheKey = $"UserPermissions_{userId}";
        HashSet<string> permissions;

        if (httpContext.Items[cacheKey] is not HashSet<string> cachedPermissions)
        {
            if (httpContext.Items["TestPermissions"] is HashSet<string> testPermissions)
            {
                permissions = testPermissions;
            }
            else
            {
                var permissionsList = await _permissionService.GetUserPermissionsAsync(userId, httpContext.RequestAborted);
                permissions = new HashSet<string>(permissionsList);
            }

            httpContext.Items[cacheKey] = permissions;
        }
        else
        {
            permissions = cachedPermissions;
        }

        if (permissions.Contains(requirement.PermissionKey))
        {
            _logger.LogDebug("User {UserId} granted permission {Permission}", userId, requirement.PermissionKey);
            context.Succeed(requirement);
        }
        else
        {
            _logger.LogWarning("User {UserId} denied permission {Permission}", userId, requirement.PermissionKey);
        }
    }
}
