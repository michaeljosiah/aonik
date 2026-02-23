using Aonik.Platform.Contracts.Api.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;

namespace Aonik.Platform.Endpoints.Tenant.Users;

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

    private static UserRoleResponse MapResponse(Aonik.Platform.Contracts.Models.Identity.UserRoleResponse result)
    {
        return new UserRoleResponse(
            result.UserId,
            result.Roles.Select(role => new RoleSummaryResponse(role.RoleId, role.Name)).ToList());
    }

}
