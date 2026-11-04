using System.Text.Json;
using System.Text.RegularExpressions;
using System.IO.Compression;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Aonik.Publishing.Contracts;
using Aonik.Publishing.Entities;
using Aonik.Publishing.Persistence;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Publishing.Services;

internal sealed class EditorialReviewService(IPublishingDataContext db, EditorialReviewAuthorization authorization,
    IPublishingObjectStore objects, IOptions<PublishingOptions> options, IClock clock) : IEditorialReviewService
{
    public async Task<EditorialReviewPage> ListAsync(string channelKey, string? status = null, int page = 1, int pageSize = 25, CancellationToken cancellationToken = default, string? query = null)
    {
        var actor = await authorization.RequireAsync(cancellationToken);
        authorization.RequireChannel(actor, channelKey);
        RequirePage(page, pageSize);
        if (status is not (null or "in-review" or "held" or "changes-requested" or "rejected" or "approved" or "validating"))
            throw new PublishingException(422, "invalid-review-filter", "Choose a supported review status.");
        var search = EditorialMaterialRules.Text(query, 200, false).ToLowerInvariant();
        var source = (from s in db.Submissions.AsNoTracking()
            join r in db.Revisions.AsNoTracking() on s.CurrentRevisionId equals r.Id
            where s.TenantId == actor.TenantId && r.TenantId == actor.TenantId && r.SubmissionId == s.Id && s.ChannelKey == channelKey
                && s.OriginatingUserId != actor.UserId && r.OriginatingUserId != actor.UserId && r.CreatedBy != actor.UserId
                && !actor.PartyIds.Contains(s.CreatorPartyId) && (status == null || s.Status == status)
                && (search == "" || r.Title.ToLower().Contains(search) || r.CreatorDisplayName.ToLower().Contains(search) || r.WorldId.Contains(search))
            orderby s.CreatedAt, s.Id
            select new { Submission = s, Revision = r });
        var total = await source.CountAsync(cancellationToken);
        var rows = await source.Skip((page-1)*pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new(page, pageSize, rows.Select(r => CreatorSubmissionService.Map(r.Submission, r.Revision)).ToArray(), total);
    }

    public async Task<EditorialReviewDetail> GetAsync(Guid submissionId, CancellationToken cancellationToken = default)
    {
        var actor = await authorization.RequireAsync(cancellationToken);
        return await db.InTransactionAsync(async ct =>
        {
            var (submission, revision, channel) = await LoadAsync(actor, submissionId, ct);
            var report = await ReportAsync(revision, ct);
            EditorialReviewSnapshot? snapshot = null;
            string? blocked = null; string? approvalBlocked = null;
            try
            {
                var evidence = await EvidenceAsync(submission, revision, report, channel, ct);
                snapshot = await db.ReviewSnapshots.SingleOrDefaultAsync(s => s.TenantId == actor.TenantId && s.RevisionId == revision.Id && s.ReviewDigest == evidence.Digest, ct);
                if (snapshot is null)
                {
                    snapshot = new() { TenantId = actor.TenantId, SubmissionId = submission.Id, RevisionId = revision.Id,
                        ValidationReportId = report!.Id, ReviewDigest = evidence.Digest, MaterialJson = evidence.Json, PolicyJson = evidence.Policy };
                    db.ReviewSnapshots.Add(snapshot); await db.SaveChangesAsync(ct);
                }
                approvalBlocked = EditorialMaterialRules.ApprovalBlock(new(revision.DistributionPermissionGranted, Material(revision)), evidence.Package);
            }
            catch (PublishingException ex) when (ex.Status == 422) { blocked = ex.Code; }
            if (submission.Status != SubmissionStatuses.InReview) blocked ??= "submission-not-in-review";
            var history = await db.EditorialDecisions.AsNoTracking().Where(d => d.TenantId == actor.TenantId && d.SubmissionId == submission.Id)
                .OrderByDescending(d => d.DecidedAt).ThenByDescending(d => d.Id).Take(100).ToListAsync(ct);
            var uploads = await db.UploadSessions.AsNoTracking().Where(s => s.TenantId == actor.TenantId && s.SubmissionId == submission.Id && s.RevisionId == revision.Id)
                .OrderByDescending(s => s.CreatedAt).Take(20).ToListAsync(ct);
            var activity = uploads.Select(s => new EditorialActivity("Upload", s.Status, s.UpdatedAt ?? s.CreatedAt,
                $"{s.ReceivedBytes} of {s.DeclaredBytes} bytes received")).ToList();
            if (revision.UploadedAt is { } uploaded) activity.Add(new("Package uploaded", "uploaded", uploaded, "The declared archive digest and length matched the received bytes."));
            if (report is not null) activity.Add(new("Package verification", report.Status, report.CompletedAt ?? report.CreatedAt, report.Code));
            return new EditorialReviewDetail(CreatorSubmissionService.Map(submission, revision), report is null ? null : PublicationIntakeService.Preview(report),
                snapshot is null ? null : new(snapshot.Id, snapshot.ReviewDigest, snapshot.DigestVersion, snapshot.ValidationReportId, snapshot.MaterialJson, snapshot.CreatedAt),
                blocked, approvalBlocked, history.Select(d => Map(d, submission)).ToArray(), activity.OrderByDescending(a => a.At).ToArray(), revision.UploadedAt.HasValue && revision.CurrentUploadSessionId.HasValue);
        }, cancellationToken);
    }

    public async Task<EditorialDecisionResponse> DecideAsync(Guid submissionId, EditorialDecisionRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await authorization.RequireAsync(cancellationToken);
        request = Normalize(request);
        var digest = ReviewConcurrency.Digest(new { version = 1, submissionId, actor.UserId, actor.PartyId, request });
        try
        {
            return await db.InTransactionAsync(async ct =>
            {
                var (submission, revision, channel) = await LoadAsync(actor, submissionId, ct);
                var old = await db.EditorialDecisions.SingleOrDefaultAsync(d => d.TenantId == actor.TenantId && d.SubmissionId == submissionId && d.OperationId == request.OperationId, ct);
                if (old is not null)
                {
                    if (old.CommandDigest != digest) throw new PublishingException(409, "decision-operation-conflict", "This operation already names different decision arguments or actor.");
                    return Map(old, submission);
                }
                ReviewConcurrency.RequireVersion(submission, request.ExpectedVersion);
                if (revision.Id != request.RevisionId) throw new PublishingException(409, "review-revision-changed", "Review the current revision before deciding.");
                if (submission.Status != SubmissionStatuses.InReview) throw new PublishingException(409, "submission-not-in-review", "Only an undecided revision in review can receive an editorial decision.");
                var report = await ReportAsync(revision, ct);
                var evidence = await EvidenceAsync(submission, revision, report, channel, ct);
                if (request.ReviewDigest != evidence.Digest) throw new PublishingException(409, "review-material-changed", "Reload the exact package and review snapshot before deciding.");
                var snapshot = await db.ReviewSnapshots.SingleOrDefaultAsync(s => s.TenantId == actor.TenantId && s.RevisionId == revision.Id && s.ReviewDigest == evidence.Digest, ct)
                    ?? throw new PublishingException(409, "review-snapshot-required", "Open the review snapshot before deciding.");
                if (snapshot.MaterialJson != evidence.Json || snapshot.PolicyJson != evidence.Policy || snapshot.ValidationReportId != report!.Id)
                    throw EditorialReviewEvidence.Blocked("review-snapshot-invalid");
                if (request.Decision == "approve")
                {
                    var code = EditorialMaterialRules.ApprovalBlock(new(revision.DistributionPermissionGranted, Material(revision)), evidence.Package);
                    if (code is not null) throw EditorialReviewEvidence.Blocked(code);
                    if (request.Checks is not { DistributionRightsReviewed: true, AttributionReviewed: true, AudienceSafetyReviewed: true })
                        throw EditorialReviewEvidence.Blocked("editorial-checks-required");
                }
                var decision = new EditorialDecision
                {
                    TenantId = actor.TenantId, SubmissionId = submissionId, RevisionId = revision.Id, SnapshotId = snapshot.Id,
                    OperationId = request.OperationId, CommandDigest = digest, ReviewDigest = evidence.Digest, ExpectedVersion = request.ExpectedVersion,
                    Decision = request.Decision, InternalReason = request.InternalReason, CreatorMessage = request.CreatorMessage,
                    ActorUserId = actor.UserId, ActorPartyId = actor.PartyId, PolicyVersion = channel.ReviewPolicyVersion,
                    ChecksJson = request.Checks is null ? null : JsonSerializer.Serialize(request.Checks), DecidedAt = clock.UtcNow,
                };
                db.EditorialDecisions.Add(decision);
                submission.ActiveEditorialDecisionId = decision.Id;
                submission.Status = request.Decision switch { "approve" => SubmissionStatuses.Approved, "request-changes" => SubmissionStatuses.ChangesRequested, _ => SubmissionStatuses.Rejected };
                await db.SaveChangesAsync(ct);
                return Map(decision, submission);
            }, cancellationToken);
        }
        catch (DbUpdateConcurrencyException) { throw new PublishingException(409, "review-version-changed", "Another decision or revision won. Reload the submission."); }
    }

    public Task<(Stream Content, string FileName)> DownloadAsync(Guid submissionId, Guid revisionId, CancellationToken cancellationToken = default)
        => OpenPackageAsync(submissionId, revisionId, null, cancellationToken);

    private async Task<(Stream Content, string FileName)> OpenPackageAsync(Guid submissionId, Guid revisionId, string? assetId, CancellationToken cancellationToken)
    {
        var actor = await authorization.RequireAsync(cancellationToken);
        var submission = await db.Submissions.AsNoTracking().SingleOrDefaultAsync(s => s.TenantId == actor.TenantId && s.Id == submissionId, cancellationToken) ?? throw Missing();
        authorization.RequireChannel(actor, submission.ChannelKey);
        var revision = await db.Revisions.AsNoTracking().SingleOrDefaultAsync(r => r.TenantId == actor.TenantId && r.SubmissionId == submissionId && r.Id == revisionId, cancellationToken) ?? throw Missing();
        EditorialReviewAuthorization.RequireIndependent(actor, submission, revision);
        var session = await db.UploadSessions.AsNoTracking().SingleOrDefaultAsync(s => s.TenantId == actor.TenantId && s.Id == revision.CurrentUploadSessionId
            && s.SubmissionId == submissionId && s.RevisionId == revisionId && s.Status == "uploaded", cancellationToken) ?? throw Missing();
        if (session.StorageLocationId != objects.LocationId) throw new PublishingException(503, "storage-location-changed", "The owning private package store is unavailable.");
        var input = await objects.OpenPackageAsync(actor.TenantId, session.Id, revision.DeclaredPackageSha256, revision.DeclaredPackageBytes, cancellationToken);
        var stream = new FileStream(input.Path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
        try
        {
            db.EditorialPackageAccesses.Add(new() { TenantId = actor.TenantId, SubmissionId = submissionId, RevisionId = revisionId,
                ActorUserId = actor.UserId, ActorPartyId = actor.PartyId, PackageSha256 = input.Sha256, AccessedAt = clock.UtcNow, AccessKind = assetId is null ? "package" : "image", AssetId = assetId });
            await db.SaveChangesAsync(cancellationToken);
            return (stream, revision.PackageFileName);
        }
        catch { await stream.DisposeAsync(); throw; }
    }

    public async Task<(Stream Content,string MediaType)> ImageAsync(Guid submissionId,Guid revisionId,string assetId,CancellationToken cancellationToken=default)
    {
        var actor=await authorization.RequireAsync(cancellationToken);
        var (submission,revision,_)=await LoadAsync(actor,submissionId,cancellationToken);
        if(revision.Id!=revisionId)throw new PublishingException(409,"review-revision-changed","Open the current revision before viewing its private artwork.");
        var report=await ReportAsync(revision,cancellationToken);
        var result=report?.Status=="passed"?JsonSerializer.Deserialize<PackageVerificationResult>(report.ResultJson):null;
        var asset=result?.Assets.SingleOrDefault(a=>a.Id==assetId);
        if(asset is null||asset.BytesOutsidePictureLimit())throw new PublishingException(404,"preview-unavailable","No verified private picture is available.");
        var package=await OpenPackageAsync(submission.Id,revision.Id,assetId,cancellationToken);
        using var zip=new ZipArchive(package.Content,ZipArchiveMode.Read);
        var entry=zip.GetEntry(asset.Path)??throw new PublishingException(422,"preview-unavailable","The exact verified artwork is unavailable.");
        var output=new MemoryStream();
        try
        {
            await using var input=entry.Open();await PublicationBytes.CopyAsync(input,output,asset.ByteLength,asset.Sha256,cancellationToken);output.Position=0;
            return(output,asset.MediaType);
        }
        catch{await output.DisposeAsync();throw;}
    }

    private async Task<(PublicationSubmission Submission, SubmissionRevision Revision, PublishingChannelOptions Channel)> LoadAsync(ReviewerActor actor, Guid id, CancellationToken ct)
    {
        var submission = await db.Submissions.SingleOrDefaultAsync(s => s.TenantId == actor.TenantId && s.Id == id, ct) ?? throw Missing();
        var channel = authorization.RequireChannel(actor, submission.ChannelKey);
        var revision = await db.Revisions.SingleAsync(r => r.TenantId == actor.TenantId && r.SubmissionId == id && r.Id == submission.CurrentRevisionId, ct);
        EditorialReviewAuthorization.RequireIndependent(actor, submission, revision);
        return (submission, revision, channel);
    }
    private Task<PublicationValidationReport?> ReportAsync(SubmissionRevision revision, CancellationToken ct)
        => db.ValidationReports.SingleOrDefaultAsync(r => r.TenantId == revision.TenantId && r.SubmissionId == revision.SubmissionId && r.RevisionId == revision.Id && r.Id == revision.CurrentValidationReportId, ct);
    private async Task<(string Json, string Policy, string Digest, PackageVerificationResult Package)> EvidenceAsync(PublicationSubmission submission,
        SubmissionRevision revision, PublicationValidationReport? report, PublishingChannelOptions channel, CancellationToken ct)
    {
        var space = await db.Namespaces.AsNoTracking().SingleOrDefaultAsync(n => n.TenantId == submission.TenantId && n.ChannelKey == submission.ChannelKey && n.WorldId == revision.WorldId, ct);
        return EditorialReviewEvidence.Build(submission, revision, report, space, channel, options.Value);
    }
    private static PublicationEditorialMaterial? Material(SubmissionRevision revision)
        => revision.EditorialMaterialJson is null ? null : JsonSerializer.Deserialize<PublicationEditorialMaterial>(revision.EditorialMaterialJson);
    private static EditorialDecisionRequest Normalize(EditorialDecisionRequest request)
    {
        if (request.OperationId == Guid.Empty || request.RevisionId == Guid.Empty || request.ReviewDigest is null
            || !Regex.IsMatch(request.ReviewDigest, "^[a-f0-9]{64}$") || request.Decision is not ("approve" or "request-changes" or "reject")
            || request.ExpectedVersion is not { Length: >= 8 and <= 64 })
            throw new PublishingException(422, "invalid-editorial-decision", "Provide the exact revision, digest, version, operation and supported decision.");
        return request with { InternalReason = EditorialMaterialRules.Note(request.InternalReason, 4000),
            CreatorMessage = EditorialMaterialRules.Note(request.CreatorMessage, 2000, request.Decision != "approve") };
    }
    internal static void RequirePage(int page, int size)
    {
        if (page is < 1 or > 10000 || size is < 1 or > 100) throw new PublishingException(422, "invalid-page", "Page must be 1–10000 and page size 1–100.");
    }
    private static PublishingException Missing() => new(404, "review-unavailable", "No authorised publisher review is available.");
    private static EditorialDecisionResponse Map(EditorialDecision d, PublicationSubmission submission)
        => new(d.Id, d.OperationId, d.RevisionId, d.ReviewDigest, d.Decision, d.InternalReason, d.CreatorMessage, d.ActorUserId, d.ActorPartyId,
            d.PolicyVersion, d.DecidedAt, d.ChecksJson is null ? null : JsonSerializer.Deserialize<EditorialApprovalChecks>(d.ChecksJson),
            submission.CurrentRevisionId == d.RevisionId && submission.ActiveEditorialDecisionId == d.Id);
}
