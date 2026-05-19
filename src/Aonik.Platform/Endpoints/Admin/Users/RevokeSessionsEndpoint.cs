using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Users;

internal class RevokeSessionsEndpoint : Endpoint<RevokeUserSessionsRequest, RevokeUserSessionsResponse>
{
    private readonly IAccessManagementService _accessManagementService;

    public RevokeSessionsEndpoint(IAccessManagementService accessManagementService)
    {
        _accessManagementService = accessManagementService;
    }

    public override void Configure()
    {
        Post("/admin/users/{userId}/revoke-sessions");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Force a user offline by revoking active sessions.";
            s.Description = "Spec 026 Part 3. Writes a blocklist row and invalidates the FusionCache entry so the next request from this user with a token issued before the revoke timestamp returns 401. Tokens issued AFTER the revoke time are honoured — for permanent ban semantics, use Deactivate.";
            s.Response(200, "Sessions revoked");
            s.Response(401, "Not authenticated");
            s.Response(404, "User not found");
        });
        Options(x => x.WithTags("User Administration"));
    }

    public override async Task HandleAsync(RevokeUserSessionsRequest req, CancellationToken ct)
    {
        var userId = Route<Guid>("userId");
        var result = await _accessManagementService.RevokeSessionsAsync(userId, req, ct);
        await Send.OkAsync(result, ct);
    }
}
