using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Aonik.Publishing.Contracts;
using Aonik.Publishing.Entities;
using Aonik.Publishing.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Events.Integration;

namespace Aonik.Publishing.Services;

internal sealed class PublicationService(IPublishingDataContext db,PublicationAuthority authority,IPublicationArtifactStore store,
    IOptions<PublishingOptions> options,IClock clock,IPartyReader parties) : IPublicationService
{
    public async Task<IReadOnlyList<PublicationChannelResponse>> ChannelsAsync(CancellationToken cancellationToken=default)
    {
        var actor=await authority.RequireAsync(cancellationToken);var result=new List<PublicationChannelResponse>();
        foreach(var pair in options.Value.Channels.OrderBy(p=>p.Key,StringComparer.Ordinal))
        {
            try{authority.Channel(actor,pair.Key);}catch(PublishingException e)when(e.Status==404){continue;}
            var state=await db.PublicationChannels.AsNoTracking().SingleOrDefaultAsync(c=>c.TenantId==actor.TenantId&&c.ChannelKey==pair.Key,cancellationToken);
            result.Add(ChannelResponse(actor,pair.Key,pair.Value,state));
        }
        return result;
    }
    public async Task<PublicationCatalogueResponse> CatalogueAsync(string channelKey,int page=1,int pageSize=25,CancellationToken cancellationToken=default)
    {
        var actor=await authority.RequireAsync(cancellationToken);var c=authority.Channel(actor,channelKey);EditorialReviewService.RequirePage(page,pageSize);
        var state=await db.PublicationChannels.AsNoTracking().SingleOrDefaultAsync(c=>c.TenantId==actor.TenantId&&c.ChannelKey==channelKey,cancellationToken);
        var editions=db.PublishedEditions.AsNoTracking().Where(e=>e.TenantId==actor.TenantId&&e.ChannelKey==channelKey);
        var total=await editions.CountAsync(cancellationToken);
        var rows=await editions.OrderByDescending(e=>e.CreatedAt).ThenByDescending(e=>e.Id).Skip((page-1)*pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var ops=await db.PublicationOperations.AsNoTracking().Where(o=>o.TenantId==actor.TenantId&&o.ChannelKey==channelKey)
            .OrderByDescending(o=>o.CreatedAt).ThenByDescending(o=>o.Id).Take(50).ToListAsync(cancellationToken);
        var snapshotIds=rows.Select(e=>e.SnapshotId).ToArray();
        var serials=await db.CatalogueSnapshots.AsNoTracking().Where(s=>s.TenantId==actor.TenantId&&snapshotIds.Contains(s.Id)).ToDictionaryAsync(s=>s.Id,s=>s.Serial,cancellationToken);
        var ready=actor.CanPublish&&c.PublisherUserIds.Contains(actor.UserId)?await (from s in db.Submissions.AsNoTracking()
            join r in db.Revisions.AsNoTracking() on s.CurrentRevisionId equals r.Id
            where s.TenantId==actor.TenantId&&r.TenantId==actor.TenantId&&s.ChannelKey==channelKey&&s.Status==SubmissionStatuses.Approved
                &&s.OriginatingUserId!=actor.UserId&&!actor.PartyIds.Contains(s.CreatorPartyId)
            orderby s.CreatedAt
            select new{Submission=s,Revision=r}).Take(25).ToListAsync(cancellationToken):[];
        return new(ChannelResponse(actor,channelKey,c,state),page,pageSize,total,rows.Select(e=>Edition(e,serials[e.SnapshotId])).ToArray(),ops.Select(Map).ToArray(),
            ready.Select(r=>new ApprovedReleaseSummary(r.Submission.Id,Metadata(r.Revision),r.Submission.Status,Convert.ToBase64String(r.Submission.RowVersion),r.Submission.PublicationHoldReason)).ToArray());
    }
    public async Task<PublicationReleasePreview> PreviewAsync(Guid submissionId,CancellationToken cancellationToken=default)
    {
        var actor=await authority.RequireAsync(cancellationToken);var submission=await Submission(actor,submissionId,cancellationToken);
        var c=authority.Channel(actor,submission.ChannelKey);Independent(actor,submission);
        var revision=await db.Revisions.AsNoTracking().SingleAsync(r=>r.TenantId==actor.TenantId&&r.Id==submission.CurrentRevisionId&&r.SubmissionId==submission.Id,cancellationToken);
        var state=await db.PublicationChannels.AsNoTracking().SingleOrDefaultAsync(s=>s.TenantId==actor.TenantId&&s.ChannelKey==submission.ChannelKey,cancellationToken);
        string? blocked=null;ApprovedPublicationMaterial? material=null;
        try{material=await authority.ApprovalAsync(submission,cancellationToken);}catch(PublishingException ex){blocked=ex.Code;}
        JsonObject? world=null;
        if(state is not null)
        {
            var snapshot=await db.CatalogueSnapshots.AsNoTracking().SingleAsync(s=>s.TenantId==actor.TenantId&&s.Id==state.HeadSnapshotId,cancellationToken);
            world=PlayerCatalogueProjection.World(PlayerCatalogueProjection.Read(snapshot.Json),revision.WorldId);
        }
        else blocked??="baseline-required";
        var ops=await db.PublicationOperations.AsNoTracking().Where(o=>o.TenantId==actor.TenantId&&o.SubmissionId==submissionId)
            .OrderByDescending(o=>o.CreatedAt).Take(30).ToListAsync(cancellationToken);
        return new(submission.Id,revision.Id,submission.ActiveEditorialDecisionId,submission.Status,material?.Decision.ReviewDigest??"",
            Convert.ToBase64String(submission.RowVersion),submission.PublicationHoldReason,blocked,state?.Serial,PlayerCatalogueProjection.BaseFingerprint(world),
            material is null?null:PlayerCatalogueProjection.SlotFingerprint(world,PlayerCatalogueProjection.Slot(material.Package)),world?.ToJsonString(),ops.Select(Map).ToArray(),Metadata(revision));
    }
    public async Task<PublicationOperationResponse> InitialiseAsync(string channelKey,InitialisePublicationChannelRequest request,CancellationToken cancellationToken=default)
    {
        var actor=await authority.RequireAsync(cancellationToken);var c=authority.Channel(actor,channelKey,"publish");PublicationAuthority.RequireDelivery(c,options.Value);
        var digest=ReviewConcurrency.Digest(new{kind="initialise",channelKey,actor.UserId,actor.PartyId,request});
        return await db.InTransactionAsync(async ct=>
        {
            var old=await Replay(actor,request.OperationId,digest,ct);if(old is not null)return Map(old);
            if(request.BaselineSha256!=c.BaselineSha256||PlayerCatalogueProjection.Hash(c.BaselineCatalogueJson)!=c.BaselineSha256
                ||string.IsNullOrWhiteSpace(c.BaselineEvidenceReference)||c.BaselineEvidenceReference.Length>2000)
                throw PublicationAuthority.Blocked("qualified-baseline-required");
            var root=PlayerCatalogueProjection.Read(c.BaselineCatalogueJson);
            if(await db.PublicationChannels.AnyAsync(s=>s.TenantId==actor.TenantId&&s.ChannelKey==channelKey,ct))throw Conflict("channel-already-initialised");
            _=store.LocationId;
            var op=New(actor,channelKey,request.OperationId,digest,"initialise");op.Reason=Reason(request.Reason);op.Status="committed";op.Code="baseline-imported";op.CompletedAt=clock.UtcNow;
            var serial=root["serial"]!.GetValue<long>();
            if(serial>=PlayerCatalogueProjection.MaximumSerial)throw PublicationAuthority.Blocked("catalogue-serial-exhausted");
            var snapshot=new PublicationCatalogueSnapshot{TenantId=actor.TenantId,ChannelKey=channelKey,Serial=serial,OperationId=op.Id,
                Json=c.BaselineCatalogueJson,Sha256=c.BaselineSha256,EvidenceReference=c.BaselineEvidenceReference};
            foreach(var world in root["worlds"]!.AsArray().OfType<JsonObject>())
            {
                var id=world["id"]!.GetValue<string>();
                if(!c.BaselineOwnership.TryGetValue(id,out var owner)||owner.CreatorPartyId==Guid.Empty||string.IsNullOrWhiteSpace(owner.EvidenceReference)
                    ||owner.EvidenceReference.Length>2000)throw PublicationAuthority.Blocked("baseline-ownership-required");
                if(!await parties.ExistsAsync(actor.TenantId,owner.CreatorPartyId,ct))throw PublicationAuthority.Blocked("baseline-ownership-required");
                var space=await db.Namespaces.SingleOrDefaultAsync(n=>n.TenantId==actor.TenantId&&n.ChannelKey==channelKey&&n.WorldId==id,ct);
                if(space is not null&&space.CreatorPartyId!=owner.CreatorPartyId)throw PublicationAuthority.Blocked("baseline-namespace-conflict");
                if(space is null){space=new(){TenantId=actor.TenantId,ChannelKey=channelKey,WorldId=id,CreatorPartyId=owner.CreatorPartyId};db.Namespaces.Add(space);}
                space.ApprovedBaseFingerprint=PlayerCatalogueProjection.BaseFingerprint(world);
                space.LastApprovedPartNumber=(world["package"]?["parts"] as JsonArray)?.Count??0;
                void Import(string slot,string kind,JsonNode? value,string title)
                {
                    if(value is not JsonObject pin)return;
                    var retained=pin.DeepClone().AsObject();retained.Remove("parts");
                    db.PublishedEditions.Add(new(){TenantId=actor.TenantId,ChannelKey=channelKey,WorldId=id,Slot=slot,Kind=kind,Title=title.Length>200?title[..200]:title,
                        CreatorPartyId=owner.CreatorPartyId,OperationId=op.Id,SnapshotId=snapshot.Id,PackageSha256=pin["sha256"]!.GetValue<string>(),
                        Fingerprint=pin["fingerprint"]?.GetValue<string>(),BaseFingerprint=kind=="part"?PlayerCatalogueProjection.BaseFingerprint(world):null,PinJson=retained.ToJsonString(),IsLegacy=true});
                }
                var name=world["name"]!.GetValue<string>();Import("world","world",world["package"],name);Import("guide","guide",world["guide"],name);
                if(world["package"]?["parts"] is JsonArray parts)for(var i=0;i<parts.Count;i++)Import("part:"+(i+1),"part",parts[i],name+" · Part "+(i+1));
                foreach(var p in world["productions"]!.AsArray().OfType<JsonObject>())
                {
                    var pin=p["comic"]??p["story"]??p["package"];if(pin is null)continue;
                    var pid=p["id"]?.GetValue<string>();if(pid is null)throw PublicationAuthority.Blocked("baseline-production-identity-required");
                    Import("production:"+pid,p["comic"] is not null?"comic":p["story"] is not null?"story":"production",pin,p["title"]!.GetValue<string>());
                }
            }
            db.PublicationOperations.Add(op);db.CatalogueSnapshots.Add(snapshot);
            db.PublicationChannels.Add(new(){TenantId=actor.TenantId,ChannelKey=channelKey,Serial=serial,HeadSnapshotId=snapshot.Id,
                BaselineSha256=c.BaselineSha256,PublicBaseUrl=c.PublicBaseUrl.TrimEnd('/'),MirrorStatus=c.LegacyMirrorEnabled?"pending":"disabled",MirrorCode=c.LegacyMirrorEnabled?"queued":"not-configured"});
            op.CommittedSerial=serial;op.CommittedSnapshotId=snapshot.Id;
            if(c.LegacyMirrorEnabled)db.EnqueueIntegrationEvent(new PublicationCatalogueCommittedEvent(snapshot.Id,clock.UtcNow,actor.TenantId,channelKey,serial));
            await db.SaveChangesAsync(ct);return Map(op);
        },cancellationToken);
    }
    public async Task<PublicationOperationResponse> PublishAsync(Guid submissionId,PublishRevisionRequest request,CancellationToken cancellationToken=default)
    {
        var actor=await authority.RequireAsync(cancellationToken);var digest=ReviewConcurrency.Digest(new{kind="publish",submissionId,actor.UserId,actor.PartyId,request});
        return await db.InTransactionAsync(async ct=>
        {
            var submission=await Submission(actor,submissionId,ct);var c=authority.Channel(actor,submission.ChannelKey,"publish");Independent(actor,submission);
            var old=await Replay(actor,request.OperationId,digest,ct);if(old is not null)return Map(old);
            ReviewConcurrency.RequireVersion(submission,request.ExpectedVersion);
            if(submission.Status!=SubmissionStatuses.Approved)throw Conflict("publication-in-progress");
            var approved=await authority.ApprovalAsync(submission,ct);
            if(request.RevisionId!=approved.Revision.Id||request.DecisionId!=approved.Decision.Id||request.ReviewDigest!=approved.Decision.ReviewDigest)throw Conflict("publication-approval-changed");
            var state=await State(actor.TenantId,submission.ChannelKey,ct);
            if(state.Serial!=request.ExpectedSerial||state.BaselineSha256!=c.BaselineSha256||state.PublicBaseUrl!=c.PublicBaseUrl.TrimEnd('/'))throw Conflict("catalogue-head-changed");
            var snapshot=await db.CatalogueSnapshots.SingleAsync(s=>s.TenantId==actor.TenantId&&s.Id==state.HeadSnapshotId,ct);
            var world=PlayerCatalogueProjection.World(PlayerCatalogueProjection.Read(snapshot.Json),approved.Revision.WorldId);var slot=PlayerCatalogueProjection.Slot(approved.Package);
            if(PlayerCatalogueProjection.BaseFingerprint(world)!=request.ExpectedBaseFingerprint||PlayerCatalogueProjection.SlotFingerprint(world,slot)!=request.ExpectedSlotFingerprint)throw Conflict("publication-lineage-changed");
            if(slot=="world"&&world?["package"] is not null&&!request.ConfirmBaseReplacement)throw PublicationAuthority.Blocked("base-replacement-confirmation-required");
            var op=New(actor,submission.ChannelKey,request.OperationId,digest,"publish");op.SubmissionId=submission.Id;op.RevisionId=request.RevisionId;op.DecisionId=request.DecisionId;
            op.ReviewDigest=request.ReviewDigest;op.ExpectedVersion=request.ExpectedVersion;op.ExpectedSerial=request.ExpectedSerial;
            op.ExpectedBaseFingerprint=request.ExpectedBaseFingerprint;op.ExpectedSlotFingerprint=request.ExpectedSlotFingerprint;op.ConfirmBaseReplacement=request.ConfirmBaseReplacement;
            op.PolicyJson=PublicationAuthority.DeliveryPolicy(c,options.Value);_=store.LocationId;
            op.Reason=Reason(request.Reason);
            db.PublicationOperations.Add(op);submission.Status=SubmissionStatuses.Publishing;submission.ActivePublicationOperationId=op.Id;
            db.EnqueueIntegrationEvent(new PublicationOperationRequestedEvent(op.Id,clock.UtcNow,actor.TenantId,op.Id));
            await db.SaveChangesAsync(ct);return Map(op);
        },cancellationToken);
    }
    public async Task<PublicationOperationResponse> RetryAsync(Guid operationId,PublicationActionRequest request,CancellationToken cancellationToken=default)
    {
        var actor=await authority.RequireAsync(cancellationToken);var digest=ReviewConcurrency.Digest(new{kind="retry",operationId,actor.UserId,actor.PartyId,request});
        return await db.InTransactionAsync(async ct=>
        {
            var op=await db.PublicationOperations.SingleOrDefaultAsync(o=>o.TenantId==actor.TenantId&&o.Id==operationId,ct)??throw PublicationAuthority.Missing();
            authority.Channel(actor,op.ChannelKey,op.Kind=="withdraw"?"withdraw":"publish");
            var old=await Replay(actor,request.OperationId,digest,ct);if(old is not null)return Map(op);
            Version(op.RowVersion,request.ExpectedVersion);
            if(op.Kind is not ("publish" or "withdraw")||op.Status=="committed"||op.ClaimExpiresAt>clock.UtcNow)throw Conflict("publication-not-retryable");
            if(op.RetryCycles>=10)throw PublicationAuthority.Blocked("publication-retry-limit");
            var c=options.Value.Channels[op.ChannelKey];if(op.Kind=="withdraw")PublicationAuthority.RequireWithdrawal(c,options.Value);else PublicationAuthority.RequireDelivery(c,options.Value);
            if(op.Kind=="publish")
            {
                var submission=await Submission(actor,op.SubmissionId!.Value,ct);Independent(actor,submission);
                var approved=await authority.ApprovalAsync(submission,ct);
                if(approved.Revision.Id!=op.RevisionId||approved.Decision.Id!=op.DecisionId||approved.Decision.ReviewDigest!=op.ReviewDigest)throw Conflict("publication-approval-changed");
                if(op.PolicyJson!=PublicationAuthority.DeliveryPolicy(c,options.Value))throw PublicationAuthority.Blocked("publication-policy-changed");
                submission.Status=SubmissionStatuses.Publishing;submission.ActivePublicationOperationId=op.Id;
            }
            var audit=New(actor,op.ChannelKey,request.OperationId,digest,"retry");audit.ParentOperationId=op.Id;audit.Reason=Reason(request.Reason);audit.Status="committed";audit.Code="retry-queued";audit.CompletedAt=clock.UtcNow;
            db.PublicationOperations.Add(audit);op.Status="pending";op.Code="retry-queued";op.Attempts=0;op.RetryCycles++;op.ClaimToken=null;op.ClaimExpiresAt=null;
            db.EnqueueIntegrationEvent(new PublicationOperationRequestedEvent(audit.Id,clock.UtcNow,actor.TenantId,op.Id));
            await db.SaveChangesAsync(ct);return Map(op);
        },cancellationToken);
    }
    public async Task<PublicationOperationResponse> WithdrawAsync(Guid editionId,PublicationActionRequest request,CancellationToken cancellationToken=default)
    {
        var actor=await authority.RequireAsync(cancellationToken);var digest=ReviewConcurrency.Digest(new{kind="withdraw",editionId,actor.UserId,actor.PartyId,request});
        return await db.InTransactionAsync(async ct=>
        {
            var edition=await db.PublishedEditions.SingleOrDefaultAsync(e=>e.TenantId==actor.TenantId&&e.Id==editionId,ct)??throw PublicationAuthority.Missing();
            var c=authority.Channel(actor,edition.ChannelKey,"withdraw");PublicationAuthority.RequireWithdrawal(c,options.Value);
            var old=await Replay(actor,request.OperationId,digest,ct);
            if(old is not null)
            {
                // Exact, authorised replay also reconciles receipts left stale by
                // older workers. The committed operation and catalogue never change.
                if(old.Status=="committed"&&old.CompletedAt is { } completedAt)
                {
                    var baseScope=edition.Slot=="world"||edition.Kind=="part";
                    var retired=await db.PublishedEditions.Where(row=>row.TenantId==actor.TenantId&&row.ChannelKey==edition.ChannelKey
                        &&row.WorldId==edition.WorldId&&!row.IsCurrent&&row.WithdrawnAt>=old.CreatedAt&&row.WithdrawnAt<=completedAt
                        &&(row.Id==edition.Id||(baseScope&&(row.Slot=="world"||row.Kind=="part")))).ToListAsync(ct);
                    await PublicationWithdrawalReceipts.ApplyAsync(db,actor.TenantId,retired,ct);
                    await db.SaveChangesAsync(ct);
                }
                return Map(old);
            }
            Version(edition.RowVersion,request.ExpectedVersion);if(!edition.IsCurrent)throw Conflict("publication-entry-changed");
            if(await db.PublicationOperations.AnyAsync(o=>o.TenantId==actor.TenantId&&o.EditionId==edition.Id&&o.Kind=="withdraw"&&(o.Status=="pending"||o.Status=="copying"||o.Status=="uncertain"),ct))throw Conflict("withdrawal-in-progress");
            var op=New(actor,edition.ChannelKey,request.OperationId,digest,"withdraw");op.EditionId=edition.Id;op.SubmissionId=edition.SubmissionId;op.RevisionId=edition.RevisionId;
            op.ExpectedVersion=request.ExpectedVersion;op.ExpectedSlotFingerprint=edition.Fingerprint??edition.PackageSha256;op.Reason=Reason(request.Reason);op.PolicyJson=PublicationAuthority.DeliveryPolicy(c,options.Value);
            db.PublicationOperations.Add(op);db.EnqueueIntegrationEvent(new PublicationOperationRequestedEvent(op.Id,clock.UtcNow,actor.TenantId,op.Id));
            await db.SaveChangesAsync(ct);return Map(op);
        },cancellationToken);
    }
    public async Task<PublicationOperationResponse> HoldAsync(Guid submissionId,SetPublicationHoldRequest request,CancellationToken cancellationToken=default)
    {
        var actor=await authority.RequireAsync(cancellationToken);var digest=ReviewConcurrency.Digest(new{kind="hold",submissionId,actor.UserId,actor.PartyId,request});
        return await db.InTransactionAsync(async ct=>
        {
            var submission=await Submission(actor,submissionId,ct);authority.Channel(actor,submission.ChannelKey,"withdraw");
            var old=await Replay(actor,request.OperationId,digest,ct);if(old is not null)return Map(old);
            ReviewConcurrency.RequireVersion(submission,request.ExpectedVersion);var reason=Reason(request.Reason);
            submission.PublicationHoldReason=request.Hold?reason:null;
            var op=New(actor,submission.ChannelKey,request.OperationId,digest,request.Hold?"hold":"release-hold");op.SubmissionId=submissionId;op.RevisionId=submission.CurrentRevisionId;
            op.Reason=reason;op.Status="committed";op.Code=request.Hold?"future-publication-held":"hold-cleared";op.CompletedAt=clock.UtcNow;db.PublicationOperations.Add(op);
            await db.SaveChangesAsync(ct);return Map(op);
        },cancellationToken);
    }
    private PublicationOperation New(PublicationActor actor,string channel,Guid id,string digest,string kind)
    {
        if(id==Guid.Empty)throw new PublishingException(422,"invalid-operation","A stable operation UUID is required.");
        return new(){TenantId=actor.TenantId,ChannelKey=channel,ClientOperationId=id,CommandDigest=digest,Kind=kind,ActorUserId=actor.UserId,ActorPartyId=actor.PartyId};
    }
    public async Task<PublicationOperationResponse> RetryMirrorAsync(string channelKey,PublicationActionRequest request,CancellationToken cancellationToken=default)
    {
        var actor=await authority.RequireAsync(cancellationToken);var c=authority.Channel(actor,channelKey,"publish");
        if(!c.LegacyMirrorEnabled)throw new PublishingException(503,"mirror-disabled","This channel has no enabled legacy mirror.");
        var digest=ReviewConcurrency.Digest(new{kind="mirror-retry",channelKey,actor.UserId,actor.PartyId,request});
        return await db.InTransactionAsync(async ct=>
        {
            var old=await Replay(actor,request.OperationId,digest,ct);if(old is not null)return Map(old);
            var state=await State(actor.TenantId,channelKey,ct);Version(state.RowVersion,request.ExpectedVersion);
            if(state.MirrorClaimExpiresAt>clock.UtcNow)throw Conflict("mirror-in-progress");
            var op=New(actor,channelKey,request.OperationId,digest,"mirror-retry");op.Reason=Reason(request.Reason);op.Status="committed";op.Code="mirror-retry-queued";
            op.CompletedAt=clock.UtcNow;op.CommittedSerial=state.Serial;op.CommittedSnapshotId=state.HeadSnapshotId;db.PublicationOperations.Add(op);
            state.MirrorStatus="pending";state.MirrorCode="retry-queued";
            db.EnqueueIntegrationEvent(new PublicationCatalogueCommittedEvent(op.Id,clock.UtcNow,actor.TenantId,channelKey,state.Serial));await db.SaveChangesAsync(ct);return Map(op);
        },cancellationToken);
    }
    private async Task<PublicationOperation?> Replay(PublicationActor actor,Guid id,string digest,CancellationToken ct)
    {
        if(id==Guid.Empty)throw new PublishingException(422,"invalid-operation","A stable operation UUID is required.");
        var old=await db.PublicationOperations.SingleOrDefaultAsync(o=>o.TenantId==actor.TenantId&&o.ClientOperationId==id,ct);
        if(old is not null&&old.CommandDigest!=digest)throw Conflict("publication-operation-conflict");return old;
    }
    private Task<PublicationSubmission> Submission(PublicationActor actor,Guid id,CancellationToken ct)=>FindSubmission(actor,id,ct);
    private async Task<PublicationSubmission> FindSubmission(PublicationActor actor,Guid id,CancellationToken ct)
        =>await db.Submissions.SingleOrDefaultAsync(s=>s.TenantId==actor.TenantId&&s.Id==id,ct)??throw PublicationAuthority.Missing();
    private async Task<PublicationChannelState> State(Guid tenant,string channel,CancellationToken ct)
        =>await db.PublicationChannels.SingleOrDefaultAsync(s=>s.TenantId==tenant&&s.ChannelKey==channel,ct)??throw new PublishingException(503,"baseline-required","Initialise the qualified authoritative baseline before publishing.");
    private PublicationChannelResponse ChannelResponse(PublicationActor a,string key,PublishingChannelOptions c,PublicationChannelState? state)
    {
        string? readiness=null;try{PublicationAuthority.RequireDelivery(c,options.Value);}catch(PublishingException e){readiness=e.Code;}
        readiness??=state is null?"baseline-required":null;
        return new(key,a.CanReview&&c.ReviewEnabled&&c.ReviewerUserIds.Contains(a.UserId),a.CanPublish&&c.PublisherUserIds.Contains(a.UserId),a.CanWithdraw&&c.PublisherUserIds.Contains(a.UserId),
            c.DeliveryEnabled,c.PublicCatalogueEnabled,state is not null,state?.Serial,state is null?null:Convert.ToBase64String(state.RowVersion),c.LegacyMirrorEnabled?state?.MirrorStatus:"disabled",c.LegacyMirrorEnabled?state?.MirrorCode:"not-configured",state?.MirroredSerial,readiness,c.BaselineSha256,
            Uri.TryCreate(c.PublicBaseUrl,UriKind.Absolute,out var origin)&&origin.Scheme=="https"?c.PublicBaseUrl.TrimEnd('/')+"/publishing/channels/"+key+"/catalogue.json":null);
    }
    internal PublicationOperationResponse Map(PublicationOperation o)=>new(o.Id,o.ChannelKey,o.Kind,o.SubmissionId,o.RevisionId,o.DecisionId,o.EditionId,o.Status,o.Code,o.Attempts,o.CommittedSerial,
        o.CreatedAt,o.CompletedAt,o.Reason,Convert.ToBase64String(o.RowVersion),o.Kind is "publish" or "withdraw"&&o.Status!="committed"&&!(o.ClaimExpiresAt>clock.UtcNow));
    private static PublishedEditionResponse Edition(PublishedEdition e,long serial)=>new(e.Id,e.WorldId,e.Slot,e.Kind,e.Title,e.SubmissionId,e.RevisionId,e.DecisionId,e.PackageSha256,e.Fingerprint,e.IsLegacy,e.IsCurrent,e.WithdrawnAt,Convert.ToBase64String(e.RowVersion),e.PinJson,serial);
    private static PublicRevisionMetadata Metadata(SubmissionRevision r)
    {
        var material=r.EditorialMaterialJson is null?null:JsonSerializer.Deserialize<PublicationEditorialMaterial>(r.EditorialMaterialJson);
        return new(r.Id,r.Number,r.Title,r.CreatorDisplayName,r.Edition,r.DeclaredPackageSha256,r.DeclaredPackageBytes,material?.Audience,material?.AgeRating,material?.ReleaseKind);
    }
    internal static void Version(byte[] actual,string? expected){if(expected!=Convert.ToBase64String(actual)||actual.Length==0)throw Conflict("publication-version-changed");}
    internal static PublishingException Conflict(string code)=>new(409,code,"Publication material or state changed. Reload it before confirming another operation.");
    private static string Reason(string value)=>EditorialMaterialRules.Note(value,2000);
    private static void Independent(PublicationActor actor,PublicationSubmission submission)
    {if(actor.UserId==submission.OriginatingUserId||actor.PartyIds.Contains(submission.CreatorPartyId))throw new PublishingException(403,"self-publication-forbidden","An independent publisher must release the approved edition.");}
}
