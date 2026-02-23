namespace Aonik.Platform.Contracts.Api.Identity;

public record AssignUserRoleRequest(Guid RoleId);

public record UserRoleResponse(Guid UserId, List<RoleSummaryResponse> Roles);

public record RoleSummaryResponse(Guid RoleId, string Name);
