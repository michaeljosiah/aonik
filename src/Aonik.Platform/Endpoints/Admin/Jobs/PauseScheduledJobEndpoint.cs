using Aonik.Platform.Contracts.Api.Jobs;
using Aonik.Platform.Contracts.Services.Operations;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Jobs;

/// <summary>
/// Pauses a scheduled job by updating its status in the Job entity.
/// The Worker service reads this status and skips execution when paused.
/// </summary>
internal class PauseScheduledJobEndpoint : EndpointWithoutRequest<ScheduledJobActionResponse>
{
    private readonly IScheduledJobAdminService _scheduledJobAdminService;

    public PauseScheduledJobEndpoint(IScheduledJobAdminService scheduledJobAdminService)
    {
        _scheduledJobAdminService = scheduledJobAdminService;
    }

    public override void Configure()
    {
        Post("/admin/jobs/scheduled/{jobName}/pause");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Pause a scheduled job";
            s.Description = "Queues a pause command for the specified job. The Worker service will skip execution while paused.";
            s.Response(200, "Pause queued");
            s.Response(401, "Not authenticated");
            s.Response(404, "Job not found");
        });
        Options(x => x.WithTags("System Administration"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var jobName = Route<string>("jobName")!;

        var result = await _scheduledJobAdminService.QueuePauseAsync(jobName, ct);
        if (result is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}
