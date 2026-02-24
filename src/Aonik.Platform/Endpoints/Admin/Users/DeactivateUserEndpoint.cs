using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;

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
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = Route<Guid>("userId");
        await _accessManagementService.DeactivateUserAsync(userId, ct);
        await Send.OkAsync(ct);
    }
}
