using System.Text.Json;

using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Subscriptions;
using Aonik.SharedKernel.Events;
using Aonik.SharedKernel.Events.Integration;
using Aonik.Subscriptions.Persistence;
using Aonik.Subscriptions.Services.Usage;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aonik.Subscriptions.IntegrationEvents;

/// <summary>
/// Posts the ledger side of a usage commit (Spec 087 §13).
/// </summary>
/// <remarks>
/// Discovered by the SharedKernel handler scan and invoked by the outbox dispatcher with the
/// originating tenant restored, which is what lets it resolve the canonical ledger.
///
/// The commit itself stages this event in the same save as the drawdown, so the event cannot
/// describe consumption that did not commit; and <c>IJournalWriter</c> is idempotent on
/// (SourceType, SourceId) keyed by the usage record, so a redelivery returns the existing entry
/// rather than recognising the same revenue twice. Together those two facts are what make the
/// entitlement state and the ledger recover to the same place after any crash.
/// </remarks>
internal sealed class UsageCommittedEventHandler : IEventHandler<UsageCommittedEvent>
{
    private readonly SubscriptionsDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly UsageLedgerPoster _ledger;
    private readonly ILogger<UsageCommittedEventHandler> _logger;

    public UsageCommittedEventHandler(
        SubscriptionsDbContext dbContext,
        ITenantProvider tenantProvider,
        UsageLedgerPoster ledger,
        ILogger<UsageCommittedEventHandler> logger)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _ledger = ledger;
        _logger = logger;
    }

    public async Task HandleAsync(UsageCommittedEvent @event, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var record = await _dbContext.UsageRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == @event.UsageRecordId, cancellationToken);

        if (record is null)
        {
            // Not an error worth retrying forever: the record is the subject of the event, and if it
            // is gone the commit it described has been undone by something outside this flow.
            _logger.LogWarning(
                "Usage record {UsageRecordId} was not found; no ledger entry posted.", @event.UsageRecordId);
            return;
        }

        var allocations = ParseAllocations(record.AllocationsJson);

        await _ledger.PostConsumptionAsync(record, allocations, cancellationToken);
    }

    private static IReadOnlyList<GrantAllocation> ParseAllocations(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<GrantAllocation>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
