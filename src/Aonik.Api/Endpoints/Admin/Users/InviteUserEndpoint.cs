using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Admin.Users;

public class InviteUserEndpoint : Endpoint<InviteUserRequest>
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
    }

    public override async Task HandleAsync(InviteUserRequest req, CancellationToken ct)
    {
        await _accessManagementService.InviteUserAsync(req, ct);
        await Send.OkAsync(ct);
    }
}
