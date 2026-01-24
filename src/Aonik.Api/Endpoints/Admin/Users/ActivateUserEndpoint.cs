using Aonik.Application.Services.Identity;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Admin.Users;

public class ActivateUserEndpoint : EndpointWithoutRequest
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
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = Route<Guid>("userId");
        await _accessManagementService.ActivateUserAsync(userId, ct);
        await Send.OkAsync(ct);
    }
}
