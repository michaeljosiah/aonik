using Microsoft.EntityFrameworkCore;

using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Tasks;
using Aonik.SharedKernel.Events;
using Aonik.SharedKernel.Events.Integration;

namespace Aonik.Platform.Services.Integrations;

internal sealed class ConnectorRequestWakeHandler(PlatformDbContext database, ITenantProvider tenantProvider, ITaskService tasks)
    : IEventHandler<ConnectorRequestWakeRequestedEvent>
{
    public async Task HandleAsync(ConnectorRequestWakeRequestedEvent message, CancellationToken cancellationToken = default)
    {
        if (tenantProvider.GetCurrentTenantId() != message.TenantId || message.EventId == Guid.Empty) throw new InvalidOperationException("Invalid connector request event scope.");
        var request = await database.ConnectorConnectionRequests.AsNoTracking().SingleOrDefaultAsync(request => request.TenantId == message.TenantId
            && request.Id == message.RequestId, cancellationToken) ?? throw new InvalidOperationException("Connection request is unavailable.");
        if (message.Kind is "Expire" or "ConsentExpire")
        {
            await tasks.ScheduleAsync(new("Expire connection request state", TaskKinds.ScheduledAction, "connector_request_expire",
                ConnectorJson.Serialize(new ConnectorRequestExpiryPayload(request.Id, message.Kind == "ConsentExpire")), TaskAssigneeTypes.System,
                SubjectType: "ConnectorConnectionRequest", SubjectId: request.Id, RunAtUtc: DateTime.SpecifyKind(message.NotBeforeUtc, DateTimeKind.Utc),
                MaxRuns: 1, SourceModule: "Platform", TaskId: message.EventId), cancellationToken);
            return;
        }
        if (message.Kind != "Notify" || message.RecipientUserId is not { } recipient
            || (recipient != request.RequesterUserId && recipient != request.OwnerUserId && recipient != request.ReviewerUserId))
            throw new InvalidOperationException("Invalid connection request notification recipient.");
        string path = recipient == request.RequesterUserId || recipient == request.OwnerUserId
            ? $"/settings/my-connections?requestId={request.Id:D}" : $"/settings/tenant-integrations?requestId={request.Id:D}";
        await tasks.ScheduleAsync(new("Connection request update", TaskKinds.Reminder, TaskActionTypes.NotifyUser,
            ConnectorJson.Serialize(new { userId = recipient, title = "Connection request update", body = $"Connection request {request.Id:D} is {request.Status}.",
                type = "integrations.connection_request", actionUrl = path }), TaskAssigneeTypes.User, recipient,
            SubjectType: "ConnectorConnectionRequest", SubjectId: request.Id, RunAtUtc: DateTime.SpecifyKind(message.NotBeforeUtc, DateTimeKind.Utc),
            MaxRuns: 1, SourceModule: "Platform", TaskId: message.EventId), cancellationToken);
    }
}

internal sealed record ConnectorRequestExpiryPayload(Guid RequestId, bool ConsentOnly = false);
