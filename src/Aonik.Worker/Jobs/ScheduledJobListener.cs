using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aonik.Worker.Jobs;

/// <summary>
/// Quartz job listener that updates the Job entity record
/// with the latest execution timestamp and result after each run.
/// </summary>
internal sealed class ScheduledJobListener : IJobListener
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<ScheduledJobListener> _logger;

    public string Name => "ScheduledJobListener";

    public ScheduledJobListener(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<ScheduledJobListener> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    public Task JobToBeExecuted(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task JobExecutionVetoed(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task JobWasExecuted(
        IJobExecutionContext context,
        JobExecutionException? jobException,
        CancellationToken cancellationToken = default)
    {
        if (context.JobDetail.Key.Group != "ScheduledJobs")
        {
            return;
        }

        var jobName = context.JobDetail.Key.Name;
        var jobType = $"Scheduled:{jobName}";

        try
        {
            using var scope = _serviceScopeFactory.CreateScope();

            // Set system tenant context for global Job entity writes
            var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
            tenantContext.TenantId = Guid.Empty;
            tenantContext.ResolutionSource = "system";

            var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

            var job = await dbContext.Jobs
                .FirstOrDefaultAsync(j => j.JobType == jobType, cancellationToken);

            if (job is null)
            {
                return;
            }

            job.LastRunAt = DateTime.UtcNow;

            if (jobException is not null)
            {
                job.LastResultJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    status = "Failed",
                    error = jobException.Message,
                    duration = context.JobRunTime.TotalMilliseconds,
                });
            }
            else
            {
                job.LastResultJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    status = "Completed",
                    duration = context.JobRunTime.TotalMilliseconds,
                });
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update Job record for {JobType} after execution.", jobType);
        }
    }
}
