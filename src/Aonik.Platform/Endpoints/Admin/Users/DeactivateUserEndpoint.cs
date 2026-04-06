using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Users;

internal class DeactivateUserEndpoint : EndpointWithoutRequest
{
    private readonly IAccessManagementService _accessManagementService;

    public DeactivateUserEndpoint(IAccessManagementService accessManagementService)
    {
        _accessManagementService = accessManagementService;
    }

    public override void Configure()
    {
        Post("/admin/users/{userId}/deactivate");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Deactivate a user";
            s.Description = "Disables a user account, preventing them from accessing the platform until reactivated.";
            s.Response(200, "User deactivated");
            s.Response(401, "Not authenticated");
            s.Response(404, "User not found");
        });
        Options(x => x.WithTags("User Administration"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = Route<Guid>("userId");
        await _accessManagementService.DeactivateUserAsync(userId, ct);
        await Send.OkAsync(ct);
    }
}
