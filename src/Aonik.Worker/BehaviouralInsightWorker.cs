using Aonik.Finance.Persistence;
using Aonik.Finance.Services.PersonalFinance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aonik.Worker;

/// <summary>
/// Nightly background worker that runs behavioural insight pre-computation
/// for all active personal finance users.
/// </summary>
internal sealed class BehaviouralInsightWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(6);

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<BehaviouralInsightWorker> _logger;

    public BehaviouralInsightWorker(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<BehaviouralInsightWorker> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Behavioural insight worker started with poll interval {PollInterval}.", PollInterval);

        // Initial delay to avoid running on startup
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        using var timer = new PeriodicTimer(PollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunInsightGenerationAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Behavioural insight generation cycle failed.");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken))
            {
                break;
            }
        }
    }

    private async Task RunInsightGenerationAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var generator = scope.ServiceProvider.GetRequiredService<BehaviouralInsightGenerator>();

        // Get all users with personal finance profiles
        var users = await dbContext.PersonalProfiles
            .Select(p => new { p.TenantId, p.UserId })
            .Distinct()
            .Take(100) // Batch limit
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Running behavioural insight generation for {UserCount} users.", users.Count);

        foreach (var user in users)
        {
            try
            {
                await generator.GenerateAllForUserAsync(user.TenantId, user.UserId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Insight generation failed for user {UserId} in tenant {TenantId}.",
                    user.UserId, user.TenantId);
            }
        }
    }
}
