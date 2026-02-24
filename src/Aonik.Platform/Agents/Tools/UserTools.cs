using System.ComponentModel;
using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.SharedKernel.Abstractions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Platform.Agents.Tools;

/// <summary>
/// AI agent tools for user and role management operations.
/// Read-only tools are safe for autonomous use; mutating tools should go through
/// the proposal pattern at the agent level.
/// </summary>
internal sealed class UserTools
{
    private readonly IAccessManagementService _accessManagementService;
    private readonly IUserRoleService _userRoleService;

    private UserTools(
        IAccessManagementService accessManagementService,
        IUserRoleService userRoleService)
    {
        _accessManagementService = accessManagementService;
        _userRoleService = userRoleService;
    }

    [Description("Retrieves a user by their unique identifier. Returns full user details including roles, permissions, profile, contacts, and addresses.")]
    public async Task<AccessUserDetail?> GetUser(
        [Description("The unique identifier (GUID) of the user to retrieve")] Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _accessManagementService.GetUserAsync(userId, cancellationToken);
    }

    [Description("Lists users in the current tenant with optional filtering by status or search term. Returns a paged result.")]
    public async Task<PagedResult<AccessUserSummary>> ListUsers(
        [Description("Page number (1-based, default 1)")] int pageNumber = 1,
        [Description("Page size (default 20)")] int pageSize = 20,
        [Description("Filter by status (e.g. Active, Inactive)")] string? status = null,
        [Description("Search by name or email (partial match)")] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var request = new ListUsersRequest(pageNumber, pageSize, status, search);
        return await _accessManagementService.ListUsersAsync(request, cancellationToken);
    }

    [Description("Gets the roles assigned to a specific user. Returns the user's role list.")]
    public async Task<UserRoleResponse> GetUserRoles(
        [Description("The unique identifier (GUID) of the user")] Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _userRoleService.GetUserRolesAsync(userId, cancellationToken);
    }

    [Description("Lists all roles in the current tenant with optional search. Returns a paged result with role name, description, and counts.")]
    public async Task<PagedResult<AccessRoleSummary>> ListRoles(
        [Description("Page number (1-based, default 1)")] int pageNumber = 1,
        [Description("Page size (default 20)")] int pageSize = 20,
        [Description("Search by role name (partial match)")] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var request = new ListRolesRequest(pageNumber, pageSize, search);
        return await _accessManagementService.ListRolesAsync(request, cancellationToken);
    }

    [Description("Retrieves a role by its unique identifier. Returns full role details including permissions and assigned users.")]
    public async Task<AccessRoleDetail?> GetRole(
        [Description("The unique identifier (GUID) of the role to retrieve")] Guid roleId,
        CancellationToken cancellationToken = default)
    {
        return await _accessManagementService.GetRoleAsync(roleId, cancellationToken);
    }

    [Description("Lists all available permission definitions. Returns permission keys, descriptions, and categories.")]
    public async Task<List<PermissionDefinition>> ListPermissions(
        CancellationToken cancellationToken = default)
    {
        return await _accessManagementService.ListPermissionsAsync(cancellationToken);
    }

    /// <summary>
    /// Creates <see cref="AITool"/> instances for all user/role tools.
    /// </summary>
    public static IEnumerable<AITool> CreateAll(IServiceProvider serviceProvider)
    {
        var tools = new UserTools(
            serviceProvider.GetRequiredService<IAccessManagementService>(),
            serviceProvider.GetRequiredService<IUserRoleService>());

        yield return AIFunctionFactory.Create(tools.GetUser, name: "platform_get_user");
        yield return AIFunctionFactory.Create(tools.ListUsers, name: "platform_list_users");
        yield return AIFunctionFactory.Create(tools.GetUserRoles, name: "platform_get_user_roles");
        yield return AIFunctionFactory.Create(tools.ListRoles, name: "platform_list_roles");
        yield return AIFunctionFactory.Create(tools.GetRole, name: "platform_get_role");
        yield return AIFunctionFactory.Create(tools.ListPermissions, name: "platform_list_permissions");
    }
}
