using Aonik.Platform.Contracts.Api.Jobs;
using Aonik.Platform.Persistence;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Platform.Endpoints.Admin.Jobs;

/// <summary>
/// Resumes a paused scheduled job by updating its status in the Job entity.
/// </summary>
internal class ResumeScheduledJobEndpoint : EndpointWithoutRequest<ScheduledJobActionResponse>
{
    private readonly PlatformDbContext _dbContext;

    public ResumeScheduledJobEndpoint(PlatformDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public override void Configure()
    {
        Post("/admin/jobs/scheduled/{jobName}/resume");
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

        job.Status = "Active";

        await _dbContext.SaveChangesAsync(ct);

        await Send.OkAsync(new ScheduledJobActionResponse(
            jobName,
            "resume",
            true,
            "Job resumed successfully."), ct);
    }
}
