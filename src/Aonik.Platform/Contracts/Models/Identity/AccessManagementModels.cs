namespace Aonik.Platform.Contracts.Models.Identity;

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
    List<Guid>? RoleIds = null,
    string? DisplayName = null
);

/// <summary>
/// Result of a successful invite. Returned to the admin so they can
/// confirm the placeholder was created and (later) trigger a notify
/// flow if email delivery is wired up.
/// </summary>
public record InviteUserResponse(
    Guid UserId,
    Guid TenantId,
    string Email,
    string? DisplayName,
    List<Guid> AssignedRoleIds);

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
    string? PartyLinkType,
    string? PhotoUrl,
    string? PhotoUrlSmall,
    string? PhotoUrlTiny
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
    string? PartyLinkType,
    PersonProfileDetail? PersonProfile,
    BusinessProfileDetail? BusinessProfile,
    List<PartyContactDetail> Contacts,
    List<PartyAddressDetail> Addresses
);

public record PersonProfileDetail(
    string? Title,
    string? FirstName,
    string? LastName,
    string? CountryCode,
    string? PhotoUrl,
    DateTime? Dob,
    string? Nationality,
    string? Occupation,
    string IdvStatus
);

public record BusinessProfileDetail(
    string? RegistrationNumber,
    string? IncorporationCountry,
    string? Industry,
    string KybStatus
);

public record PartyContactDetail(
    Guid ContactId,
    string Type,
    string Value,
    bool IsPrimary
);

public record PartyAddressDetail(
    Guid AddressId,
    string Type,
    string Line1,
    string? Line2,
    string? Line3,
    string City,
    string? State,
    string Postcode,
    string Country
);

public record UpdateUserProfileRequest(
    string? FirstName,
    string? LastName,
    string? Title,
    string? CountryCode,
    string? Nationality,
    string? Occupation
);

public record UserDiagnosticResult(
    Guid UserId,
    bool HasIssues,
    List<UserDiagnosticIssue> Issues
);

public record UserDiagnosticIssue(
    string Code,
    string Description,
    bool Repairable
);

public record UserRepairResult(
    Guid UserId,
    List<string> RepairsApplied
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
