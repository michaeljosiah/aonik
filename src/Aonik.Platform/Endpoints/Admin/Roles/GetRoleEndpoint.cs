using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;

namespace Aonik.Platform.Endpoints.Admin.Roles;

internal class GetRoleEndpoint : EndpointWithoutRequest<AccessRoleDetail>
{
    private readonly IAccessManagementService _accessManagementService;

    public GetRoleEndpoint(IAccessManagementService accessManagementService)
    {
        _accessManagementService = accessManagementService;
    }

    public override void Configure()
    {
        Get("/admin/roles/{roleId}");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var roleId = Route<Guid>("roleId");
        var result = await _accessManagementService.GetRoleAsync(roleId, ct);

        if (result == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}
