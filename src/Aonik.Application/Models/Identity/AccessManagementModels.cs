namespace Aonik.Application.Models.Identity;

public record ListUsersRequest(
    int PageNumber = 1,
    int PageSize = 20,
    string? Status = null,
    string? Search = null
);

public record ListRolesRequest(
    int PageNumber = 1,
    int PageSize = 20,
    string? Search = null
);

public record InviteUserRequest(
    string Email,
    List<Guid>? RoleIds = null
);

public record UpdateUserRolesRequest(
    List<Guid> RoleIds
);

public record CreateRoleRequest(
    string Name,
    string? Description,
    List<string> PermissionKeys
);

public record UpdateRoleRequest(
    string? Name,
    string? Description
);

public record UpdateRolePermissionsRequest(
    List<string> PermissionKeys
);

public record AccessUserSummary(
    Guid UserId,
    string Email,
    string? DisplayName,
    string Status,
    DateTime? LastLoginAt,
    int RoleCount,
    Guid? PartyId,
    string? PartyDisplayName,
    string? PartyType,
    string? PartyLinkType
);

public record AccessUserDetail(
    Guid UserId,
    string Email,
    string? DisplayName,
    string Status,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    List<RoleSummary> Roles,
    List<string> Permissions,
    Guid? PartyId,
    string? PartyDisplayName,
    string? PartyType,
    string? PartyLinkType
);

public record PermissionDefinition(
    string Key,
    string? Description,
    string Category
);

public record AccessRoleSummary(
    Guid RoleId,
    string Name,
    string? Description,
    int PermissionCount,
    int UserCount
);

public record AccessRoleDetail(
    Guid RoleId,
    string Name,
    string? Description,
    List<PermissionDefinition> Permissions,
    List<AccessUserSummary> Users
);
