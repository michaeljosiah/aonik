using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Users;

internal class GetUserEndpoint : EndpointWithoutRequest<AccessUserDetail>
{
    private readonly IAccessManagementService _accessManagementService;

    public GetUserEndpoint(IAccessManagementService accessManagementService)
    {
        _accessManagementService = accessManagementService;
    }

    public override void Configure()
    {
        Get("/admin/users/{userId}");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Get user by ID";
            s.Description = "Retrieves detailed information about a specific user including their profile, roles, and status.";
            s.Response(200, "User details");
            s.Response(401, "Not authenticated");
            s.Response(404, "User not found");
        });
        Options(x => x.WithTags("User Administration"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = Route<Guid>("userId");
        var result = await _accessManagementService.GetUserAsync(userId, ct);

        if (result == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}
