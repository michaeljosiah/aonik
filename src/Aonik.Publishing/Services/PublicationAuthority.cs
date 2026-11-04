using System.Text.Json;
using System.Text.RegularExpressions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Aonik.Publishing.Contracts;
using Aonik.Publishing.Entities;
using Aonik.Publishing.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Modules;

namespace Aonik.Publishing.Services;

internal sealed class PublicationAuthority(IPublishingDataContext db, ITenantProvider tenantProvider, ICurrentUserContext user,
    ICurrentPartyResolver parties, IUserPartyMembershipReader memberships, IPermissionService permissions,
    IModuleGate modules, IOptions<PublishingOptions> options)
{
    public async Task<PublicationActor> RequireAsync(CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.UserId is not { } id || id == Guid.Empty) throw new PublishingException(401,"unauthenticated","Sign in as an assigned publisher.");
        var tenant = tenantProvider.GetCurrentTenantId();
        if (tenant == Guid.Empty || user.TenantId != tenant) throw Forbidden();
        await modules.EnsureEnabledAsync(tenant,ModuleIds.Publishing,ct);
        var party = await parties.GetCurrentPartyIdAsync(ct);
        var links = await memberships.GetPartyIdsAsync(tenant,id,ct);
        if (party is null || party == Guid.Empty || !links.Contains(party.Value)) throw Forbidden();
        var review = await permissions.HasPermissionAsync(id,"Publishing.Review",ct);
        var publish = await permissions.HasPermissionAsync(id,"Publishing.Publish",ct);
        var withdraw = await permissions.HasPermissionAsync(id,"Publishing.Withdraw",ct);
        if (!review && !publish && !withdraw) throw Forbidden();
        return new(tenant,id,party.Value,links,review,publish,withdraw);
    }

    public PublishingChannelOptions Channel(PublicationActor actor,string key,string? action=null)
    {
        if (!options.Value.Channels.TryGetValue(key,out var c) || c.TenantId!=actor.TenantId) throw Missing();
        var review=actor.CanReview&&c.ReviewEnabled&&c.ReviewerUserIds.Contains(actor.UserId);
        var publish=actor.CanPublish&&c.PublisherUserIds.Contains(actor.UserId);
        var withdraw=actor.CanWithdraw&&c.PublisherUserIds.Contains(actor.UserId);
        if (action=="publish" ? !publish : action=="withdraw" ? !withdraw : !review&&!publish&&!withdraw) throw Missing();
        return c;
    }

    public async Task RequireStoredActorAsync(PublicationOperation op,PublicationSubmission? submission,CancellationToken ct)
    {
        if (tenantProvider.GetCurrentTenantId()!=op.TenantId) throw new InvalidOperationException("Publication tenant restoration mismatch.");
        await modules.EnsureEnabledAsync(op.TenantId,ModuleIds.Publishing,ct);
        var c=options.Value.Channels.GetValueOrDefault(op.ChannelKey)??throw Missing();
        var permission=op.Kind=="withdraw"?"Publishing.Withdraw":"Publishing.Publish";
        var links=await memberships.GetPartyIdsAsync(op.TenantId,op.ActorUserId,ct);
        if (c.TenantId!=op.TenantId||!c.PublisherUserIds.Contains(op.ActorUserId)||!links.Contains(op.ActorPartyId)
            ||!await permissions.HasPermissionAsync(op.ActorUserId,permission,ct))throw Forbidden();
        if (submission is not null && op.Kind=="publish" && (submission.OriginatingUserId==op.ActorUserId||links.Contains(submission.CreatorPartyId)))
            throw new PublishingException(403,"self-publication-forbidden","An independent publisher must release the approved edition.");
    }

    public async Task<ApprovedPublicationMaterial> ApprovalAsync(PublicationSubmission submission,CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(submission.PublicationHoldReason)) throw Blocked("publication-held");
        if (submission.Status is not (SubmissionStatuses.Approved or SubmissionStatuses.Publishing)) throw Blocked("active-approval-required");
        var c=options.Value.Channels.GetValueOrDefault(submission.ChannelKey)??throw Missing();
        RequireDelivery(c,options.Value);
        var revision=await db.Revisions.SingleAsync(r=>r.TenantId==submission.TenantId&&r.SubmissionId==submission.Id&&r.Id==submission.CurrentRevisionId,ct);
        var decision=await db.EditorialDecisions.SingleOrDefaultAsync(d=>d.TenantId==submission.TenantId&&d.SubmissionId==submission.Id&&d.RevisionId==revision.Id
            &&d.Id==submission.ActiveEditorialDecisionId&&d.Decision=="approve",ct)??throw Blocked("active-approval-required");
        var reviewerLinks=await memberships.GetPartyIdsAsync(submission.TenantId,decision.ActorUserId,ct);
        if (!c.ReviewEnabled||!c.ReviewerUserIds.Contains(decision.ActorUserId)||!reviewerLinks.Contains(decision.ActorPartyId)
            ||reviewerLinks.Contains(submission.CreatorPartyId)||decision.ActorUserId==submission.OriginatingUserId||decision.ActorUserId==revision.OriginatingUserId
            ||!await permissions.HasPermissionAsync(decision.ActorUserId,"Publishing.Review",ct))throw Blocked("approval-authority-changed");
        var report=await db.ValidationReports.SingleOrDefaultAsync(r=>r.TenantId==submission.TenantId&&r.SubmissionId==submission.Id&&r.RevisionId==revision.Id&&r.Id==revision.CurrentValidationReportId,ct);
        var space=await db.Namespaces.SingleOrDefaultAsync(n=>n.TenantId==submission.TenantId&&n.ChannelKey==submission.ChannelKey&&n.WorldId==revision.WorldId,ct);
        var evidence=EditorialReviewEvidence.Build(submission,revision,report,space,c,options.Value);
        var snapshot=await db.ReviewSnapshots.SingleOrDefaultAsync(s=>s.TenantId==submission.TenantId&&s.Id==decision.SnapshotId&&s.RevisionId==revision.Id,ct);
        if (snapshot is null||decision.ReviewDigest!=evidence.Digest||snapshot.ReviewDigest!=evidence.Digest||snapshot.MaterialJson!=evidence.Json
            ||decision.PolicyVersion!=c.ReviewPolicyVersion||decision.ChecksJson is null
            ||JsonSerializer.Deserialize<EditorialApprovalChecks>(decision.ChecksJson) is not {DistributionRightsReviewed:true,AttributionReviewed:true,AudienceSafetyReviewed:true})
            throw Blocked("approval-material-changed");
        var material=revision.EditorialMaterialJson is null?null:JsonSerializer.Deserialize<PublicationEditorialMaterial>(revision.EditorialMaterialJson);
        if (EditorialMaterialRules.ApprovalBlock(new(revision.DistributionPermissionGranted,material),evidence.Package) is { } code)throw Blocked(code);
        return new(submission,revision,decision,evidence.Package,material!);
    }

    public static string DeliveryPolicy(PublishingChannelOptions c,PublishingOptions o)=>JsonSerializer.Serialize(new{version=1,c.DeliveryPolicyVersion,c.PublicBaseUrl,c.BaselineSha256,
        o.Delivery.LeaseMinutes,o.Delivery.MaxAttempts,o.Delivery.MaxPictures});
    public static void RequireDelivery(PublishingChannelOptions c,PublishingOptions o)
    {
        if (!c.DeliveryEnabled)throw new PublishingException(503,"delivery-disabled","Publication delivery is paused for this channel.");
        if (!c.PublicCatalogueEnabled||c.PublicBaseUrl.Length>1800||string.IsNullOrWhiteSpace(c.DeliveryPolicyVersion)||c.DeliveryPolicyVersion.Length>120||c.DeliveryPolicyVersion.Any(char.IsControl)
            ||!Uri.TryCreate(c.PublicBaseUrl,UriKind.Absolute,out var uri)||uri.Scheme!="https"||string.IsNullOrEmpty(uri.Host)||uri.UserInfo!=""||uri.Query!=""||uri.Fragment!=""
            ||string.IsNullOrWhiteSpace(o.Delivery.PublicRoot)||!Path.IsPathFullyQualified(o.Delivery.PublicRoot)
            ||o.Delivery.LeaseMinutes is <5 or >60||o.Delivery.MaxAttempts is <1 or >10||o.Delivery.MaxPictures is <1 or >1024
            ||!Regex.IsMatch(c.BaselineSha256,"^[a-f0-9]{64}$"))throw new PublishingException(503,"delivery-not-configured","Qualify immutable public storage, baseline and delivery policy first.");
    }
    public static void RequireWithdrawal(PublishingChannelOptions c,PublishingOptions o)
    {
        if(string.IsNullOrWhiteSpace(c.DeliveryPolicyVersion)||c.DeliveryPolicyVersion.Length>120
            ||o.Delivery.LeaseMinutes is <5 or >60||o.Delivery.MaxAttempts is <1 or >10)
            throw new PublishingException(503,"withdrawal-not-configured","Qualify the channel's publication and withdrawal policy first.");
    }
    public static PublishingException Blocked(string code)=>new(422,code,"The exact active approval, publication policy and lineage must remain valid.");
    public static PublishingException Missing()=>new(404,"publication-unavailable","No authorised publication is available.");
    private static PublishingException Forbidden()=>new(403,"publication-forbidden","An assigned publisher with the required permission and active party is required.");
}

internal sealed record PublicationActor(Guid TenantId,Guid UserId,Guid PartyId,IReadOnlyList<Guid> PartyIds,bool CanReview,bool CanPublish,bool CanWithdraw);
internal sealed record ApprovedPublicationMaterial(PublicationSubmission Submission,SubmissionRevision Revision,EditorialDecision Decision,
    PackageVerificationResult Package,PublicationEditorialMaterial Material);
