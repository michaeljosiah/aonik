using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Admin.Users;

public class UpdateUserRolesEndpoint : Endpoint<UpdateUserRolesRequest>
{
    private readonly IAccessManagementService _accessManagementService;

    public UpdateUserRolesEndpoint(IAccessManagementService accessManagementService)
    {
        _accessManagementService = accessManagementService;
    }

    public override void Configure()
    {
        Put("/admin/users/{userId}/roles");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(UpdateUserRolesRequest req, CancellationToken ct)
    {
        var userId = Route<Guid>("userId");
        await _accessManagementService.UpdateUserRolesAsync(userId, req, ct);
        await Send.OkAsync(ct);
    }
}
