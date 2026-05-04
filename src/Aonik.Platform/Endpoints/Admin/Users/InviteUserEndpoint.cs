using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Users;

internal class InviteUserEndpoint : Endpoint<InviteUserRequest, InviteUserResponse>
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
            s.Description = "Provisions a pending placeholder user with the requested roles in the current tenant. The first IdP login matching the invited email links to the placeholder.";
            s.Response(200, "Invitation created");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("User Administration"));
    }

    public override async Task HandleAsync(InviteUserRequest req, CancellationToken ct)
    {
        var result = await _accessManagementService.InviteUserAsync(req, ct);
        await Send.OkAsync(result, ct);
    }
}
