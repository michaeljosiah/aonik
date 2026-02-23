using Aonik.Platform.Contracts.Models.Identity;
using Aonik.SharedKernel.Abstractions;
using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Admin.Roles;

public class ListRolesEndpoint : Endpoint<ListRolesRequest, PagedResult<AccessRoleSummary>>
{
    private readonly IAccessManagementService _accessManagementService;

    public ListRolesEndpoint(IAccessManagementService accessManagementService)
    {
        _accessManagementService = accessManagementService;
    }

    public override void Configure()
    {
        Get("/admin/roles");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(ListRolesRequest req, CancellationToken ct)
    {
        var result = await _accessManagementService.ListRolesAsync(req, ct);
        await Send.OkAsync(result, ct);
    }
}
