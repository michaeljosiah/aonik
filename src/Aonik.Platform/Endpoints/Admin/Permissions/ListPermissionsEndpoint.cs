using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "List all permission definitions";
            s.Description = "Returns the complete catalog of permission definitions available for role assignment.";
            s.Response(200, "Permission list");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Role Administration"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await _accessManagementService.ListPermissionsAsync(ct);
        await Send.OkAsync(result, ct);
    }
}
