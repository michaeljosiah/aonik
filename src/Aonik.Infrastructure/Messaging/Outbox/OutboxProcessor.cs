using Aonik.Infrastructure.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Events.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aonik.Infrastructure.Messaging.Outbox;

/// <summary>
/// Drains due outbox rows. Reads a batch of eligible message ids under a system
/// scope, then processes each id in its own DI scope so the originating tenant can
/// be restored and a fresh DbContext used per message. On success the message is
/// marked processed; on failure it is retried with exponential backoff and finally
/// dead-lettered once <see cref="OutboxOptions.MaxAttempts"/> is reached.
/// Assumes a single drainer (registered only in the Worker host) — there is no row
/// claim, so running multiple instances could double-dispatch (inbox idempotency
/// still guards individual handlers).
/// </summary>
public sealed class OutboxProcessor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IClock _clock;
    private readonly OutboxOptions _options;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(
        IServiceScopeFactory scopeFactory,
        IClock clock,
        IOptions<OutboxOptions> options,
        ILogger<OutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Processes one batch. Returns the number of messages attempted.</summary>
    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var dueMessageIds = await ReadDueMessageIdsAsync(cancellationToken);
        var processed = 0;

        foreach (var messageId in dueMessageIds)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            await ProcessMessageAsync(messageId, cancellationToken);
            processed++;
        }

        return processed;
    }

    private async Task<List<Guid>> ReadDueMessageIdsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        SetSystemTenant(scope.ServiceProvider);

        var dbContext = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        var now = _clock.UtcNow;

        return await dbContext.Set<OutboxMessage>()
            .Where(m => m.ProcessedAt == null
                && m.DeadLetteredAt == null
                && (m.NextAttemptAt == null || m.NextAttemptAt <= now))
            .OrderBy(m => m.CreatedAt)
            .Select(m => m.Id)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken);
    }

    private async Task ProcessMessageAsync(Guid messageId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AonikDbContext>();

        var message = await dbContext.Set<OutboxMessage>()
            .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);

        if (message is null || message.ProcessedAt is not null || message.DeadLetteredAt is not null)
        {
            return;
        }

        // Restore the tenant the event was raised under so handlers and their
        // tenant-scoped queries/writes run in the correct context.
        SetTenant(scope.ServiceProvider, message.TenantId);

        var dispatcher = scope.ServiceProvider.GetRequiredService<IIntegrationEventDispatcher>();

        try
        {
            await dispatcher.DispatchAsync(message, cancellationToken);

            message.ProcessedAt = _clock.UtcNow;
            message.Error = null;
            message.NextAttemptAt = null;

            // Commits the inbox rows staged by the dispatcher together with the
            // processed marker, in one transaction on this scope's DbContext.
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Dispatched outbox message {EventId} ({EventType}).",
                message.EventId, message.EventType);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await RecordFailureAsync(dbContext, message, ex, cancellationToken);
        }
    }

    private async Task RecordFailureAsync(
        AonikDbContext dbContext,
        OutboxMessage message,
        Exception ex,
        CancellationToken cancellationToken)
    {
        message.Attempts += 1;
        message.Error = Truncate(ex.ToString(), 4000);

        if (message.Attempts >= _options.MaxAttempts)
        {
            message.DeadLetteredAt = _clock.UtcNow;
            message.NextAttemptAt = null;
            _logger.LogError(ex,
                "Outbox message {EventId} ({EventType}) dead-lettered after {Attempts} attempts.",
                message.EventId, message.EventType, message.Attempts);
        }
        else
        {
            message.NextAttemptAt = _clock.UtcNow.Add(ComputeBackoff(message.Attempts));
            _logger.LogWarning(ex,
                "Outbox message {EventId} ({EventType}) failed on attempt {Attempts}; retrying at {NextAttemptAt:o}.",
                message.EventId, message.EventType, message.Attempts, message.NextAttemptAt);
        }

        try
        {
            // Persists the failure state alongside any inbox rows the dispatcher
            // staged for handlers that succeeded before the throw.
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception saveEx)
        {
            // A failed status write must not crash the loop; the row stays due and
            // is retried on the next sweep.
            _logger.LogError(saveEx,
                "Failed to persist failure state for outbox message {EventId}.", message.EventId);
        }
    }

    private TimeSpan ComputeBackoff(int attempts)
    {
        var exponent = Math.Min(attempts - 1, 30); // cap exponent to avoid overflow
        var seconds = _options.BaseBackoffSeconds * Math.Pow(2, exponent);
        var capped = Math.Min(seconds, _options.MaxBackoffSeconds);
        return TimeSpan.FromSeconds(capped);
    }

    private static void SetTenant(IServiceProvider serviceProvider, Guid? tenantId)
    {
        var tenantContext = serviceProvider.GetRequiredService<ITenantContext>();
        tenantContext.TenantId = tenantId ?? Guid.Empty;
        tenantContext.ResolutionSource = "outbox";
    }

    private static void SetSystemTenant(IServiceProvider serviceProvider)
    {
        var tenantContext = serviceProvider.GetRequiredService<ITenantContext>();
        tenantContext.TenantId = Guid.Empty;
        tenantContext.ResolutionSource = "outbox-system";
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
