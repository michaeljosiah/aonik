using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Roles;

internal class CreateRoleEndpoint : Endpoint<CreateRoleRequest, AccessRoleDetail>
{
    private readonly IAccessManagementService _accessManagementService;

    public CreateRoleEndpoint(IAccessManagementService accessManagementService)
    {
        _accessManagementService = accessManagementService;
    }

    public override void Configure()
    {
        Post("/admin/roles");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Create a new role";
            s.Description = "Creates a new role definition that can be assigned to users within the tenant.";
            s.Response(200, "Role created");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Role Administration"));
    }

    public override async Task HandleAsync(CreateRoleRequest req, CancellationToken ct)
    {
        var result = await _accessManagementService.CreateRoleAsync(req, ct);
        await Send.OkAsync(result, ct);
    }
}
