using Aonik.Finance.Persistence;
using Aonik.Finance.Services.PersonalFinance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aonik.Worker.Jobs;

/// <summary>
/// Quartz job that pre-computes behavioural insights (late-month spending spikes,
/// income rhythm, recurring merchants) for all active personal finance users.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class BehaviouralInsightJob : IJob
{
    private readonly FinanceDbContext _financeDbContext;
    private readonly BehaviouralInsightGenerator _insightGenerator;
    private readonly ScheduledJobOptions _options;
    private readonly ILogger<BehaviouralInsightJob> _logger;

    public BehaviouralInsightJob(
        FinanceDbContext financeDbContext,
        BehaviouralInsightGenerator insightGenerator,
        IOptions<ScheduledJobOptions> options,
        ILogger<BehaviouralInsightJob> logger)
    {
        _financeDbContext = financeDbContext;
        _insightGenerator = insightGenerator;
        _options = options.Value;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var cancellationToken = context.CancellationToken;
        var maxUsers = _options.BehaviouralInsight.MaxUsers;

        var users = await _financeDbContext.PersonalProfiles
            .Select(p => new { p.TenantId, p.UserId })
            .Distinct()
            .Take(maxUsers)
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Running behavioural insight generation for {UserCount} users.",
            users.Count);

        foreach (var user in users)
        {
            try
            {
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
    }
}
