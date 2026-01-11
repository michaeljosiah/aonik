namespace Aonik.Application.Models.Identity;

public record UserRoleAssignmentRequest(Guid UserId, Guid RoleId);

public record UserRoleResponse(Guid UserId, List<RoleSummary> Roles);

public record RoleSummary(Guid RoleId, string Name);
