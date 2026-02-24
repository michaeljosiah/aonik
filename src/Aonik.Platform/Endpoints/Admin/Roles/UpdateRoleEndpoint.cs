using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;

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
    }

    public override async Task HandleAsync(UpdateRoleRequest req, CancellationToken ct)
    {
        var roleId = Route<Guid>("roleId");
        var result = await _accessManagementService.UpdateRoleAsync(roleId, req, ct);
        await Send.OkAsync(result, ct);
    }
}
