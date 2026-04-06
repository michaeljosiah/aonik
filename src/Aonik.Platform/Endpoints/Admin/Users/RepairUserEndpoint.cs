using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Users;

internal class RepairUserEndpoint : EndpointWithoutRequest<UserRepairResult>
{
    private readonly IAccessManagementService _accessManagementService;

    public RepairUserEndpoint(IAccessManagementService accessManagementService)
    {
        _accessManagementService = accessManagementService;
    }

    public override void Configure()
    {
        Post("/admin/users/{userId}/repair");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Repair user account";
            s.Description = "Attempts to automatically fix detected issues with a user account such as missing identity provider links or stale data.";
            s.Response(200, "Repair result");
            s.Response(401, "Not authenticated");
            s.Response(404, "User not found");
        });
        Options(x => x.WithTags("User Administration"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = Route<Guid>("userId");
        var result = await _accessManagementService.RepairUserAsync(userId, ct);
        await Send.OkAsync(result, ct);
    }
}
