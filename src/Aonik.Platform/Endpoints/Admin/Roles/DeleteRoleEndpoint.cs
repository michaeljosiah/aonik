using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;

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
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var roleId = Route<Guid>("roleId");
        await _accessManagementService.DeleteRoleAsync(roleId, ct);
        await Send.OkAsync(ct);
    }
}
