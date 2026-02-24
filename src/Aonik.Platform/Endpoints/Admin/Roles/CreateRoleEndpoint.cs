using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;

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
    }

    public override async Task HandleAsync(CreateRoleRequest req, CancellationToken ct)
    {
        var result = await _accessManagementService.CreateRoleAsync(req, ct);
        await Send.OkAsync(result, ct);
    }
}
