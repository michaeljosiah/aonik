using System.Data;
using System.Data.Common;
using System.Text.Json;
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
/// Drains due outbox rows. Each sweep atomically <em>claims</em> a batch of eligible
/// message ids under a system scope, stamping every claimed row with a time-boxed
/// lease (<see cref="OutboxOptions.ClaimLeaseSeconds"/>) so a concurrent drainer skips
/// it (SQL Server <c>READPAST</c>/<c>UPDLOCK</c>) and a crashed drainer's in-flight
/// rows recover once the lease lapses. Each claimed id is then processed in its own DI
/// scope so the originating tenant can be restored and a fresh DbContext used per
/// message. On success the message is marked processed and its claim released; on
/// failure it is retried with exponential backoff and finally dead-lettered once
/// <see cref="OutboxOptions.MaxAttempts"/> is reached.
/// The Worker host still registers a single drainer; the lease lets that scale to
/// multiple instances safely, with inbox idempotency as the second line of defence.
/// </summary>
public sealed class OutboxProcessor
{
    /// <summary>
    /// Identifies this drainer instance in claim tokens, so a lease can be traced to
    /// the process that took it. Stable for the life of the process.
    /// </summary>
    private static readonly string InstanceId = $"{Environment.MachineName}:{Guid.NewGuid():N}";

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
        var dueMessageIds = await ClaimDueMessageIdsAsync(cancellationToken);
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

    /// <summary>
    /// Atomically claims up to <see cref="OutboxOptions.BatchSize"/> due rows for this
    /// drainer under a fresh lease and returns their ids oldest-first. On SQL Server the
    /// claim is a single set-based statement so concurrent drainers never select the same
    /// row; the InMemory path emulates the same effect for tests.
    /// </summary>
    private async Task<List<Guid>> ClaimDueMessageIdsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        SetSystemTenant(scope.ServiceProvider);

        var dbContext = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        var now = _clock.UtcNow;
        var expiresAt = now.AddSeconds(_options.ClaimLeaseSeconds);
        var claimToken = $"{InstanceId}:{Guid.NewGuid():N}";

