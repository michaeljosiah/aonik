using Aonik.Application.Models.Identity;
using Aonik.Application.Services.Identity;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Admin.Roles;

public class UpdateRolePermissionsEndpoint : Endpoint<UpdateRolePermissionsRequest>
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
    }

    public override async Task HandleAsync(UpdateRolePermissionsRequest req, CancellationToken ct)
    {
        var roleId = Route<Guid>("roleId");
        await _accessManagementService.UpdateRolePermissionsAsync(roleId, req, ct);
        await Send.OkAsync(ct);
    }
}
