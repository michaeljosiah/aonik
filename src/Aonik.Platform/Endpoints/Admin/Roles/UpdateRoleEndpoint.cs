using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Roles;

internal class UpdateRoleEndpoint : Endpoint<UpdateRoleRequest, AccessRoleDetail>
{
    private readonly IAccessManagementService _accessManagementService;

    public UpdateRoleEndpoint(IAccessManagementService accessManagementService)
    {
        _accessManagementService = accessManagementService;
    }

    public override void Configure()
    {
        Put("/admin/roles/{roleId}");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Update a role";
            s.Description = "Updates the name and description of an existing role.";
            s.Response(200, "Role updated");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
            s.Response(404, "Role not found");
        });
        Options(x => x.WithTags("Role Administration"));
    }

    public override async Task HandleAsync(UpdateRoleRequest req, CancellationToken ct)
    {
        var roleId = Route<Guid>("roleId");
        var result = await _accessManagementService.UpdateRoleAsync(roleId, req, ct);
        await Send.OkAsync(result, ct);
    }
}