        return dbContext.Database.IsRelational()
            ? await ClaimDueMessageIdsRelationalAsync(dbContext, now, expiresAt, claimToken, cancellationToken)
            : await ClaimDueMessageIdsInMemoryAsync(dbContext, now, expiresAt, claimToken, cancellationToken);
    }

    /// <summary>
    /// SQL Server claim: a CTE selects the oldest due rows with <c>READPAST</c> (skip
    /// rows another drainer has locked) and <c>UPDLOCK</c>/<c>ROWLOCK</c> (hold them for
    /// the update), then a single UPDATE stamps the lease and OUTPUTs the claimed ids.
    /// The columns written by the UPDATE are listed in the CTE so it is updatable.
    /// </summary>
    private async Task<List<Guid>> ClaimDueMessageIdsRelationalAsync(
        AonikDbContext dbContext,
        DateTime now,
        DateTime expiresAt,
        string claimToken,
        CancellationToken cancellationToken)
    {
        var entityType = dbContext.Model.FindEntityType(typeof(OutboxMessage))
            ?? throw new InvalidOperationException("OutboxMessage is not mapped in the model.");
        var schema = entityType.GetSchema() ?? "dbo";
        var table = entityType.GetTableName()
            ?? throw new InvalidOperationException("OutboxMessage has no mapped table name.");

        var sql = $@"
WITH due AS (
    SELECT TOP (@batchSize) [Id], [ClaimedBy], [ClaimedAt], [ClaimExpiresAt]
    FROM [{schema}].[{table}] WITH (READPAST, UPDLOCK, ROWLOCK)
    WHERE [ProcessedAt] IS NULL
      AND [DeadLetteredAt] IS NULL
      AND ([NextAttemptAt] IS NULL OR [NextAttemptAt] <= @now)
      AND ([ClaimExpiresAt] IS NULL OR [ClaimExpiresAt] <= @now)
    ORDER BY [CreatedAt]
)
UPDATE due
SET [ClaimedBy] = @claimToken,
    [ClaimedAt] = @now,
    [ClaimExpiresAt] = @expiresAt
OUTPUT inserted.[Id];";

        var connection = dbContext.Database.GetDbConnection();
        var ids = new List<Guid>();

        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            AddParameter(command, "@batchSize", _options.BatchSize);
            AddParameter(command, "@now", now);
            AddParameter(command, "@expiresAt", expiresAt);
            AddParameter(command, "@claimToken", claimToken);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                ids.Add(reader.GetGuid(0));
            }
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }

        return ids;
    }

    /// <summary>
    /// InMemory claim used by tests: the provider can run neither raw SQL nor table
    /// hints and has no real concurrency, so a tracked read-modify-write reproduces the
    /// lease stamping under the same due/lease predicate as the relational path.
    /// </summary>
    private async Task<List<Guid>> ClaimDueMessageIdsInMemoryAsync(
        AonikDbContext dbContext,
        DateTime now,
        DateTime expiresAt,
        string claimToken,
        CancellationToken cancellationToken)
    {
        var due = await dbContext.Set<OutboxMessage>()
            .Where(m => m.ProcessedAt == null
                && m.DeadLetteredAt == null
                && (m.NextAttemptAt == null || m.NextAttemptAt <= now)
                && (m.ClaimExpiresAt == null || m.ClaimExpiresAt <= now))
            .OrderBy(m => m.CreatedAt)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in due)
        {
            message.ClaimedBy = claimToken;
            message.ClaimedAt = now;
            message.ClaimExpiresAt = expiresAt;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return due.Select(m => m.Id).ToList();
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        if (value is DateTime)
        {
            // Match the datetime2 mapping of the timestamp columns so lease comparisons
            // and writes keep full precision rather than the legacy datetime default.
            parameter.DbType = DbType.DateTime2;
        }

        parameter.Value = value;
        command.Parameters.Add(parameter);
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

        // Probe the payload for an OrderId so finance events (OrderCreatedEvent,
        // OrderStatusChangedEvent, PaymentCompletedEvent, etc.) put OrderId in
        // log scope before dispatch. Issue #142: every dispatch line — success,
        // retry, dead-letter — needs to carry OrderId for the saved KQL query.
        // Probing the JSON beats hardcoding the event-type list because new
        // OrderId-bearing events get picked up automatically.
        var orderId = TryExtractOrderId(message.Payload);

        using var orderScope = orderId.HasValue
            ? _logger.BeginScope(new Dictionary<string, object> { ["OrderId"] = orderId.Value })
            : null;

        var dispatcher = scope.ServiceProvider.GetRequiredService<IIntegrationEventDispatcher>();

        try
        {
            await dispatcher.DispatchAsync(message, cancellationToken);

            message.ProcessedAt = _clock.UtcNow;
            message.Error = null;
            message.NextAttemptAt = null;
            ClearClaim(message);

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

    /// <summary>
    /// Deserialise a minimal envelope from the message payload to surface an
    /// <c>OrderId</c> property if present. The probe is intentionally
    /// non-type-specific so any new OrderId-bearing event picks up scope
    /// enrichment without needing to list it here. Malformed payloads return
    /// <c>null</c> rather than crashing the processor.
    /// </summary>
    private static Guid? TryExtractOrderId(string? payload)
    {
        if (string.IsNullOrEmpty(payload))
        {
            return null;
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<OrderIdProbeEnvelope>(payload, ProbeJsonOptions);
            return envelope?.OrderId is Guid id && id != Guid.Empty ? id : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static readonly JsonSerializerOptions ProbeJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private sealed record OrderIdProbeEnvelope(Guid? OrderId);

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

        // Release the lease so the row is reclaimable on or after NextAttemptAt; the
        // backoff gate (not the lease) governs when the retry actually runs.
        ClearClaim(message);

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

    /// <summary>Releases this drainer's processing lease on the row.</summary>
    private static void ClearClaim(OutboxMessage message)
    {
        message.ClaimedBy = null;
        message.ClaimedAt = null;
        message.ClaimExpiresAt = null;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
