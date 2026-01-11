using Aonik.Api.Contracts.Identity;
using Aonik.Application.Services.Identity;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Tenant.Users;

public class AssignUserRoleEndpoint : Endpoint<AssignUserRoleRequest, UserRoleResponse>
{
    private readonly IUserRoleService _userRoleService;

    public AssignUserRoleEndpoint(IUserRoleService userRoleService)
    {
        _userRoleService = userRoleService;
    }

    public override void Configure()
    {
        Post("/tenant/users/{userId}/roles");
        Policies("TenantAdmin");
    }

    public override async Task HandleAsync(AssignUserRoleRequest req, CancellationToken ct)
    {
        var userId = Route<Guid>("userId");

        var result = await _userRoleService.AssignRoleAsync(userId, req.RoleId, ct);

        await Send.OkAsync(MapResponse(result), ct);
    }

    private static UserRoleResponse MapResponse(Application.Models.Identity.UserRoleResponse result)
    {
        return new UserRoleResponse(
            result.UserId,
            result.Roles.Select(role => new RoleSummaryResponse(role.RoleId, role.Name)).ToList());
    }
}
