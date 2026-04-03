using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;

namespace Aonik.Platform.Endpoints.Admin.Users;

internal class DiagnoseUserEndpoint : EndpointWithoutRequest<UserDiagnosticResult>
{
    private readonly IAccessManagementService _accessManagementService;

    public DiagnoseUserEndpoint(IAccessManagementService accessManagementService)
    {
        _accessManagementService = accessManagementService;
    }

    public override void Configure()
    {
        Get("/admin/users/{userId}/diagnose");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = Route<Guid>("userId");
        var result = await _accessManagementService.DiagnoseUserAsync(userId, ct);
        await Send.OkAsync(result, ct);
    }
}
