using Microsoft.EntityFrameworkCore;

using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Tasks;

namespace Aonik.Platform.Services.Integrations;

internal sealed class ConnectorRequestExpiryHandler(PlatformDbContext database, ITenantProvider tenantProvider,
    ConnectorConnectionRequestService requests, IClock clock) : ITaskActionHandler
{
    public string ActionType => "connector_request_expire";
    public async Task<TaskActionResult> ExecuteAsync(TaskActionContext context, CancellationToken cancellationToken = default)
    {
        ConnectorRequestExpiryPayload payload = ConnectorJson.Read<ConnectorRequestExpiryPayload>(context.ActionPayloadJson);
        if (context.TenantId != tenantProvider.GetCurrentTenantId() || context.Kind != TaskKinds.ScheduledAction || context.AssigneeType != TaskAssigneeTypes.System
            || context.SubjectType != "ConnectorConnectionRequest" || context.SubjectId != payload.RequestId)
            throw new ArgumentException("Invalid connection request expiry scope.");
        var request = await database.ConnectorConnectionRequests.SingleOrDefaultAsync(request => request.TenantId == context.TenantId
            && request.Id == payload.RequestId, cancellationToken);
        if (request is null || request.Status is not ("Pending" or "AwaitingConsent")) return new(TaskActionOutcome.Succeeded);
        if (payload.ConsentOnly)
        {
            if (request.OAuthExpiresAt is null || request.OAuthExpiresAt > clock.UtcNow) return new(TaskActionOutcome.Succeeded);
            ConnectorConnectionRequestService.ClearConsent(request);
            request.ReasonCode = "ConsentExpired"; request.NextAction = "Restart account-owner consent for this unchanged request.";
            try { await database.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateConcurrencyException) { return new(TaskActionOutcome.Failed, Error: "Consent state changed during expiry."); }
            return new(TaskActionOutcome.Succeeded);
        }
        if (request.ExpiresAt > clock.UtcNow) return new(TaskActionOutcome.Failed, Error: "The connection request is not due to expire.");
        ConnectorConnectionRequestService.Expire(request);
        await requests.AbandonPendingConnectionAsync(request, cancellationToken);
        requests.StageWake(request, "Notify", clock.UtcNow, request.RequesterUserId);
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return new(TaskActionOutcome.Failed, Error: "The connection request changed during expiry."); }
        return new(TaskActionOutcome.Succeeded);
    }
}
