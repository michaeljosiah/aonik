using System.Threading.Channels;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aonik.Platform.Services.Operations;

internal interface IAlertProcessingQueue
{
    ValueTask EnqueueAsync(Guid alertId, CancellationToken cancellationToken = default);
}

internal sealed class AlertProcessingQueue : IAlertProcessingQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
    });

    public ValueTask EnqueueAsync(Guid alertId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _channel.Writer.WriteAsync(alertId, cancellationToken);
    }

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAllAsync(cancellationToken);
}

internal sealed class AlertProcessingBackgroundService : BackgroundService
{
    private readonly AlertProcessingQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AlertProcessingBackgroundService> _logger;

    public AlertProcessingBackgroundService(
        AlertProcessingQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<AlertProcessingBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var alertId in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
                tenantContext.TenantId = Guid.Empty;
                tenantContext.ResolutionSource = "AzureMonitorAlertProcessing";

                var processor = scope.ServiceProvider.GetRequiredService<AlertProcessingService>();
                await processor.ProcessAsync(alertId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background processing failed for platform alert {AlertId}.", alertId);
            }
        }
    }
}
