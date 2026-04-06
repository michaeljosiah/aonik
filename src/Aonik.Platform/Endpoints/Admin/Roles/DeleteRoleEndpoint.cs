using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Roles;

internal class DeleteRoleEndpoint : EndpointWithoutRequest
{
    private readonly IAccessManagementService _accessManagementService;

    public DeleteRoleEndpoint(IAccessManagementService accessManagementService)
    {
        _accessManagementService = accessManagementService;
    }

    public override void Configure()
    {
        Delete("/admin/roles/{roleId}");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Delete a role";
            s.Description = "Permanently removes a role definition. Users currently assigned this role will lose its permissions.";
            s.Response(200, "Role deleted");
            s.Response(401, "Not authenticated");
            s.Response(404, "Role not found");
        });
        Options(x => x.WithTags("Role Administration"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var roleId = Route<Guid>("roleId");
        await _accessManagementService.DeleteRoleAsync(roleId, ct);
        await Send.OkAsync(ct);
    }
}
