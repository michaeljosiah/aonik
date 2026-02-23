using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Admin.Users;

public class ListUsersEndpoint : Endpoint<ListUsersRequest, PagedResult<AccessUserSummary>>
{
    private readonly IAccessManagementService _accessManagementService;

    public ListUsersEndpoint(IAccessManagementService accessManagementService)
    {
        _accessManagementService = accessManagementService;
    }

    public override void Configure()
    {
        Get("/admin/users");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(ListUsersRequest req, CancellationToken ct)
    {
        var result = await _accessManagementService.ListUsersAsync(req, ct);
        await Send.OkAsync(result, ct);
    }
}
