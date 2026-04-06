using Aonik.Platform.Contracts.Models.Identity;
using Aonik.SharedKernel.Abstractions;
using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Roles;

internal class ListRolesEndpoint : Endpoint<ListRolesRequest, PagedResult<AccessRoleSummary>>
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
        Summary(s =>
        {
            s.Summary = "List all roles";
            s.Description = "Returns a paginated list of roles defined for the current tenant.";
            s.Response(200, "Paged role list");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Role Administration"));
    }

    public override async Task HandleAsync(ListRolesRequest req, CancellationToken ct)
    {
        var result = await _accessManagementService.ListRolesAsync(req, ct);
        await Send.OkAsync(result, ct);
    }
}
