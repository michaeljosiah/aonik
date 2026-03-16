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
        var requestPath = _httpContextAccessor.HttpContext?.Request.Path.Value;
        var requiredRoles = string.Join(", ", requirement.RoleNames);
        var requiredPermissions = string.Join(", ", requirement.PermissionKeys);

        if (HasRequiredRole(requirement))
        {
            _logger.LogDebug("Authorization granted via role for {Path}", requestPath);
            context.Succeed(requirement);
            return;
        }

        var userId = _currentUserContext.UserId;
        if (userId == null)
        {
            _logger.LogWarning(
                "Authorization denied for {Path}: missing user id. Required roles: [{RequiredRoles}]",
                requestPath, requiredRoles);
            return;
        }

        var httpContext = _httpContextAccessor.HttpContext;
        var cacheKey = $"UserPermissions_{userId}";

        HashSet<string> permissions;
        if (httpContext?.Items[cacheKey] is not HashSet<string> cachedPermissions)
        {
            if (httpContext?.Items["TestPermissions"] is HashSet<string> testPermissions)
            {
                permissions = testPermissions;
            }
            else
            {
                var permissionsList = await _permissionService.GetUserPermissionsAsync(
                    userId.Value,
                    httpContext?.RequestAborted ?? CancellationToken.None);
                permissions = new HashSet<string>(permissionsList, StringComparer.OrdinalIgnoreCase);
            }

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
            _logger.LogDebug("Authorization granted via permission for {Path}", requestPath);
            context.Succeed(requirement);
        }
        else
        {
            var actualRoles = string.Join(", ", _currentUserContext.Roles);
            var actualPermissions = string.Join(", ", permissions);
            _logger.LogWarning(
                "Authorization denied for {Path}: user {UserId} has roles [{ActualRoles}] and permissions [{ActualPermissions}], " +
                "but requires one of roles [{RequiredRoles}] or permissions [{RequiredPermissions}]",
                requestPath, userId, actualRoles, actualPermissions, requiredRoles, requiredPermissions);
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
