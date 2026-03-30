using Aonik.Platform.Persistence;
using Aonik.Platform.Entities.Operations;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aonik.Worker.Jobs;

/// <summary>
/// Hosted service that runs once at startup to upsert Job entity records
/// for each scheduled Quartz job, so the API can list them.
/// </summary>
internal sealed class ScheduledJobRegistrar : IHostedService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ScheduledJobOptions _options;
    private readonly ILogger<ScheduledJobRegistrar> _logger;

    public ScheduledJobRegistrar(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<ScheduledJobOptions> options,
        ILogger<ScheduledJobRegistrar> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();

        // Set a system tenant context so the DbContext tenant enforcement passes.
        // Job entities are marked as global via IsGlobalEntity, so TenantId stays Guid.Empty.
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.TenantId = Guid.Empty;
        tenantContext.ResolutionSource = "system";

        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var jobDefinitions = new[]
        {
            new
            {
                Name = "FinancialConnectionRecurringSyncJob",
                Cron = _options.FinancialConnectionSync.CronExpression,
                Enabled = _options.FinancialConnectionSync.Enabled,
            },
            new
            {
                Name = "StaleSessionDetectorJob",
                Cron = _options.StaleSessionDetector.CronExpression,
                Enabled = _options.StaleSessionDetector.Enabled,
            },
            new
            {
                Name = "BehaviouralInsightJob",
                Cron = _options.BehaviouralInsight.CronExpression,
                Enabled = _options.BehaviouralInsight.Enabled,
            },
        };

        foreach (var def in jobDefinitions)
        {
            var jobType = $"Scheduled:{def.Name}";

            var existing = await dbContext.Jobs
                .FirstOrDefaultAsync(j => j.JobType == jobType, cancellationToken);

            if (existing is null)
            {
                dbContext.Jobs.Add(new Job
                {
                    TenantId = Guid.Empty,
                    JobType = jobType,
                    ScheduleCron = def.Cron,
                    Status = def.Enabled ? "Active" : "Disabled",
                    LastResultJson = "{}",
                });

                _logger.LogInformation("Registered scheduled job {JobType} with cron {Cron}.", jobType, def.Cron);
            }
            else
            {
                existing.ScheduleCron = def.Cron;

                if (!def.Enabled && existing.Status != "Paused")
                {
                    existing.Status = "Disabled";
                }

                _logger.LogDebug("Updated scheduled job {JobType} with cron {Cron}.", jobType, def.Cron);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
