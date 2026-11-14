using FastEndpoints;
using Microsoft.EntityFrameworkCore;

using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Integrations;

namespace Aonik.Platform.Endpoints.Integrations;

internal sealed record MailboxSubscriptionHttpRequest(Guid ConnectionId);
internal sealed class GetMailboxSubscriptionEndpoint(PlatformDbContext database, ConnectorAuthorization authorization, ConnectorGrantService grants)
    : Endpoint<MailboxSubscriptionHttpRequest>
{
    public override void Configure() => Get("/v1/integrations/connections/{ConnectionId:guid}/mailbox-subscription");
    public override async Task HandleAsync(MailboxSubscriptionHttpRequest request, CancellationToken ct)
    {
        Guid userId = await authorization.RequireUserAsync("Integration.Read", ct);
        var connection = await database.ConnectorConnections.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == authorization.TenantId && value.Id == request.ConnectionId, ct);
        if (connection is null) { await Send.NotFoundAsync(ct); return; }
        await grants.RequireManagerAsync(connection, userId, ct);
        var subscription = await database.ConnectorSubscriptions.AsNoTracking().Where(value => value.TenantId == authorization.TenantId && value.ConnectionId == request.ConnectionId)
            .Select(value => new { value.Id, value.Status, value.Mode, value.WatchExpiresAtUtc, value.WatchRenewedAtUtc,
                value.LastSuccessAtUtc, value.LastError, value.FullSync, value.NextPollAtUtc, value.PollTaskEndAtUtc }).SingleOrDefaultAsync(ct);
        await Send.OkAsync(new { subscription }, ct);
    }
}
