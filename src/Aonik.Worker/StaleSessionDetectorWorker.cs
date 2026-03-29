using Aonik.Agents.Contracts.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aonik.Worker;

/// <summary>
/// Background worker that detects stale chat sessions (no interaction for 15+ minutes)
/// and triggers conversation summary generation for them.
/// Runs every 5 minutes.
/// </summary>
internal sealed class StaleSessionDetectorWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<StaleSessionDetectorWorker> _logger;

    public StaleSessionDetectorWorker(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<StaleSessionDetectorWorker> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Stale session detector started with poll interval {PollInterval}.", PollInterval);

        using var timer = new PeriodicTimer(PollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IConversationSummaryService>();
                await service.ProcessStaleSessionsAsync(batchSize: 10, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stale session detection cycle failed.");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken))
            {
                break;
            }
        }
    }
}
