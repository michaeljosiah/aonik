using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Aonik.Publishing.Contracts;
using Aonik.Publishing.Entities;
using Aonik.Publishing.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Events;
using Aonik.SharedKernel.Events.Integration;

namespace Aonik.Publishing.Services;

internal sealed class PublicationDeliveryHandler(IPublishingDataContext db,PublicationAuthority authority,IPublishingObjectStore privateObjects,
    IPublicationArtifactStore publicObjects,IOptions<PublishingOptions> options,ITenantProvider tenants,IClock clock,
    ILogger<PublicationDeliveryHandler> logger) : IEventHandler<PublicationOperationRequestedEvent>
{
    public async Task HandleAsync(PublicationOperationRequestedEvent message,CancellationToken cancellationToken=default)
    {
        var tenant=tenants.GetCurrentTenantId();if(tenant!=message.TenantId)throw new InvalidOperationException("Publication event tenant mismatch.");
        var token=Guid.NewGuid();
        var operation=await db.InTransactionAsync(async ct=>
        {
            var op=await db.PublicationOperations.SingleAsync(o=>o.TenantId==tenant&&o.Id==message.OperationId,ct);
            if(message.EventId!=op.Id&&!await db.PublicationOperations.AnyAsync(a=>a.TenantId==tenant&&a.Id==message.EventId&&a.ParentOperationId==op.Id&&a.Kind=="retry",ct))
                throw new InvalidOperationException("Publication command identity mismatch.");
            if(op.Status=="committed")return null;
            if(op.Kind is not ("publish" or "withdraw"))throw new InvalidOperationException("Unsupported delivery command.");
            if(op.ClaimExpiresAt>clock.UtcNow)throw new IOException("Publication is claimed; redeliver after its lease.");
            if(op.Attempts>=Math.Clamp(options.Value.Delivery.MaxAttempts,1,10))
            {
                op.Status="dead-letter";op.Code="attempt-limit";await RestoreSubmissionAsync(op,ct);await db.SaveChangesAsync(ct);return null;
            }
            op.Status="copying";op.Code=op.Kind=="publish"?"preparing-immutable-assets":"preparing-withdrawal";op.Attempts++;
            op.ClaimToken=token;op.ClaimExpiresAt=clock.UtcNow.AddMinutes(Math.Clamp(options.Value.Delivery.LeaseMinutes,5,60));
            await db.SaveChangesAsync(ct);return op;
        },cancellationToken);
        if(operation is null)return;
        using var limit=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(TimeSpan.FromMinutes(Math.Clamp(options.Value.Delivery.LeaseMinutes,5,60)));
        try
        {
            if(operation.Kind=="publish")await PrepareAsync(operation,token,limit.Token);
            await CommitAsync(operation.Id,token,limit.Token);
        }
        catch(OperationCanceledException)when(cancellationToken.IsCancellationRequested){throw;}
        catch(Exception ex)
        {
            var code=ex is PublishingException problem?problem.Code:ex is IOException or HttpRequestException or OperationCanceledException?"public-transport-uncertain":"publication-reconciliation-required";
            var transient=ex is IOException or HttpRequestException or OperationCanceledException or DbUpdateException;
            logger.LogWarning("Publication {OperationId} held for {Code}; exact assets remain retained",operation.Id,code);
            using var recordLimit=new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var retry=await db.InTransactionAsync(async ct=>
            {
                var current=await db.PublicationOperations.SingleAsync(o=>o.TenantId==tenant&&o.Id==operation.Id,ct);
                if(current.Status=="committed"||current.ClaimToken!=token)return false;
                // Persist exhaustion on the final failure; the outbox may never dispatch this event again.
                var exhausted=transient&&current.Attempts>=Math.Clamp(options.Value.Delivery.MaxAttempts,1,10);
                current.Status=exhausted?"dead-letter":transient?"uncertain":"held";
                current.Code=exhausted?"attempt-limit":code;current.ClaimToken=null;current.ClaimExpiresAt=null;
                await RestoreSubmissionAsync(current,ct);await db.SaveChangesAsync(ct);return transient&&!exhausted;
            },recordLimit.Token);
            if(retry)throw new IOException("Publication requires a reconciled delivery retry.");
        }
    }
    private async Task PrepareAsync(PublicationOperation operation,Guid token,CancellationToken ct)
    {
        var submission=await db.Submissions.AsNoTracking().SingleAsync(s=>s.TenantId==operation.TenantId&&s.Id==operation.SubmissionId,ct);
        await authority.RequireStoredActorAsync(operation,submission,ct);
        var material=await authority.ApprovalAsync(submission,ct);Exact(operation,material);
        var c=options.Value.Channels[operation.ChannelKey];if(operation.PolicyJson!=PublicationAuthority.DeliveryPolicy(c,options.Value))throw PublicationAuthority.Blocked("publication-policy-changed");
        var revision=material.Revision;
        var session=await db.UploadSessions.AsNoTracking().SingleAsync(s=>s.TenantId==operation.TenantId&&s.SubmissionId==submission.Id&&s.RevisionId==revision.Id
            &&s.Id==revision.CurrentUploadSessionId&&s.CreatorPartyId==submission.CreatorPartyId&&s.Status=="uploaded",ct);
        if(session.StorageLocationId!=privateObjects.LocationId)throw PublicationAuthority.Blocked("private-store-changed");
        var input=await privateObjects.OpenPackageAsync(operation.TenantId,session.Id,revision.DeclaredPackageSha256,revision.DeclaredPackageBytes,ct);
        var pictureIds=PlayerCatalogueProjection.PictureIds(material);
        if(pictureIds.Count>options.Value.Delivery.MaxPictures)throw PublicationAuthority.Blocked("public-picture-count-limit");
        var pictures=pictureIds.Select(id=>material.Package.Assets.Single(a=>a.Id==id)).ToArray();
        if(pictures.Any(p=>p.BytesOutsidePictureLimit()))throw PublicationAuthority.Blocked("public-picture-size-limit");
        var location=publicObjects.LocationId;
        var artifacts=await db.InTransactionAsync(async tx=>
        {
            var owner=await OwnedClaim(operation.Id,token,tx);
            var existing=await db.PublicationArtifacts.Where(a=>a.TenantId==operation.TenantId&&a.OperationId==operation.Id).ToListAsync(tx);
            void Reserve(string id,long bytes,string hash,string type)
            {
                var old=existing.SingleOrDefault(a=>a.AssetId==id);
                if(old is not null)
                {if(old.Sha256!=hash||old.Bytes!=bytes||old.MediaType!=type||old.StorageLocationId!=location)throw PublicationAuthority.Blocked("public-artifact-identity-changed");return;}
                var item=new PublicationArtifact{TenantId=operation.TenantId,ChannelKey=operation.ChannelKey,OperationId=operation.Id,AssetId=id,
                    Bytes=bytes,Sha256=hash,MediaType=type,StorageLocationId=location};item.Url=PublicationBytes.Url(c,operation.ChannelKey,item.Id);
                db.PublicationArtifacts.Add(item);existing.Add(item);
            }
            Reserve("",input.Bytes,input.Sha256,"application/zip");foreach(var p in pictures)Reserve(p.Id,p.ByteLength,p.Sha256,p.MediaType);
            owner.Code="copying-reviewed-assets";await db.SaveChangesAsync(tx);return existing;
        },ct);
        foreach(var artifact in artifacts)
        {
            if(artifact.ReadyAt is null)
            {
                if(artifact.AssetId=="")
                {await using var source=new FileStream(input.Path,FileMode.Open,FileAccess.Read,FileShare.Read,65536,FileOptions.Asynchronous);await publicObjects.WriteAsync(artifact,source,ct);}
                else
                {
                    var asset=pictures.Single(p=>p.Id==artifact.AssetId);
                    using var zip=new ZipArchive(new FileStream(input.Path,FileMode.Open,FileAccess.Read,FileShare.Read),ZipArchiveMode.Read);
                    var entry=zip.GetEntry(asset.Path)??throw PublicationAuthority.Blocked("reviewed-artwork-unavailable");
                    if(entry.Length!=artifact.Bytes)throw PublicationAuthority.Blocked("reviewed-artwork-changed");
                    await using var source=entry.Open();await publicObjects.WriteAsync(artifact,source,ct);
                }
                await db.InTransactionAsync(async tx=>{await OwnedClaim(operation.Id,token,tx);var row=await Artifact(artifact.Id,operation.TenantId,tx);row.ReadyAt??=clock.UtcNow;await db.SaveChangesAsync(tx);return true;},ct);
            }
            // Read every actual public URL before any complete catalogue can reference it.
            await publicObjects.VerifyPublicAsync(artifact,ct);
            await db.InTransactionAsync(async tx=>{await OwnedClaim(operation.Id,token,tx);var row=await Artifact(artifact.Id,operation.TenantId,tx);row.PublicVerifiedAt=clock.UtcNow;await db.SaveChangesAsync(tx);return true;},ct);
        }
    }
    private async Task CommitAsync(Guid operationId,Guid token,CancellationToken cancellationToken)
    {
        await db.InTransactionAsync(async ct=>
        {
            var op=await db.PublicationOperations.SingleAsync(o=>o.TenantId==tenants.GetCurrentTenantId()&&o.Id==operationId,ct);
            if(op.Status=="committed")return true;
            op=await OwnedClaim(operationId,token,ct);
            var c=options.Value.Channels[op.ChannelKey];
            if(op.Kind=="withdraw")PublicationAuthority.RequireWithdrawal(c,options.Value);else PublicationAuthority.RequireDelivery(c,options.Value);
            if(op.PolicyJson!=PublicationAuthority.DeliveryPolicy(c,options.Value))throw PublicationAuthority.Blocked("publication-policy-changed");
            PublicationSubmission? submission=op.SubmissionId is { } subId?await db.Submissions.SingleAsync(s=>s.TenantId==op.TenantId&&s.Id==subId,ct):null;
            await authority.RequireStoredActorAsync(op,submission,ct);
            var state=await db.PublicationChannels.SingleAsync(s=>s.TenantId==op.TenantId&&s.ChannelKey==op.ChannelKey,ct);
            if(state.BaselineSha256!=c.BaselineSha256||state.PublicBaseUrl!=c.PublicBaseUrl.TrimEnd('/'))throw PublicationAuthority.Blocked("channel-baseline-changed");
            if(state.Serial>=PlayerCatalogueProjection.MaximumSerial)throw PublicationAuthority.Blocked("catalogue-serial-exhausted");
            var previous=await db.CatalogueSnapshots.SingleAsync(s=>s.TenantId==op.TenantId&&s.Id==state.HeadSnapshotId&&s.ChannelKey==op.ChannelKey,ct);
            var root=PlayerCatalogueProjection.Read(previous.Json);var serial=state.Serial+1;
            var next=new PublicationCatalogueSnapshot{TenantId=op.TenantId,ChannelKey=op.ChannelKey,Serial=serial,PreviousSnapshotId=previous.Id,OperationId=op.Id};
            if(op.Kind=="publish")
            {
                var approved=await authority.ApprovalAsync(submission!,ct);Exact(op,approved);
                var package=approved.Package;var slot=PlayerCatalogueProjection.Slot(package);var world=PlayerCatalogueProjection.World(root,approved.Revision.WorldId);
                if(PlayerCatalogueProjection.BaseFingerprint(world)!=op.ExpectedBaseFingerprint||PlayerCatalogueProjection.SlotFingerprint(world,slot)!=op.ExpectedSlotFingerprint)
                    throw PublicationService.Conflict("publication-lineage-changed");
                if(slot=="world"&&world?["package"] is not null&&!op.ConfirmBaseReplacement)throw PublicationAuthority.Blocked("base-replacement-confirmation-required");
                if(slot=="world"&&await db.PublishedEditions.AnyAsync(e=>e.TenantId==op.TenantId&&e.ChannelKey==op.ChannelKey&&e.WorldId==approved.Revision.WorldId&&e.Kind=="part"&&e.BaseFingerprint==package.Fingerprint,ct))
                    throw PublicationAuthority.Blocked("retired-part-lineage-requires-new-edition");
                if(package.Kind=="part")await CheckPartAsync(op,approved,world!,ct);
                var artifacts=await db.PublicationArtifacts.Where(a=>a.TenantId==op.TenantId&&a.OperationId==op.Id).ToListAsync(ct);
                if(artifacts.Count!=PlayerCatalogueProjection.PictureIds(approved).Count+1||artifacts.Any(a=>a.ReadyAt is null||a.PublicVerifiedAt is null))
                    throw PublicationAuthority.Blocked("public-asset-verification-required");
                var projection=PlayerCatalogueProjection.Apply(root,approved,artifacts,op,serial);next.Json=projection.Json;
                var old=await db.PublishedEditions.Where(e=>e.TenantId==op.TenantId&&e.ChannelKey==op.ChannelKey&&e.WorldId==approved.Revision.WorldId&&e.IsCurrent
                    &&(e.Slot==slot||(slot=="world"&&e.Kind=="part"))).ToListAsync(ct);
                foreach(var edition in old)edition.IsCurrent=false;
                // Release the filtered unique slot before adding its immutable replacement, within this transaction.
                if(old.Count>0)await db.SaveChangesAsync(ct);
                var published=new PublishedEdition{TenantId=op.TenantId,ChannelKey=op.ChannelKey,WorldId=approved.Revision.WorldId,Slot=slot,Kind=package.Kind,Title=approved.Revision.Title,
                    CreatorPartyId=submission!.CreatorPartyId,SubmissionId=submission.Id,RevisionId=approved.Revision.Id,DecisionId=approved.Decision.Id,OperationId=op.Id,SnapshotId=next.Id,
                    PackageSha256=package.ArchiveSha256,Fingerprint=package.Fingerprint,BaseFingerprint=package.BaseFingerprint,PinJson=projection.Pin.ToJsonString()};
                db.PublishedEditions.Add(published);op.EditionId=published.Id;
                var space=await db.Namespaces.SingleAsync(n=>n.TenantId==op.TenantId&&n.ChannelKey==op.ChannelKey&&n.WorldId==published.WorldId,ct);
                if(space.CreatorPartyId!=published.CreatorPartyId)throw PublicationAuthority.Blocked("publication-namespace-changed");
                if(package.Kind=="world"){space.ApprovedBaseFingerprint=package.Fingerprint;space.LastApprovedPartNumber=0;}
                if(package.Kind=="part")
                {
                    if(space.ApprovedBaseFingerprint!=package.BaseFingerprint||space.LastApprovedPartNumber+1!=package.PartNumber)throw PublicationAuthority.Blocked("publication-lineage-changed");
                    space.LastApprovedPartNumber=package.PartNumber!.Value;
                }
                submission.Status=SubmissionStatuses.Published;
            }
            else
            {
                var edition=await db.PublishedEditions.SingleAsync(e=>e.TenantId==op.TenantId&&e.ChannelKey==op.ChannelKey&&e.Id==op.EditionId,ct);
                PublicationService.Version(edition.RowVersion,op.ExpectedVersion);
                if(!edition.IsCurrent||(edition.Fingerprint??edition.PackageSha256)!=op.ExpectedSlotFingerprint)throw PublicationService.Conflict("publication-entry-changed");
                next.Json=PlayerCatalogueProjection.Withdraw(root,edition,serial);
                var baseScope=edition.Slot=="world"||edition.Kind=="part";
                var affected=await db.PublishedEditions.Where(e=>e.TenantId==op.TenantId&&e.ChannelKey==op.ChannelKey&&e.WorldId==edition.WorldId&&e.IsCurrent
                    &&(e.Id==edition.Id||(baseScope&&(e.Slot=="world"||e.Kind=="part")))).ToListAsync(ct);
                foreach(var row in affected){row.IsCurrent=false;row.WithdrawnAt=clock.UtcNow;}
                if(baseScope)
                {
                    var space=await db.Namespaces.SingleAsync(n=>n.TenantId==op.TenantId&&n.ChannelKey==op.ChannelKey&&n.WorldId==edition.WorldId,ct);
                    space.ApprovedBaseFingerprint=null;space.LastApprovedPartNumber=0;
                }
                await PublicationWithdrawalReceipts.ApplyAsync(db,op.TenantId,affected,ct);
            }
            next.Sha256=PlayerCatalogueProjection.Hash(next.Json);db.CatalogueSnapshots.Add(next);
            state.Serial=serial;state.HeadSnapshotId=next.Id;
            if(c.LegacyMirrorEnabled){state.MirrorStatus="pending";state.MirrorCode="queued";db.EnqueueIntegrationEvent(new PublicationCatalogueCommittedEvent(next.Id,clock.UtcNow,op.TenantId,op.ChannelKey,serial));}
            op.Status="committed";op.Code=op.Kind=="publish"?"published":"withdrawn";op.CompletedAt=clock.UtcNow;op.CommittedSerial=serial;op.CommittedSnapshotId=next.Id;op.ClaimToken=null;op.ClaimExpiresAt=null;
            await db.SaveChangesAsync(ct);return true;
        },cancellationToken);
    }
    private async Task<PublicationOperation> OwnedClaim(Guid id,Guid token,CancellationToken ct)
    {
        var row=await db.PublicationOperations.SingleAsync(o=>o.TenantId==tenants.GetCurrentTenantId()&&o.Id==id,ct);
        if(row.Status!="copying"||row.ClaimToken!=token||row.ClaimExpiresAt<=clock.UtcNow)throw PublicationService.Conflict("publication-claim-lost");return row;
    }
    private async Task CheckPartAsync(PublicationOperation op,ApprovedPublicationMaterial approved,JsonObject world,CancellationToken ct)
    {
        var package=approved.Package;var manifest=package.Presentation!.Manifest;
        var production=manifest.GetProperty("productions")[0].GetProperty("id").GetString()!;
        if(manifest.GetProperty("part").GetProperty("follows").GetString()!=world["package"]?["edition"]?.GetValue<string>()
            ||!world["productions"]!.AsArray().OfType<JsonObject>().Any(p=>p["id"]?.GetValue<string>()==production))
            throw PublicationAuthority.Blocked("publication-lineage-changed");
        if(!manifest.GetProperty("productions")[0].TryGetProperty("listening",out _))return;
        var baseEdition=await db.PublishedEditions.SingleAsync(e=>e.TenantId==op.TenantId&&e.ChannelKey==op.ChannelKey&&e.WorldId==approved.Revision.WorldId&&e.Slot=="world"&&e.IsCurrent,ct);
        List<PublicationChapterHead> earlier;
        if(baseEdition.IsLegacy)
        {
            var qualified=options.Value.Channels[op.ChannelKey].BaselineOwnership.GetValueOrDefault(baseEdition.WorldId);
            if(qualified is null||!qualified.ProductionChapters.TryGetValue(production,out var chapters))throw PublicationAuthority.Blocked("part-base-capability-unavailable");
            earlier=[..chapters];
        }
        else
        {
            var revision=await db.Revisions.SingleAsync(r=>r.TenantId==op.TenantId&&r.Id==baseEdition.RevisionId,ct);
            var report=await db.ValidationReports.SingleAsync(r=>r.TenantId==op.TenantId&&r.Id==revision.CurrentValidationReportId,ct);
            var verified=JsonSerializer.Deserialize<PackageVerificationResult>(report.ResultJson)!;
            if(verified.Presentation?.Details is not { } details||!details.TryGetProperty(production,out var detail))throw PublicationAuthority.Blocked("part-base-capability-unavailable");
            earlier=detail.TryGetProperty("chapters",out var chapters)?chapters.EnumerateArray().Select(c=>new PublicationChapterHead(c.GetProperty("id").GetString()!,c.GetProperty("number").GetInt32())).ToList():[];
        }
        foreach(var pin in (world["package"]?["parts"] as JsonArray??new JsonArray()).OfType<JsonObject>().Where(p=>p["production"]?.GetValue<string>()==production))
            earlier.AddRange(pin["chapters"]!.AsArray().Select(c=>new PublicationChapterHead(c!["id"]!.GetValue<string>(),c["number"]!.GetValue<int>())));
        if(package.Presentation.Details is not { } incoming||!incoming.TryGetProperty(production,out var incomingProduction)||!incomingProduction.TryGetProperty("chapters",out var next))throw PublicationAuthority.Blocked("part-chapters-unavailable");
        var last=earlier.Count==0?0:earlier.Max(c=>c.Number);var ids=earlier.Select(c=>c.Id).ToHashSet(StringComparer.Ordinal);
        if(next.EnumerateArray().Any(c=>c.GetProperty("number").GetInt32()<=last||ids.Contains(c.GetProperty("id").GetString()!)))throw PublicationAuthority.Blocked("part-chapter-lineage-changed");
    }
    private Task<PublicationArtifact> Artifact(Guid id,Guid tenant,CancellationToken ct)=>db.PublicationArtifacts.SingleAsync(a=>a.TenantId==tenant&&a.Id==id,ct);
    private async Task RestoreSubmissionAsync(PublicationOperation op,CancellationToken ct)
    {
        if(op.Kind!="publish"||op.SubmissionId is null)return;
        var sub=await db.Submissions.SingleAsync(s=>s.TenantId==op.TenantId&&s.Id==op.SubmissionId,ct);
        if(sub.ActivePublicationOperationId==op.Id&&sub.CurrentRevisionId==op.RevisionId&&sub.Status==SubmissionStatuses.Publishing)sub.Status=SubmissionStatuses.Approved;
    }
    private static void Exact(PublicationOperation op,ApprovedPublicationMaterial material)
    {
        if(material.Submission.ActivePublicationOperationId!=op.Id||material.Revision.Id!=op.RevisionId||material.Decision.Id!=op.DecisionId||material.Decision.ReviewDigest!=op.ReviewDigest)
            throw PublicationAuthority.Blocked("publication-approval-changed");
    }
}

internal static class PublicPictureLimits
{
    public static bool BytesOutsidePictureLimit(this PackageAsset asset)=>asset.ByteLength is <1 or >4194304||asset.MediaType is not ("image/png" or "image/jpeg");
}
