using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;

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
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = Route<Guid>("userId");
        var result = await _accessManagementService.RepairUserAsync(userId, ct);
        await Send.OkAsync(result, ct);
    }
}
