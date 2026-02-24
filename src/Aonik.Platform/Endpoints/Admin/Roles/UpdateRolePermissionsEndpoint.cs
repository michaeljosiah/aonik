using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;

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
    }

    public override async Task HandleAsync(UpdateRolePermissionsRequest req, CancellationToken ct)
    {
        var roleId = Route<Guid>("roleId");
        await _accessManagementService.UpdateRolePermissionsAsync(roleId, req, ct);
        await Send.OkAsync(ct);
    }
}
