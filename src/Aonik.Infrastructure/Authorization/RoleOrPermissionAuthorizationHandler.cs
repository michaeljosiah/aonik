using Aonik.Application.Services.Identity;
using Aonik.SharedKernel.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Aonik.Infrastructure.Authorization;

public class RoleOrPermissionAuthorizationHandler : AuthorizationHandler<RoleOrPermissionRequirement>
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IPermissionService _permissionService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<RoleOrPermissionAuthorizationHandler> _logger;

    public RoleOrPermissionAuthorizationHandler(
        ICurrentUserContext currentUserContext,
        IPermissionService permissionService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<RoleOrPermissionAuthorizationHandler> logger)
    {
        _currentUserContext = currentUserContext;
        _permissionService = permissionService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RoleOrPermissionRequirement requirement)
    {
        if (HasRequiredRole(requirement))
        {
            _logger.LogDebug("Authorization granted via role for policy");
            context.Succeed(requirement);
            return;
        }

        var userId = _currentUserContext.UserId;
        if (userId == null)
        {
            _logger.LogWarning("Authorization denied: missing user id");
            return;
        }

        var httpContext = _httpContextAccessor.HttpContext;
        var cacheKey = $"UserPermissions_{userId}";

        HashSet<string> permissions;
        if (httpContext?.Items[cacheKey] is not HashSet<string> cachedPermissions)
        {
            var permissionsList = await _permissionService.GetUserPermissionsAsync(
                userId.Value,
                httpContext?.RequestAborted ?? CancellationToken.None);
            permissions = new HashSet<string>(permissionsList, StringComparer.OrdinalIgnoreCase);
            if (httpContext != null)
            {
                httpContext.Items[cacheKey] = permissions;
            }
        }
        else
        {
            permissions = cachedPermissions;
        }

        if (requirement.PermissionKeys.Any(permission => permissions.Contains(permission)))
        {
            _logger.LogDebug("Authorization granted via permission for policy");
            context.Succeed(requirement);
        }
        else
        {
            _logger.LogWarning("Authorization denied: missing required role/permission");
        }
    }

    private bool HasRequiredRole(RoleOrPermissionRequirement requirement)
    {
        var roles = _currentUserContext.Roles;
        if (roles.Count == 0)
        {
            return false;
        }

        return roles.Any(role => requirement.RoleNames.Contains(role, StringComparer.OrdinalIgnoreCase));
    }
}
