using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "Get role by ID";
            s.Description = "Retrieves the details and associated permissions of a specific role.";
            s.Response(200, "Role details");
            s.Response(401, "Not authenticated");
            s.Response(404, "Role not found");
        });
        Options(x => x.WithTags("Role Administration"));
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
