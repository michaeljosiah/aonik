using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "Diagnose user account issues";
            s.Description = "Runs diagnostic checks on a user account to identify configuration or synchronization problems.";
            s.Response(200, "Diagnostic result");
            s.Response(401, "Not authenticated");
            s.Response(404, "User not found");
        });
        Options(x => x.WithTags("User Administration"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = Route<Guid>("userId");
        var result = await _accessManagementService.DiagnoseUserAsync(userId, ct);
        await Send.OkAsync(result, ct);
    }
}
