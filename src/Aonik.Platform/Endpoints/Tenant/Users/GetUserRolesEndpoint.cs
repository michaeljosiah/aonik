using Aonik.Platform.Contracts.Api.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Tenant.Users;

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
        Summary(s =>
        {
            s.Summary = "Get roles for a user";
            s.Description = "Returns all roles currently assigned to a user within the current tenant.";
            s.Response(200, "Success");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("User Administration"));
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
