using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Aonik.Publishing.Contracts;
using Aonik.Publishing.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Events;
using Aonik.SharedKernel.Events.Integration;

namespace Aonik.Publishing.Services;

internal sealed class PublicationMirrorHandler(IPublishingDataContext db,ILegacyCatalogueMirror mirror,IOptions<PublishingOptions> options,
    ITenantProvider tenants,IClock clock) : IEventHandler<PublicationCatalogueCommittedEvent>
{
    public async Task HandleAsync(PublicationCatalogueCommittedEvent message,CancellationToken cancellationToken=default)
    {
        var tenant=tenants.GetCurrentTenantId();if(tenant!=message.TenantId)throw new InvalidOperationException("Mirror tenant mismatch.");
        var claim=Guid.NewGuid();
        var snapshot=await db.InTransactionAsync(async ct=>
        {
            if(!await db.CatalogueSnapshots.AnyAsync(s=>s.TenantId==tenant&&s.Id==message.EventId&&s.ChannelKey==message.ChannelKey&&s.Serial==message.Serial,ct)
                &&!await db.PublicationOperations.AnyAsync(o=>o.TenantId==tenant&&o.Id==message.EventId&&o.ChannelKey==message.ChannelKey&&o.Kind=="mirror-retry"&&o.CommittedSerial==message.Serial,ct))
                throw new InvalidOperationException("Mirror command identity mismatch.");
            var state=await db.PublicationChannels.SingleAsync(s=>s.TenantId==tenant&&s.ChannelKey==message.ChannelKey,ct);
            var c=options.Value.Channels.GetValueOrDefault(message.ChannelKey);
            if(c is null||c.TenantId!=tenant)throw new InvalidOperationException("Mirror publisher mismatch.");
            if(!c.LegacyMirrorEnabled){state.MirrorStatus="disabled";state.MirrorCode="not-configured";await db.SaveChangesAsync(ct);return null;}
            if(state.MirrorClaimExpiresAt>clock.UtcNow)throw new IOException("Mirror is claimed; redeliver after its lease.");
            state.MirrorClaimToken=claim;state.MirrorClaimExpiresAt=clock.UtcNow.AddMinutes(2);state.MirrorStatus="syncing";state.MirrorCode="reconciling-latest-head";
            await db.SaveChangesAsync(ct);
            // Deliberately ignore the event's historical head: every retry reconciles today's committed head.
            return await db.CatalogueSnapshots.AsNoTracking().SingleAsync(s=>s.TenantId==tenant&&s.Id==state.HeadSnapshotId&&s.ChannelKey==message.ChannelKey,ct);
        },cancellationToken);
        if(snapshot is null)return;
        string? failure=null;var transient=false;
        using var limit=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);limit.CancelAfter(TimeSpan.FromSeconds(90));
        try
        {
            var channel=options.Value.Channels[message.ChannelKey];
            var remote=await mirror.ReadAsync(channel,limit.Token);
            if(remote.Json is not null)
            {
                var serial=PlayerCatalogueProjection.Read(remote.Json)["serial"]!.GetValue<long>();
                if(serial>snapshot.Serial)failure="remote-ahead";
                else if(serial==snapshot.Serial&&PlayerCatalogueProjection.Hash(remote.Json)!=snapshot.Sha256)failure="same-serial-conflict";
                else if(serial<snapshot.Serial)await mirror.CompareAndSwapAsync(channel,remote.Revision,snapshot.Json,limit.Token);
            }
            else await mirror.CompareAndSwapAsync(channel,null,snapshot.Json,limit.Token);
        }
        catch(OperationCanceledException)when(cancellationToken.IsCancellationRequested){throw;}
        catch(Exception ex)
        {
            failure=ex is PublishingException problem?problem.Code:"mirror-transport-uncertain";
            transient=ex is IOException or HttpRequestException or OperationCanceledException||ex is PublishingException{Status:409};
        }
        var accepted=await db.InTransactionAsync(async ct=>
        {
            var state=await db.PublicationChannels.SingleAsync(s=>s.TenantId==tenant&&s.ChannelKey==message.ChannelKey,ct);
            if(state.MirrorClaimToken!=claim||state.MirrorClaimExpiresAt<=clock.UtcNow)return false;
            state.MirrorClaimToken=null;state.MirrorClaimExpiresAt=null;
            if(failure is null)
            {
                state.MirroredSerial=Math.Max(state.MirroredSerial??0,snapshot.Serial);
                state.MirrorStatus=state.HeadSnapshotId==snapshot.Id?"synced":"pending";state.MirrorCode=state.HeadSnapshotId==snapshot.Id?"latest-head-confirmed":"newer-canonical-head-pending";
            }
            else{state.MirrorStatus=transient?"retryable":"held";state.MirrorCode=failure;}
            await db.SaveChangesAsync(ct);return true;
        },cancellationToken);
        if(accepted&&transient)throw new IOException("Legacy mirror requires reconciliation against the latest head.");
    }
}
