using System.ComponentModel;
using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.SharedKernel.Abstractions;
using ModelContextProtocol.Server;

namespace Aonik.Platform.Mcp.Tools;

/// <summary>
/// MCP tools for user and role management operations.
/// Read-only tools for agent reasoning; mutating operations go through proposal pattern.
/// Domain services are injected via DI into method parameters.
/// </summary>
[McpServerToolType]
public static class UserMcpTools
{
    [McpServerTool(Name = "platform_get_user"), Description("Retrieves a user by their unique identifier. Returns full user details including roles, permissions, profile, contacts, and addresses.")]
    public static async Task<AccessUserDetail?> GetUser(
        IAccessManagementService accessManagementService,
        [Description("The unique identifier (GUID) of the user to retrieve")] Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await accessManagementService.GetUserAsync(userId, cancellationToken);
    }

    [McpServerTool(Name = "platform_list_users"), Description("Lists users in the current tenant with optional filtering by status or search term. Returns a paged result.")]
    public static async Task<PagedResult<AccessUserSummary>> ListUsers(
        IAccessManagementService accessManagementService,
        [Description("Page number (1-based, default 1)")] int pageNumber = 1,
        [Description("Page size (default 20)")] int pageSize = 20,
        [Description("Filter by status (e.g. Active, Inactive)")] string? status = null,
        [Description("Search by name or email (partial match)")] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var request = new ListUsersRequest(pageNumber, pageSize, status, search);
        return await accessManagementService.ListUsersAsync(request, cancellationToken);
    }

    [McpServerTool(Name = "platform_get_user_roles"), Description("Gets the roles assigned to a specific user. Returns the user's role list.")]
    public static async Task<UserRoleResponse> GetUserRoles(
        IUserRoleService userRoleService,
        [Description("The unique identifier (GUID) of the user")] Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await userRoleService.GetUserRolesAsync(userId, cancellationToken);
    }

    [McpServerTool(Name = "platform_list_roles"), Description("Lists all roles in the current tenant with optional search. Returns a paged result with role name, description, and counts.")]
    public static async Task<PagedResult<AccessRoleSummary>> ListRoles(
        IAccessManagementService accessManagementService,
        [Description("Page number (1-based, default 1)")] int pageNumber = 1,
        [Description("Page size (default 20)")] int pageSize = 20,
        [Description("Search by role name (partial match)")] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var request = new ListRolesRequest(pageNumber, pageSize, search);
        return await accessManagementService.ListRolesAsync(request, cancellationToken);
    }

    [McpServerTool(Name = "platform_get_role"), Description("Retrieves a role by its unique identifier. Returns full role details including permissions and assigned users.")]
    public static async Task<AccessRoleDetail?> GetRole(
        IAccessManagementService accessManagementService,
        [Description("The unique identifier (GUID) of the role to retrieve")] Guid roleId,
        CancellationToken cancellationToken = default)
    {
        return await accessManagementService.GetRoleAsync(roleId, cancellationToken);
    }

    [McpServerTool(Name = "platform_list_permissions"), Description("Lists all available permission definitions. Returns permission keys, descriptions, and categories.")]
    public static async Task<List<PermissionDefinition>> ListPermissions(
        IAccessManagementService accessManagementService,
        CancellationToken cancellationToken = default)
    {
        return await accessManagementService.ListPermissionsAsync(cancellationToken);
    }
}
