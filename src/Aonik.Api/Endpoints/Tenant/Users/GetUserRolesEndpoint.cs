using Aonik.Api.Contracts.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Tenant.Users;

public class GetUserRolesEndpoint : EndpointWithoutRequest<UserRoleResponse>
{
    private readonly IUserRoleService _userRoleService;

    public GetUserRolesEndpoint(IUserRoleService userRoleService)
    {
        _userRoleService = userRoleService;
    }

    public override void Configure()
    {
        Get("/tenant/users/{userId}/roles");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = Route<Guid>("userId");

        var result = await _userRoleService.GetUserRolesAsync(userId, ct);

        await Send.OkAsync(MapResponse(result), ct);
    }

    private static UserRoleResponse MapResponse(Aonik.Platform.Contracts.Models.Identity.UserRoleResponse result)
    {
        return new UserRoleResponse(
            result.UserId,
            result.Roles.Select(role => new RoleSummaryResponse(role.RoleId, role.Name)).ToList());
    }
}
