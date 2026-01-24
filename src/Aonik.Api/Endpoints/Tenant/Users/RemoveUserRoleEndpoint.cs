using Aonik.Api.Contracts.Identity;
using Aonik.Application.Services.Identity;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Tenant.Users;

public class RemoveUserRoleEndpoint : EndpointWithoutRequest<UserRoleResponse>
{
    private readonly IUserRoleService _userRoleService;

    public RemoveUserRoleEndpoint(IUserRoleService userRoleService)
    {
        _userRoleService = userRoleService;
    }

    public override void Configure()
    {
        Delete("/tenant/users/{userId}/roles/{roleId}");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = Route<Guid>("userId");
        var roleId = Route<Guid>("roleId");

        var result = await _userRoleService.RemoveRoleAsync(userId, roleId, ct);

        await Send.OkAsync(MapResponse(result), ct);
    }

    private static UserRoleResponse MapResponse(Application.Models.Identity.UserRoleResponse result)
    {
        return new UserRoleResponse(
            result.UserId,
            result.Roles.Select(role => new RoleSummaryResponse(role.RoleId, role.Name)).ToList());
    }

}
