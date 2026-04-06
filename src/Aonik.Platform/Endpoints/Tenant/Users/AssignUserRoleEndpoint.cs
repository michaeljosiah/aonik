using Aonik.Platform.Contracts.Api.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Tenant.Users;

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
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Assign a role to a user";
            s.Description = "Assigns a specific role to a user within the current tenant and returns the user's updated role list.";
            s.Response(200, "Role assigned");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("User Administration"));
    }

    public override async Task HandleAsync(AssignUserRoleRequest req, CancellationToken ct)
    {
        var userId = Route<Guid>("userId");

        var result = await _userRoleService.AssignRoleAsync(userId, req.RoleId, ct);

        await Send.OkAsync(MapResponse(result), ct);
    }

    private static UserRoleResponse MapResponse(Aonik.Platform.Contracts.Models.Identity.UserRoleResponse result)
    {
        return new UserRoleResponse(
            result.UserId,
            result.Roles.Select(role => new RoleSummaryResponse(role.RoleId, role.Name)).ToList());
    }
}
