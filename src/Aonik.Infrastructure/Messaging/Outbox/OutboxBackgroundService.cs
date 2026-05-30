using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aonik.Infrastructure.Messaging.Outbox;

/// <summary>
/// Hosted service that periodically drains the transactional outbox. Registered
/// only in the Worker host so exactly one process dispatches integration events.
/// When a sweep fills an entire batch it loops again immediately to clear a
/// backlog; otherwise it idles for <see cref="OutboxOptions.PollIntervalSeconds"/>.
/// </summary>
public sealed class OutboxBackgroundService : BackgroundService
{
    private readonly OutboxProcessor _processor;
    private readonly OutboxOptions _options;
    private readonly ILogger<OutboxBackgroundService> _logger;

    public OutboxBackgroundService(
        OutboxProcessor processor,
        IOptions<OutboxOptions> options,
        ILogger<OutboxBackgroundService> logger)
    {
        _processor = processor;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.StartupDelaySeconds > 0)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.StartupDelaySeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        _logger.LogInformation("Outbox processor started (batch {BatchSize}, poll {PollInterval}s).",
            _options.BatchSize, _options.PollIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            int processed;
            try
            {
                processed = await _processor.ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // ProcessBatchAsync handles per-message failures internally; an
                // exception here means the batch read itself failed (e.g. DB blip).
                _logger.LogError(ex, "Outbox sweep failed; backing off before retry.");
                processed = 0;
            }

            // A full batch likely means a backlog — keep draining without idling.
            if (processed >= _options.BatchSize)
            {
                continue;
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Outbox processor stopped.");
    }
}
