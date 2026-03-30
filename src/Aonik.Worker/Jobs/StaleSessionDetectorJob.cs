using Aonik.Agents.Contracts.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aonik.Worker.Jobs;

/// <summary>
/// Quartz job that detects stale chat sessions (no interaction for 15+ minutes)
/// and triggers conversation summary generation.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class StaleSessionDetectorJob : IJob
{
    private readonly IConversationSummaryService _conversationSummaryService;
    private readonly ScheduledJobOptions _options;
    private readonly ILogger<StaleSessionDetectorJob> _logger;

    public StaleSessionDetectorJob(
        IConversationSummaryService conversationSummaryService,
        IOptions<ScheduledJobOptions> options,
        ILogger<StaleSessionDetectorJob> logger)
    {
        _conversationSummaryService = conversationSummaryService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var batchSize = _options.StaleSessionDetector.BatchSize;

        _logger.LogDebug("Running stale session detection with batch size {BatchSize}.", batchSize);

        await _conversationSummaryService.ProcessStaleSessionsAsync(
            batchSize,
            context.CancellationToken);
    }
}
