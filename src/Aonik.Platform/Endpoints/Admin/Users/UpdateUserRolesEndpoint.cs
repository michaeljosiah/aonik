using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Users;

internal class UpdateUserRolesEndpoint : Endpoint<UpdateUserRolesRequest>
{
    private readonly IAccessManagementService _accessManagementService;

    public UpdateUserRolesEndpoint(IAccessManagementService accessManagementService)
    {
        _accessManagementService = accessManagementService;
    }

    public override void Configure()
    {
        Put("/admin/users/{userId}/roles");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Update user role assignments";
            s.Description = "Replaces the set of roles assigned to the specified user with the provided role list.";
            s.Response(200, "Roles updated");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
            s.Response(404, "User not found");
        });
        Options(x => x.WithTags("User Administration"));
    }

    public override async Task HandleAsync(UpdateUserRolesRequest req, CancellationToken ct)
    {
        var userId = Route<Guid>("userId");
        await _accessManagementService.UpdateUserRolesAsync(userId, req, ct);
        await Send.OkAsync(ct);
    }
}
