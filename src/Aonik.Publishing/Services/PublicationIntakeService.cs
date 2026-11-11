using System.Text.Json;
using System.Text.RegularExpressions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Aonik.Publishing.Contracts;
using Aonik.Publishing.Entities;
using Aonik.Publishing.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Events.Integration;

namespace Aonik.Publishing.Services;

internal sealed class PublicationIntakeService(IPublishingDataContext db, CreatorSubmissionService creators,
    IPublishingObjectStore objects, IOptions<PublishingOptions> configuration, IClock clock) : IPublicationIntakeService
{
    private PublishingOptions Options => configuration.Value;

    public async Task<UploadReceipt> BeginAsync(Guid submissionId, CancellationToken cancellationToken = default)
    {
        var actor = await creators.RequireActorAsync(true, cancellationToken);
        RequireLimits();
        var location = objects.LocationId;
        return await db.InTransactionAsync(async ct =>
        {
            var (submission, revision) = await OwnAsync(submissionId, actor.TenantId, actor.PartyId, ct);
            RequireAdmission(submission, revision);
            if (revision.CurrentUploadSessionId is { } previousId)
            {
                var previous = await SessionAsync(submission, revision, previousId, ct);
                RequireStorage(previous);
                if (previous.Status == "uploaded" || (previous.ExpiresAt > clock.UtcNow && previous.Status is "open" or "assembling"))
                    return await DescribeAsync(previous, ct);
                if (previous.Status is "open" or "assembling") UploadReservations.Retire(previous, "expired", clock.UtcNow);
            }
            var claim = await db.Namespaces.SingleOrDefaultAsync(n => n.TenantId == actor.TenantId
                && n.ChannelKey == submission.ChannelKey && n.WorldId == revision.WorldId, ct);
            if (claim is not null && claim.CreatorPartyId != actor.PartyId)
                throw Problem(409, "world-namespace-unavailable", "This public world identifier is not available to this creator.");
            if (claim is null && await db.Namespaces.CountAsync(n => n.TenantId == actor.TenantId && n.CreatorPartyId == actor.PartyId, ct) >= Options.Intake.MaxCreatorNamespaces)
                throw Problem(429, "namespace-limit", "The creator namespace allowance is exhausted.");
            if (await db.UploadSessions.CountAsync(s => s.TenantId == actor.TenantId && s.CreatorPartyId == actor.PartyId && s.CreatedAt >= clock.UtcNow.Date, ct) >= Options.Intake.MaxSessionsPerCreatorPerDay)
                throw Problem(429, "session-admission-limit", "The daily upload session allowance is exhausted.");
            if (claim is null) db.Namespaces.Add(new PublicationNamespace
            {
                TenantId = actor.TenantId, ChannelKey = submission.ChannelKey, WorldId = revision.WorldId, CreatorPartyId = actor.PartyId,
            });
            await UploadReservations.ReserveAsync(db, actor.TenantId, actor.PartyId, revision.DeclaredPackageBytes, Options.Intake, ct);
            var partSize = revision.DeclaredPackageBytes <= Options.Intake.SingleShotThresholdBytes
                ? checked((int)revision.DeclaredPackageBytes) : Options.Intake.PartSizeBytes;
            var total = (revision.DeclaredPackageBytes - 1) / partSize + 1;
            if (total > 4096) throw Problem(413, "too-many-parts", "The package needs too many upload parts.");
            var session = new PublicationUploadSession
            {
                TenantId = actor.TenantId, CreatorPartyId = actor.PartyId, SubmissionId = submission.Id, RevisionId = revision.Id,
                DeclaredBytes = revision.DeclaredPackageBytes, DeclaredSha256 = revision.DeclaredPackageSha256,
                StorageLocationId = location,
                PartSizeBytes = partSize, TotalParts = (int)total, ExpiresAt = clock.UtcNow.AddHours(Options.Intake.SessionHours),
            };
            db.UploadSessions.Add(session);
            revision.CurrentUploadSessionId = session.Id;
            submission.Status = SubmissionStatuses.AwaitingUpload;
            await db.SaveChangesAsync(ct);
            return await DescribeAsync(session, ct);
        }, cancellationToken);
    }

    public async Task<UploadReceipt> GetAsync(Guid submissionId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var actor = await creators.RequireActorAsync(false, cancellationToken);
        var (submission, revision) = await OwnAsync(submissionId, actor.TenantId, actor.PartyId, cancellationToken);
        return await DescribeAsync(await SessionAsync(submission, revision, sessionId, cancellationToken), cancellationToken);
    }

    public async Task<UploadReceipt> PutPartAsync(Guid submissionId, Guid sessionId, int number, string sha256, long bytes, Stream content, CancellationToken cancellationToken = default)
    {
        var actor = await creators.RequireActorAsync(true, cancellationToken);
        sha256 = sha256.Trim().ToLowerInvariant();
        if (!Regex.IsMatch(sha256, "^[a-f0-9]{64}$", RegexOptions.CultureInvariant))
            throw Problem(422, "invalid-part-digest", "A SHA-256 digest is required for each part.");
        // Claim the immutable part identity BEFORE writing bytes. Competing changed retries cannot
        // accumulate unreserved objects under a session while racing the database insert.
        await db.InTransactionAsync(async ct =>
        {
            var (submission, revision) = await OwnAsync(submissionId, actor.TenantId, actor.PartyId, ct);
            RequireAdmission(submission, revision);
            RequireCurrent(revision, sessionId);
            var session = await SessionAsync(submission, revision, sessionId, ct);
            RequireStorage(session);
            RequireOpen(session);
            if (number < 1 || number > session.TotalParts || bytes != ExpectedPartBytes(session, number))
                throw Problem(422, "invalid-part-length", "Part number and length must match the upload receipt.");
            var part = await db.UploadParts.SingleOrDefaultAsync(p => p.TenantId == actor.TenantId && p.SessionId == sessionId && p.Number == number, ct);
            if (part is not null && (part.Sha256 != sha256 || part.Bytes != bytes))
                throw Problem(409, "part-identity-conflict", "This part number already names different bytes.");
            if (part is null)
            {
                db.UploadParts.Add(new PublicationUploadPart
                {
                    TenantId = actor.TenantId, SessionId = sessionId, Number = number, Bytes = bytes, Sha256 = sha256,
                });
                await db.SaveChangesAsync(ct);
            }
            return true;
        }, cancellationToken);
        await objects.StorePartAsync(actor.TenantId, sessionId, number, sha256, bytes, content, cancellationToken);
        return await db.InTransactionAsync(async ct =>
        {
            var (submission, revision) = await OwnAsync(submissionId, actor.TenantId, actor.PartyId, ct);
            RequireCurrent(revision, sessionId);
            var session = await SessionAsync(submission, revision, sessionId, ct);
            RequireAdmission(submission, revision);
            RequireStorage(session);
            RequireOpen(session);
            var part = await db.UploadParts.SingleAsync(p => p.TenantId == actor.TenantId && p.SessionId == sessionId && p.Number == number, ct);
            if (part.Sha256 != sha256) throw Problem(409, "part-identity-conflict", "Part identity changed.");
            if (!part.Received)
            {
                part.Received = true;
                session.ReceivedBytes = checked(session.ReceivedBytes + part.Bytes);
                await db.SaveChangesAsync(ct);
            }
            return await DescribeAsync(session, ct);
        }, cancellationToken);
    }

    public async Task<UploadReceipt> CompleteAsync(Guid submissionId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var actor = await creators.RequireActorAsync(true, cancellationToken);
        var token = Guid.NewGuid();
        var session = await db.InTransactionAsync(async ct =>
        {
            var (submission, revision) = await OwnAsync(submissionId, actor.TenantId, actor.PartyId, ct);
            RequireAdmission(submission, revision);
            RequireCurrent(revision, sessionId);
            var current = await SessionAsync(submission, revision, sessionId, ct);
            RequireStorage(current);
            if (current.Status == "uploaded") return current;
            if (current.ExpiresAt <= clock.UtcNow) throw Problem(410, "upload-expired", "The upload session expired.");
            if (current.Status == "assembling" && current.AssemblyExpiresAt > clock.UtcNow)
                throw Problem(409, "assembly-in-progress", "Package assembly is already running. Retry after its lease expires.");
            if (current.Status is not ("open" or "assembling")) throw Problem(409, "upload-not-open", "The upload is no longer open.");
            var parts = await db.UploadParts.Where(p => p.TenantId == actor.TenantId && p.SessionId == sessionId && p.Received).ToListAsync(ct);
            if (parts.Count != current.TotalParts || parts.Sum(p => p.Bytes) != current.DeclaredBytes)
                throw Problem(409, "parts-missing", "Every declared part must be received before completion.");
            current.Status = "assembling"; current.AssemblyToken = token;
            current.AssemblyExpiresAt = clock.UtcNow.AddMinutes(Options.Intake.AssemblyMinutes);
            await db.SaveChangesAsync(ct);
            return current;
        }, cancellationToken);
        if (session.Status == "uploaded") return await DescribeAsync(session, cancellationToken);
        var heldParts = await db.UploadParts.AsNoTracking().Where(p => p.TenantId == actor.TenantId && p.SessionId == sessionId)
            .OrderBy(p => p.Number).ToListAsync(cancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(Options.Intake.AssemblyMinutes));
        try
        {
            await objects.AssembleAsync(actor.TenantId, sessionId, session.DeclaredSha256, session.DeclaredBytes,
                heldParts.Select(p => (p.Number, p.Sha256)).ToArray(), timeout.Token);
        }
        catch (PublishingException ex) when (ex.Code == "upload-bytes-mismatch")
        {
            await RetireFailedAssemblyAsync(actor.TenantId, sessionId, token, cancellationToken);
            throw;
        }
        return await db.InTransactionAsync(async ct =>
        {
            var (submission, revision) = await OwnAsync(submissionId, actor.TenantId, actor.PartyId, ct);
            RequireCurrent(revision, sessionId);
            var current = await SessionAsync(submission, revision, sessionId, ct);
            RequireStorage(current);
            if (current.Status != "assembling" || current.AssemblyToken != token || current.AssemblyExpiresAt <= clock.UtcNow || current.ExpiresAt <= clock.UtcNow)
                throw Problem(409, "assembly-claim-lost", "Assembly authority expired; reconcile the upload before retrying.");
            current.Status = "uploaded"; current.AssemblyToken = null; current.AssemblyExpiresAt = null;
            revision.UploadedAt = clock.UtcNow;
            submission.Status = SubmissionStatuses.Uploaded;
            await db.SaveChangesAsync(ct);
            return await DescribeAsync(current, ct);
        }, cancellationToken);
    }

    public async Task CancelAsync(Guid submissionId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var actor = await creators.RequireActorAsync(true, cancellationToken);
        await db.InTransactionAsync(async ct =>
        {
            var (submission, revision) = await OwnAsync(submissionId, actor.TenantId, actor.PartyId, ct);
            var session = await SessionAsync(submission, revision, sessionId, ct);
            if (revision.CurrentUploadSessionId != sessionId)
            {
                if (session.RetiredAt.HasValue) return true; // A lost cancel response must not cancel its replacement.
                throw Problem(409, "upload-superseded", "The revision now belongs to another upload session.");
            }
            if (revision.CurrentValidationReportId.HasValue)
                throw Problem(409, "submitted-package-retained", "Submitted packages remain retained for validation and review.");
            UploadReservations.Retire(session, "cancelled", clock.UtcNow);
            session.RetiredByUserId = actor.UserId;
            revision.CurrentUploadSessionId = null; revision.UploadedAt = null;
            submission.Status = SubmissionStatuses.AwaitingUpload;
            await db.SaveChangesAsync(ct); return true;
        }, cancellationToken);
    }

    public async Task<ValidationPreview> SubmitAsync(Guid submissionId, SubmitPackageRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await creators.RequireActorAsync(true, cancellationToken);
        if (request.OperationId == Guid.Empty) throw Problem(422, "invalid-operation", "A stable submission operation ID is required.");
        return await db.InTransactionAsync(async ct =>
        {
            var (submission, revision) = await OwnAsync(submissionId, actor.TenantId, actor.PartyId, ct);
            RequireAdmission(submission, revision);
            if (request.RevisionId != revision.Id) throw Problem(409, "revision-changed", "Submit the current immutable revision.");
            var old = await db.ValidationReports.SingleOrDefaultAsync(r => r.TenantId == actor.TenantId && r.RevisionId == revision.Id && r.OperationId == request.OperationId, ct);
            if (old is not null) return Preview(old);
            if (revision.CurrentValidationReportId is { } currentId)
            {
                var current = await db.ValidationReports.SingleAsync(r => r.TenantId == actor.TenantId && r.Id == currentId, ct);
                if (current.Status is "pending" or "running" or "passed") return Preview(current);
            }
            if (revision.CurrentUploadSessionId is not { } sessionId || revision.UploadedAt is null)
                throw Problem(409, "package-not-uploaded", "A complete private package is required before submission.");
            var session = await SessionAsync(submission, revision, sessionId, ct);
            RequireStorage(session);
            if (session.Status != "uploaded") throw Problem(409, "package-not-uploaded", "The package is not complete.");
            if (await db.ValidationReports.CountAsync(r => r.TenantId == actor.TenantId && r.RevisionId == revision.Id, ct) >= Options.Intake.MaxValidationAttempts)
                throw Problem(429, "validation-attempt-limit", "The validation retry limit for this immutable revision was reached.");
            var report = new PublicationValidationReport
            {
                TenantId = actor.TenantId, SubmissionId = submission.Id, RevisionId = revision.Id, OperationId = request.OperationId,
                PackageSha256 = session.DeclaredSha256, PackageBytes = session.DeclaredBytes,
                PolicyJson = JsonSerializer.Serialize(Options.Verifier), ReaderCommit = Options.Verifier.ReaderCommit,
                VerifierImage = Options.Verifier.Image,
            };
            db.ValidationReports.Add(report);
            revision.CurrentValidationReportId = report.Id;
            submission.Status = SubmissionStatuses.Validating;
            db.EnqueueIntegrationEvent(new PublicationValidationRequestedEvent(report.Id, clock.UtcNow, actor.TenantId, submission.Id, revision.Id, report.Id));
            await db.SaveChangesAsync(ct);
            return Preview(report);
        }, cancellationToken);
    }

    public async Task<ValidationPreview?> PreviewAsync(Guid submissionId, CancellationToken cancellationToken = default)
    {
        var actor = await creators.RequireActorAsync(false, cancellationToken);
        var (_, revision) = await OwnAsync(submissionId, actor.TenantId, actor.PartyId, cancellationToken);
        if (revision.CurrentValidationReportId is not { } reportId) return null;
        return Preview(await db.ValidationReports.AsNoTracking().SingleAsync(r => r.TenantId == actor.TenantId && r.Id == reportId, cancellationToken));
    }

    public async Task<(Stream Content, string FileName)> DownloadAsync(Guid submissionId, CancellationToken cancellationToken = default)
    {
        var actor = await creators.RequireActorAsync(false, cancellationToken);
        var (submission, revision) = await OwnAsync(submissionId, actor.TenantId, actor.PartyId, cancellationToken);
        if (revision.CurrentUploadSessionId is not { } sessionId || revision.UploadedAt is null)
            throw Problem(404, "package-unavailable", "No complete package is available.");
        var session = await SessionAsync(submission, revision, sessionId, cancellationToken);
        RequireStorage(session);
        if (session.Status != "uploaded") throw Problem(404, "package-unavailable", "No complete package is available.");
        var input = await objects.OpenPackageAsync(actor.TenantId, sessionId, session.DeclaredSha256, session.DeclaredBytes, cancellationToken);
        return (new FileStream(input.Path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous), revision.PackageFileName);
    }

    private async Task<(PublicationSubmission Submission, SubmissionRevision Revision)> OwnAsync(Guid id, Guid tenant, Guid party, CancellationToken ct)
    {
        var submission = await db.Submissions.SingleOrDefaultAsync(s => s.TenantId == tenant && s.CreatorPartyId == party && s.Id == id, ct)
            ?? throw Problem(404, "submission-unavailable", "No such submission is available.");
        var revision = await db.Revisions.SingleAsync(r => r.TenantId == tenant && r.SubmissionId == id && r.Id == submission.CurrentRevisionId, ct);
        return (submission, revision);
    }
    private async Task<PublicationUploadSession> SessionAsync(PublicationSubmission submission, SubmissionRevision revision, Guid id, CancellationToken ct)
        => await db.UploadSessions.SingleOrDefaultAsync(s => s.TenantId == submission.TenantId && s.CreatorPartyId == submission.CreatorPartyId
            && s.SubmissionId == submission.Id && s.RevisionId == revision.Id && s.Id == id, ct)
            ?? throw Problem(404, "upload-unavailable", "No such upload is available.");

    private void RequireAdmission(PublicationSubmission submission, SubmissionRevision revision)
    {
        if (!Options.Channels.TryGetValue(submission.ChannelKey, out var channel) || !channel.Enabled || channel.TenantId != submission.TenantId)
            throw Problem(404, "channel-unavailable", "The submission channel is not available.");
        if (!channel.NamespaceAdmissionEnabled) throw Problem(503, "namespace-not-configured", "Public namespace admission must be qualified before uploading.");
        if (!PackageCapacity.IsKnown(Options.Verifier.CapacityProfile)
            || ((Options.Verifier.Provider == "AzureDynamicSessions" || Options.Verifier.CapacityProfile is not null)
                && channel.MaxPackageBytes > PackageCapacity.Resolve(Options.Verifier.CapacityProfile).PackageBytes))
            throw Problem(503, "intake-not-configured", "The channel exceeds its verifier capacity profile.");
        if (revision.DeclaredPackageBytes > channel.MaxPackageBytes || revision.RightsDeclarationVersion != channel.RightsDeclarationVersion)
            throw Problem(409, "channel-policy-changed", "The channel's size or distribution permission policy changed.");
    }
    private void RequireLimits()
    {
        var p = Options.Intake;
        if (string.IsNullOrWhiteSpace(p.PrivateRoot) || p.MaxCreatorRetainedBytes <= 0 || p.MaxTenantRetainedBytes <= 0
            || p.MaxCreatorPackages < 1 || p.MaxTenantPackages < 1 || p.PartSizeBytes is < 1 or > 33554432
            || p.SingleShotThresholdBytes is < 1 or > 33554432 || p.SessionHours is < 1 or > 168
            || p.AssemblyMinutes is < 1 or > 60 || p.MaxValidationAttempts is < 1 or > 10 || p.MaxCreatorNamespaces < 1 || p.MaxSessionsPerCreatorPerDay < 1)
            throw Problem(503, "intake-not-configured", "Private storage and bounded upload quotas must be configured.");
    }
    private void RequireOpen(PublicationUploadSession session)
    {
        if (session.ExpiresAt <= clock.UtcNow) throw Problem(410, "upload-expired", "The upload session expired.");
        if (session.Status != "open") throw Problem(409, "upload-not-open", "The upload is no longer open.");
    }
    private void RequireStorage(PublicationUploadSession session)
    {
        if (session.StorageLocationId != objects.LocationId)
            throw Problem(503, "storage-location-changed", "Restore or migrate the owning private store before resuming this upload.");
    }
    private static void RequireCurrent(SubmissionRevision revision, Guid sessionId)
    {
        if (revision.CurrentUploadSessionId != sessionId)
            throw Problem(409, "upload-superseded", "The revision now belongs to another upload session.");
    }
    private async Task<UploadReceipt> DescribeAsync(PublicationUploadSession s, CancellationToken ct)
    {
        var received = await db.UploadParts.AsNoTracking().Where(p => p.TenantId == s.TenantId && p.SessionId == s.Id && p.Received).Select(p => p.Number).ToListAsync(ct);
        return new UploadReceipt(s.Id, s.RevisionId, s.Status == "open" && s.ExpiresAt <= clock.UtcNow ? "expired" : s.Status,
            s.PartSizeBytes, s.TotalParts, s.ReceivedBytes, s.ExpiresAt,
            Enumerable.Range(1, s.TotalParts).Except(received).ToArray());
    }
    private async Task RetireFailedAssemblyAsync(Guid tenant, Guid sessionId, Guid token, CancellationToken ct)
    {
        await db.InTransactionAsync(async tokenCt =>
        {
            var session = await db.UploadSessions.SingleAsync(s => s.TenantId == tenant && s.Id == sessionId, tokenCt);
            if (session.Status == "assembling" && session.AssemblyToken == token)
            {
                UploadReservations.Retire(session, "failed", clock.UtcNow);
                var revision = await db.Revisions.SingleAsync(r => r.TenantId == tenant && r.Id == session.RevisionId, tokenCt);
                if (revision.CurrentUploadSessionId == sessionId) revision.CurrentUploadSessionId = null;
                await db.SaveChangesAsync(tokenCt);
            }
            return true;
        }, ct);
    }
    internal static ValidationPreview Preview(PublicationValidationReport r) => new(r.Id, r.Status, r.Code, r.PackageSha256,
        r.PackageBytes, r.ReaderCommit, r.VerifierImage, r.CompletedAt,
        string.IsNullOrEmpty(r.ResultJson) ? null : JsonSerializer.Deserialize<PackageVerificationResult>(r.ResultJson));
    private static long ExpectedPartBytes(PublicationUploadSession s, int number) => Math.Min(s.PartSizeBytes, s.DeclaredBytes - (long)(number - 1) * s.PartSizeBytes);
    private static PublishingException Problem(int status, string code, string message) => new(status, code, message);
}
