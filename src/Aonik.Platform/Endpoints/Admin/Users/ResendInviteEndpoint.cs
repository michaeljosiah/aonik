using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Users;

internal class ResendInviteEndpoint : EndpointWithoutRequest<ResendInviteResponse>
{
    private readonly IAccessManagementService _accessManagementService;

    public ResendInviteEndpoint(IAccessManagementService accessManagementService)
    {
        _accessManagementService = accessManagementService;
    }

    public override void Configure()
    {
        Post("/admin/users/{userId}/resend-invite");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Resend the invite email for an unaccepted placeholder.";
            s.Description = "Spec 026 Part 1. Regenerates the one-shot invite token and re-fires the AdminUserInvitation email. Enforced by a soft rate-limit (max sends/24h) per UserLifecycleOptions.";
            s.Response(200, "Invite resent");
            s.Response(401, "Not authenticated");
            s.Response(404, "User not found");
        });
        Options(x => x.WithTags("User Administration"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = Route<Guid>("userId");
        var result = await _accessManagementService.ResendInviteAsync(userId, ct);
        await Send.OkAsync(result, ct);
    }
}
