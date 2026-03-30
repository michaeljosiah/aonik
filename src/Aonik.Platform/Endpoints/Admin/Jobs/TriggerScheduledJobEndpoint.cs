using Aonik.Platform.Contracts.Api.Jobs;
using Aonik.Platform.Persistence;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Platform.Endpoints.Admin.Jobs;

/// <summary>
/// Requests an immediate trigger of a scheduled job by setting a command
/// on the Job entity that the Worker service reads and acts on.
/// </summary>
internal class TriggerScheduledJobEndpoint : EndpointWithoutRequest<ScheduledJobActionResponse>
{
    private readonly PlatformDbContext _dbContext;

    public TriggerScheduledJobEndpoint(PlatformDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public override void Configure()
    {
        Post("/admin/jobs/scheduled/{jobName}/trigger");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var jobName = Route<string>("jobName")!;
        var jobType = $"Scheduled:{jobName}";

        var job = await _dbContext.Jobs
            .FirstOrDefaultAsync(j => j.JobType == jobType, ct);

        if (job is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        job.LastResultJson = "{\"requestedAction\":\"trigger\"}";

        await _dbContext.SaveChangesAsync(ct);

        await Send.OkAsync(new ScheduledJobActionResponse(
            jobName,
            "trigger",
            true,
            "Trigger request queued. The job will be executed on the next Worker poll cycle."), ct);
    }
}
