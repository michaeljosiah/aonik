using Microsoft.EntityFrameworkCore;

using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Tasks;

namespace Aonik.Platform.Services.Integrations;

internal sealed class ConnectorMailboxPollHandler(ConnectorSubscriptionService subscriptions, PlatformDbContext database, ITenantProvider tenantProvider, IClock clock) : ITaskActionHandler
{
    public string ActionType => "connector_mailbox_poll";
    public async Task<TaskActionResult> ExecuteAsync(TaskActionContext context, CancellationToken cancellationToken = default)
    {
        if (context.TenantId != tenantProvider.GetCurrentTenantId() || context.Kind != TaskKinds.ScheduledAction || context.AssigneeType != TaskAssigneeTypes.System)
            throw new ArgumentException("Mailbox polling requires its actual server task scope.");
        Guid id = subscriptions.ReadTask(context.ActionPayloadJson, context.TenantId, context.WorkItemId);
        if (context.SubjectType != "ConnectorSubscription" || context.SubjectId != id || !await database.ConnectorSubscriptions.AsNoTracking()
            .AnyAsync(value => value.TenantId == context.TenantId && value.Id == id && value.PollTaskId == context.WorkItemId, cancellationToken))
            throw new ArgumentException("Mailbox polling task does not match its recorded subscription.");
        try { await subscriptions.SynchronizeAsync(id, cancellationToken); return new(TaskActionOutcome.Succeeded); }
        catch (ConnectorProviderException exception) when (exception.Transient)
        { return new(TaskActionOutcome.Deferred, Error: exception.Code, DeferredUntilUtc: clock.UtcNow.AddMinutes(5)); }
    }
}
