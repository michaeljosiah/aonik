using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Aonik.Publishing.Contracts;
using Aonik.Publishing.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Events;
using Aonik.SharedKernel.Events.Integration;

namespace Aonik.Publishing.Services;

internal sealed class PublicationValidationHandler(IPublishingDataContext db, IPublishingObjectStore objects,
    IPackageVerifier verifier, ITenantProvider tenantProvider, IClock clock, IOptions<PublishingOptions> configuration)
    : IEventHandler<PublicationValidationRequestedEvent>
{
    public async Task HandleAsync(PublicationValidationRequestedEvent message, CancellationToken cancellationToken = default)
    {
        var tenant = tenantProvider.GetCurrentTenantId();
        if (tenant != message.TenantId || message.EventId != message.ReportId) throw new InvalidOperationException("Validation event scope does not match its report.");
        var claim = Guid.NewGuid();
        var report = await db.InTransactionAsync(async ct =>
        {
            var row = await db.ValidationReports.SingleAsync(r => r.TenantId == tenant && r.Id == message.ReportId
                && r.RevisionId == message.RevisionId && r.SubmissionId == message.SubmissionId, ct);
            if (row.CompletedAt.HasValue) return null;
            if (row.Status == "running" && row.ClaimExpiresAt > clock.UtcNow) throw new InvalidOperationException("Validation is already claimed.");
            row.Status = "running"; row.ClaimToken = claim; row.ClaimExpiresAt = clock.UtcNow.AddMinutes(5);
            await db.SaveChangesAsync(ct); return row;
        }, cancellationToken);
        if (report is null) return;
        var policy = JsonSerializer.Deserialize<PackageVerifierOptions>(report.PolicyJson) ?? new PackageVerifierOptions();
        PackageVerificationResult result;
        try
        {
            var revision = await db.Revisions.AsNoTracking().SingleAsync(r => r.TenantId == tenant && r.Id == message.RevisionId, cancellationToken);
            var session = await db.UploadSessions.AsNoTracking().SingleAsync(s => s.TenantId == tenant && s.Id == revision.CurrentUploadSessionId
                && s.RevisionId == revision.Id && s.Status == "uploaded", cancellationToken);
            if (session.StorageLocationId != objects.LocationId) throw new IOException("The owning private store changed.");
            var input = await objects.OpenPackageAsync(tenant, session.Id, report.PackageSha256, report.PackageBytes, cancellationToken);
            result = await verifier.VerifyAsync(input, policy, cancellationToken);
        }
        catch (Exception ex) when (ex is PublishingException or IOException or UnauthorizedAccessException)
        {
            result = new(false, "private-package-unavailable", policy.ReaderCommit, "", 0, "", "", "", "", null, null, null, []);
        }
        await db.InTransactionAsync(async ct =>
        {
            var current = await db.ValidationReports.SingleAsync(r => r.TenantId == tenant && r.Id == message.ReportId, ct);
            if (current.CompletedAt.HasValue || current.ClaimToken != claim || current.ClaimExpiresAt <= clock.UtcNow)
                throw new InvalidOperationException("Validation claim was replaced or expired.");
            var submission = await db.Submissions.SingleAsync(s => s.TenantId == tenant && s.Id == message.SubmissionId, ct);
            var revision = await db.Revisions.SingleAsync(r => r.TenantId == tenant && r.Id == message.RevisionId, ct);
            var code = result.Passed ? "verified" : result.Code;
            if (result.Passed && (result.Code != "verified" || result.ReaderCommit != current.ReaderCommit
                || result.Kind is not ("world" or "guide" or "part" or "production" or "story" or "comic")))
                code = "verifier-protocol-invalid";
            if (result.Passed && (result.ArchiveSha256 != current.PackageSha256 || result.ArchiveBytes != current.PackageBytes
                || result.WorldId != revision.WorldId || result.Edition != revision.Edition)) code = "package-declaration-mismatch";
            var channel = configuration.Value.Channels.GetValueOrDefault(submission.ChannelKey);
            if (channel is null || !channel.Enabled || !channel.NamespaceAdmissionEnabled || channel.TenantId != tenant || channel.RightsDeclarationVersion != revision.RightsDeclarationVersion
                || current.PackageBytes > channel.MaxPackageBytes
                || JsonSerializer.Serialize(configuration.Value.Verifier) != current.PolicyJson)
                code = "validation-policy-changed";
            var space = await db.Namespaces.SingleOrDefaultAsync(n => n.TenantId == tenant && n.ChannelKey == submission.ChannelKey && n.WorldId == revision.WorldId, ct);
            if (space is null || space.CreatorPartyId != submission.CreatorPartyId) code = "namespace-authority-unavailable";
            if (result.Passed && result.Kind == "part" && (space?.ApprovedBaseFingerprint is null
                || result.BaseFingerprint != space.ApprovedBaseFingerprint || result.PartNumber != space.LastApprovedPartNumber + 1))
                code = "part-lineage-unavailable";
            var active = revision.CurrentValidationReportId == current.Id && submission.CurrentRevisionId == revision.Id;
            var encoded = JsonSerializer.Serialize(result);
            if (encoded.Length > 262144)
            {
                code = "validation-report-too-large";
                encoded = JsonSerializer.Serialize(result with { Passed = false, Code = code, Assets = [] });
            }
            current.Status = active && result.Passed && code == "verified" ? "passed" : "held";
            current.Code = active ? code : "revision-superseded";
            current.ResultJson = encoded;
            current.CompletedAt = clock.UtcNow; current.ClaimToken = null; current.ClaimExpiresAt = null;
            if (active)
            {
                revision.PackageVerifiedAt = current.Status == "passed" ? clock.UtcNow : null;
                submission.Status = current.Status == "passed" ? Entities.SubmissionStatuses.InReview : Entities.SubmissionStatuses.Held;
            }
            await db.SaveChangesAsync(ct); return true;
        }, cancellationToken);
    }
}
