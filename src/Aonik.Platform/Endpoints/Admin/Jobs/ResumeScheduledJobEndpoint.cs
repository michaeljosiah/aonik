using Aonik.Platform.Contracts.Api.Jobs;
using Aonik.Platform.Contracts.Services.Operations;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Jobs;

/// <summary>
/// Resumes a paused scheduled job by updating its status in the Job entity.
/// </summary>
internal class ResumeScheduledJobEndpoint : EndpointWithoutRequest<ScheduledJobActionResponse>
{
    private readonly IScheduledJobAdminService _scheduledJobAdminService;

    public ResumeScheduledJobEndpoint(IScheduledJobAdminService scheduledJobAdminService)
    {
        _scheduledJobAdminService = scheduledJobAdminService;
    }

    public override void Configure()
    {
        Post("/admin/jobs/scheduled/{jobName}/resume");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Resume a scheduled job";
            s.Description = "Queues a resume command for a previously paused job, restoring its normal execution schedule.";
            s.Response(200, "Resume queued");
            s.Response(401, "Not authenticated");
            s.Response(404, "Job not found");
        });
        Options(x => x.WithTags("System Administration"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var jobName = Route<string>("jobName")!;

        var result = await _scheduledJobAdminService.QueueResumeAsync(jobName, ct);
        if (result is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}
