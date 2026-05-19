using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Users;

internal class DeleteUserEndpoint : Endpoint<DeleteUserRequest, DeleteUserResponse>
{
    private readonly IAccessManagementService _accessManagementService;

    public DeleteUserEndpoint(IAccessManagementService accessManagementService)
    {
        _accessManagementService = accessManagementService;
    }

    public override void Configure()
    {
        Delete("/admin/users/{userId}");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Permanently delete a user (GDPR-strict).";
            s.Description = "Spec 026 Part 2. Revokes active sessions, deletes the IdP user record, writes a tombstone, redacts PII from audit logs, cascades to role + invite-log rows, then drops the AnkPlatformUsers row. Requires the operator to type back the user's email and supply a reason ≥ 10 characters.";
            s.Response(200, "User deleted; tombstone returned");
            s.Response(400, "Email confirmation or reason missing/invalid");
            s.Response(401, "Not authenticated");
            s.Response(404, "User not found");
        });
        Options(x => x.WithTags("User Administration"));
    }

    public override async Task HandleAsync(DeleteUserRequest req, CancellationToken ct)
    {
        var userId = Route<Guid>("userId");
        var result = await _accessManagementService.DeleteUserAsync(userId, req, ct);
        await Send.OkAsync(result, ct);
    }
}
