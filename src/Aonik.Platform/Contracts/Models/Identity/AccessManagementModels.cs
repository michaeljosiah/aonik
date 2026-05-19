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
/// confirm the placeholder was created. After Spec 026 Part 1 the
/// invite email is sent inline as part of the InviteUser pipeline,
/// so <c>EmailSent</c> + <c>ExpiresUtc</c> let the UI report when
/// the message went out and when it stops working.
/// </summary>
public record InviteUserResponse(
    Guid UserId,
    Guid TenantId,
    string Email,
    string? DisplayName,
    List<Guid> AssignedRoleIds,
    bool EmailSent = false,
    DateTime? ExpiresUtc = null,
    int EmailSendCount = 0);

public record ResendInviteResponse(
    Guid UserId,
    string Email,
    bool EmailSent,
    DateTime? ExpiresUtc,
    int EmailSendCount,
    string? RateLimitReason);

/// <summary>
/// Posted by the invitee's front-end at <c>/identity/invite/accept</c>.
/// Anonymous: authentication is the combination of (a) a valid IdP
/// bearer token in the Authorization header and (b) the one-shot
/// invite token issued at invite time.
/// </summary>
public record AcceptInviteRequest(string InviteToken);

public record AcceptInviteResponse(
    Guid UserId,
    Guid TenantId,
    string Email,
    bool Accepted,
    string? FailureReason);

public record RevokeUserSessionsRequest(string? Reason);

public record RevokeUserSessionsResponse(
    Guid UserId,
    DateTime RevokedUtc,
    DateTime ExpiresUtc,
    string Reason);

public record DeleteUserRequest(
    string EmailConfirmation,
    string Reason);

public record DeleteUserResponse(
    Guid TombstoneId,
    Guid OriginalUserId,
    DateTime DeletedUtc,
    int AuditRowsRedacted,
    bool IdentityProviderUserDeleted);

public record UserTombstoneSummary(
    Guid TombstoneId,
    Guid OriginalUserId,
    DateTime DeletedUtc,
    Guid? DeletedByUserId,
    string? DeletedByEmail,
    string Reason,
    string? MaskedEmail,
    int AuditRowsRedacted);

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
