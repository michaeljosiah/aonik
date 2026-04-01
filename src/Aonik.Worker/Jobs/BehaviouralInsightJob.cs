using Aonik.Finance.Persistence;
using Aonik.Finance.Services.PersonalFinance;
using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Platform.Entities.Operations;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aonik.Worker.Jobs;

/// <summary>
/// Quartz job that materializes secondary generic behavioural insights from
/// the current canonical customer insight snapshots.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class BehaviouralInsightJob : IJob
{
    public static readonly JobKey Key = new("BehaviouralInsightJob", ScheduledJobGroups.ScheduledJobs);

    private readonly FinanceDbContext _financeDbContext;
    private readonly BehaviouralInsightGenerator _insightGenerator;
    private readonly ITenantContext _tenantContext;
    private readonly ScheduledJobOptions _options;
    private readonly ILogger<BehaviouralInsightJob> _logger;

    public BehaviouralInsightJob(
        FinanceDbContext financeDbContext,
        BehaviouralInsightGenerator insightGenerator,
        ITenantContext tenantContext,
        IOptions<ScheduledJobOptions> options,
        ILogger<BehaviouralInsightJob> logger)
    {
        _financeDbContext = financeDbContext;
        _insightGenerator = insightGenerator;
        _tenantContext = tenantContext;
        _options = options.Value;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var cancellationToken = context.CancellationToken;
        var maxUsers = _options.BehaviouralInsight.MaxUsers;

        var users = await _financeDbContext.CustomerInsightSnapshots
            .AsNoTracking()
            .Where(s => s.Status == CustomerInsightSnapshotContract.StatusCurrent)
            .OrderBy(s => s.TenantId)
            .ThenBy(s => s.UserId)
            .Select(s => new { s.TenantId, s.UserId })
            .Take(maxUsers)
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Running behavioural insight generation for {UserCount} users.",
            users.Count);

        foreach (var user in users)
        {
            try
            {
                _tenantContext.TenantId = user.TenantId;
                _tenantContext.ResolutionSource = "system";

                await _insightGenerator.GenerateAllForUserAsync(
                    user.TenantId,
                    user.UserId,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Insight generation failed for user {UserId} in tenant {TenantId}.",
                    user.UserId,
                    user.TenantId);
            }
        }

        _tenantContext.TenantId = Guid.Empty;
        _tenantContext.ResolutionSource = "system";
    }
}
