using Aonik.Platform.Contracts.Api.Jobs;
using Aonik.Platform.Contracts.Services.Operations;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Jobs;

/// <summary>
/// Lists all registered scheduled background jobs and their current state.
/// Data is populated by the Worker service on startup and after each execution.
/// </summary>
internal class ListScheduledJobsEndpoint : EndpointWithoutRequest<ScheduledJobListResponse>
{
    private readonly IScheduledJobAdminService _scheduledJobAdminService;

    public ListScheduledJobsEndpoint(IScheduledJobAdminService scheduledJobAdminService)
    {
        _scheduledJobAdminService = scheduledJobAdminService;
    }

    public override void Configure()
    {
        Get("/admin/jobs/scheduled");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "List all scheduled jobs";
            s.Description = "Returns all registered scheduled background jobs and their current execution state.";
            s.Response(200, "Scheduled job list");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("System Administration"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await _scheduledJobAdminService.ListScheduledJobsAsync(ct);
        await Send.OkAsync(result, ct);
    }
}
