using Aonik.Platform.Contracts.Api.Jobs;
using Aonik.Platform.Persistence;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Platform.Endpoints.Admin.Jobs;

/// <summary>
/// Lists all registered scheduled background jobs and their current state.
/// Data is populated by the Worker service on startup and after each execution.
/// </summary>
internal class ListScheduledJobsEndpoint : EndpointWithoutRequest<ScheduledJobListResponse>
{
    private readonly PlatformDbContext _dbContext;

    public ListScheduledJobsEndpoint(PlatformDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public override void Configure()
    {
        Get("/admin/jobs/scheduled");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var jobs = await _dbContext.Jobs
            .AsNoTracking()
            .Where(j => j.JobType.StartsWith("Scheduled:"))
            .OrderBy(j => j.JobType)
            .Select(j => new ScheduledJobSummary(
                j.JobType.Replace("Scheduled:", ""),
                "ScheduledJobs",
                null,
                j.ScheduleCron,
                j.Status,
                null,
                j.LastRunAt))
            .ToListAsync(ct);

        await Send.OkAsync(new ScheduledJobListResponse(jobs), ct);
    }
}
