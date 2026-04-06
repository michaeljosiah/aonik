using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Users;

internal class InviteUserEndpoint : Endpoint<InviteUserRequest>
{
    private readonly IAccessManagementService _accessManagementService;

    public InviteUserEndpoint(IAccessManagementService accessManagementService)
    {
        _accessManagementService = accessManagementService;
    }

    public override void Configure()
    {
        Post("/admin/users/invite");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Invite a new user";
            s.Description = "Sends an invitation to a new user to join the tenant, provisioning their account and sending a welcome notification.";
            s.Response(200, "Invitation sent");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("User Administration"));
    }

    public override async Task HandleAsync(InviteUserRequest req, CancellationToken ct)
    {
        await _accessManagementService.InviteUserAsync(req, ct);
        await Send.OkAsync(ct);
    }
}
