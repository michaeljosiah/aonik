using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Aonik.Publishing.Contracts;
using Aonik.Publishing.Entities;
using Aonik.Publishing.Persistence;

namespace Aonik.Publishing.Services;

internal sealed class SubmissionRevisionService(IPublishingDataContext db, CreatorSubmissionService creators,
    IOptions<PublishingOptions> options) : ISubmissionRevisionService
{
    public async Task<SubmissionReceipt> ReplaceAsync(Guid submissionId, ReplaceSubmissionRevisionRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await creators.RequireActorAsync(true, cancellationToken);
        if (request.OperationId == Guid.Empty || request.ExpectedRevisionId == Guid.Empty || request.Declaration is null
            || request.Declaration.ClientRequestId != request.OperationId)
            throw new PublishingException(422, "invalid-revision-operation", "A replacement declaration must use its stable operation ID as client request ID.");
        var declaration = SubmissionDeclaration.Normalize(request.Declaration);
        var declarationDigest = SubmissionDeclaration.Digest(declaration);
        var commandDigest = ReviewConcurrency.Digest(new { version = 1, request.OperationId, request.ExpectedRevisionId, request.ExpectedVersion, declarationDigest });
        try
        {
            return await db.InTransactionAsync(async ct =>
            {
                var submission = await db.Submissions.SingleOrDefaultAsync(s => s.TenantId == actor.TenantId && s.CreatorPartyId == actor.PartyId && s.Id == submissionId, ct) ?? throw Missing();
                if (!options.Value.Channels.TryGetValue(submission.ChannelKey, out var channel) || !channel.Enabled || channel.TenantId != actor.TenantId)
                    throw new PublishingException(404, "channel-unavailable", "The submission channel is unavailable.");
                var current = await db.Revisions.SingleAsync(r => r.TenantId == actor.TenantId && r.SubmissionId == submissionId && r.Id == submission.CurrentRevisionId, ct);
                var replay = await db.Revisions.SingleOrDefaultAsync(r => r.TenantId == actor.TenantId && r.SubmissionId == submissionId && r.RevisionOperationId == request.OperationId, ct);
                if (replay is not null)
                {
                    if (replay.RevisionCommandDigest != commandDigest) throw new PublishingException(409, "revision-operation-conflict", "This operation already names different revision arguments.");
                    return CreatorSubmissionService.Map(submission, current);
                }
                ReviewConcurrency.RequireVersion(submission, request.ExpectedVersion);
                if (current.Id != request.ExpectedRevisionId) throw new PublishingException(409, "review-revision-changed", "Replace the current revision only.");
                if (submission.Status is not (SubmissionStatuses.AwaitingUpload or SubmissionStatuses.Uploaded or SubmissionStatuses.Held
                    or SubmissionStatuses.InReview or SubmissionStatuses.ChangesRequested or SubmissionStatuses.Rejected or SubmissionStatuses.Approved))
                    throw new PublishingException(409, "revision-not-replaceable", "Wait for the current operation before replacing this revision.");
                if (declaration.ChannelKey != submission.ChannelKey || declaration.DeclaredPackageBytes > channel.MaxPackageBytes
                    || declaration.RightsDeclarationVersion != channel.RightsDeclarationVersion)
                    throw new PublishingException(422, "revision-policy-mismatch", "Use the same channel and its current package and distribution policy.");
                if (current.Number >= 100) throw new PublishingException(429, "revision-limit", "The retained revision allowance has been reached.");
                var revision = new SubmissionRevision
                {
                    TenantId = actor.TenantId, SubmissionId = submissionId, Number = current.Number + 1,
                    RequestDigest = declarationDigest, RequestDigestVersion = declaration.EditorialMaterial is null ? 1 : 2,
                    Title = declaration.Title, CreatorDisplayName = declaration.CreatorDisplayName, Summary = declaration.Summary,
                    Language = declaration.Language, WorldId = declaration.WorldId, Edition = declaration.Edition,
                    PackageFileName = declaration.PackageFileName, DeclaredPackageBytes = declaration.DeclaredPackageBytes,
                    DeclaredPackageSha256 = declaration.DeclaredPackageSha256, RightsDeclarationVersion = declaration.RightsDeclarationVersion,
                    DistributionPermissionGranted = declaration.DistributionPermissionGranted,
                    EditorialMaterialJson = declaration.EditorialMaterial is null ? null : JsonSerializer.Serialize(declaration.EditorialMaterial),
                    OriginatingUserId = actor.UserId, RevisionOperationId = request.OperationId, RevisionCommandDigest = commandDigest,
                };
                db.Revisions.Add(revision);
                submission.CurrentRevisionId = revision.Id; submission.ActiveEditorialDecisionId = null;
                submission.Status = SubmissionStatuses.AwaitingUpload;
                await db.SaveChangesAsync(ct);
                return CreatorSubmissionService.Map(submission, revision);
            }, cancellationToken);
        }
        catch (DbUpdateConcurrencyException) { throw new PublishingException(409, "review-version-changed", "The current submission changed. Reload it before replacing."); }
    }

    public async Task<CreatorDecisionPage> DecisionsAsync(Guid submissionId, int page = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var actor = await creators.RequireActorAsync(false, cancellationToken);
        EditorialReviewService.RequirePage(page, pageSize);
        if (!await db.Submissions.AnyAsync(s => s.TenantId == actor.TenantId && s.CreatorPartyId == actor.PartyId && s.Id == submissionId, cancellationToken)) throw Missing();
        var summaries = await db.EditorialDecisions.AsNoTracking().Where(d => d.TenantId == actor.TenantId && d.SubmissionId == submissionId)
            .OrderByDescending(d => d.DecidedAt).ThenByDescending(d => d.Id).Skip((page-1)*pageSize).Take(pageSize)
            .Select(d => new CreatorDecisionSummary(d.Id, d.RevisionId, d.Decision, d.CreatorMessage, d.DecidedAt)).ToListAsync(cancellationToken);
        return new(page, pageSize, summaries);
    }
    private static PublishingException Missing() => new(404, "submission-unavailable", "No such submission is available.");
}
