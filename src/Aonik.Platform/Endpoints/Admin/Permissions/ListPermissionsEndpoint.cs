using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;

namespace Aonik.Platform.Endpoints.Admin.Permissions;

internal class ListPermissionsEndpoint : EndpointWithoutRequest<List<PermissionDefinition>>
{
    private readonly IAccessManagementService _accessManagementService;

    public ListPermissionsEndpoint(IAccessManagementService accessManagementService)
    {
        _accessManagementService = accessManagementService;
    }

    public override void Configure()
    {
        Get("/admin/permissions");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await _accessManagementService.ListPermissionsAsync(ct);
        await Send.OkAsync(result, ct);
    }
}
