using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.SharedKernel.Abstractions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Users;

internal class ListUserTombstonesEndpoint : Endpoint<ListUsersRequest, PagedResult<UserTombstoneSummary>>
{
    private readonly IAccessManagementService _accessManagementService;

    public ListUserTombstonesEndpoint(IAccessManagementService accessManagementService)
    {
        _accessManagementService = accessManagementService;
    }

    public override void Configure()
    {
        Get("/admin/users/tombstones");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "List recent user deletions (compliance review).";
            s.Description = "Spec 026 Part 2. Returns paged tombstones — id, original user-id, deletion timestamp, operator, reason, masked email, count of audit rows redacted.";
            s.Response(200, "Tombstones returned");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("User Administration"));
    }

    public override async Task HandleAsync(ListUsersRequest req, CancellationToken ct)
    {
        var result = await _accessManagementService.ListTombstonesAsync(req, ct);
        await Send.OkAsync(result, ct);
    }
}
