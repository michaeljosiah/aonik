using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Roles;

internal class UpdateRolePermissionsEndpoint : Endpoint<UpdateRolePermissionsRequest>
{
    private readonly IAccessManagementService _accessManagementService;

    public UpdateRolePermissionsEndpoint(IAccessManagementService accessManagementService)
    {
        _accessManagementService = accessManagementService;
    }

    public override void Configure()
    {
        Put("/admin/roles/{roleId}/permissions");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Update role permissions";
            s.Description = "Replaces the full set of permissions assigned to the specified role.";
            s.Response(200, "Permissions updated");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
            s.Response(404, "Role not found");
        });
        Options(x => x.WithTags("Role Administration"));
    }

    public override async Task HandleAsync(UpdateRolePermissionsRequest req, CancellationToken ct)
    {
        var roleId = Route<Guid>("roleId");
        await _accessManagementService.UpdateRolePermissionsAsync(roleId, req, ct);
        await Send.OkAsync(ct);
    }
}
