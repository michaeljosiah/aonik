using Aonik.Platform.Contracts.Api.Jobs;
using Aonik.Platform.Persistence;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Platform.Endpoints.Admin.Jobs;

/// <summary>
/// Pauses a scheduled job by updating its status in the Job entity.
/// The Worker service reads this status and skips execution when paused.
/// </summary>
internal class PauseScheduledJobEndpoint : EndpointWithoutRequest<ScheduledJobActionResponse>
{
    private readonly PlatformDbContext _dbContext;

    public PauseScheduledJobEndpoint(PlatformDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public override void Configure()
    {
        Post("/admin/jobs/scheduled/{jobName}/pause");
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

        job.Status = "Paused";

        await _dbContext.SaveChangesAsync(ct);

        await Send.OkAsync(new ScheduledJobActionResponse(
            jobName,
            "pause",
            true,
            "Job paused successfully."), ct);
    }
}
