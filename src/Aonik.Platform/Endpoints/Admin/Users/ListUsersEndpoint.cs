using Aonik.Platform.Contracts.Models.Identity;
using Aonik.SharedKernel.Abstractions;
using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Users;

internal class ListUsersEndpoint : Endpoint<ListUsersRequest, PagedResult<AccessUserSummary>>
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
        Summary(s =>
        {
            s.Summary = "List all users";
            s.Description = "Returns a paginated list of users for the current tenant with optional filtering.";
            s.Response(200, "Paged user list");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("User Administration"));
    }

    public override async Task HandleAsync(ListUsersRequest req, CancellationToken ct)
    {
        var result = await _accessManagementService.ListUsersAsync(req, ct);
        await Send.OkAsync(result, ct);
    }
}
