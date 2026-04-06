using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Users;

internal class ActivateUserEndpoint : EndpointWithoutRequest
{
    private readonly IAccessManagementService _accessManagementService;

    public ActivateUserEndpoint(IAccessManagementService accessManagementService)
    {
        _accessManagementService = accessManagementService;
    }

    public override void Configure()
    {
        Post("/admin/users/{userId}/activate");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Activate a user";
            s.Description = "Restores an inactive user account to active status, re-enabling their access to the platform.";
            s.Response(200, "User activated");
            s.Response(401, "Not authenticated");
            s.Response(404, "User not found");
        });
        Options(x => x.WithTags("User Administration"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = Route<Guid>("userId");
        await _accessManagementService.ActivateUserAsync(userId, ct);
        await Send.OkAsync(ct);
    }
}
