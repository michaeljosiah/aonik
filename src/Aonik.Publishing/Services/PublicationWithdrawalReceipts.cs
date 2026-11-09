using Microsoft.EntityFrameworkCore;

using Aonik.Publishing.Entities;
using Aonik.Publishing.Persistence;

namespace Aonik.Publishing.Services;

internal static class PublicationWithdrawalReceipts
{
    // A part withdrawal retires its base and every current part. Each receipt must
    // reflect that removal, without replacing the state of a newer revision.
    internal static async Task ApplyAsync(IPublishingDataContext db, Guid tenantId,
        IReadOnlyCollection<PublishedEdition> retired, CancellationToken cancellationToken = default)
    {
        if (retired.Any(edition => edition.TenantId != tenantId || edition.IsCurrent || edition.WithdrawnAt is null))
            throw new InvalidOperationException("Only this tenant's retired editions can update withdrawal receipts.");

        var submissionIds = retired.Where(edition => edition.SubmissionId.HasValue)
            .Select(edition => edition.SubmissionId!.Value).Distinct().ToArray();
        if (submissionIds.Length == 0) return;

        var retiredIds = retired.Select(edition => edition.Id).ToArray();
        var submissions = await db.Submissions.Where(submission => submission.TenantId == tenantId
            && submissionIds.Contains(submission.Id) && submission.Status == SubmissionStatuses.Published)
            .ToListAsync(cancellationToken);
        foreach (var submission in submissions)
        {
            if (!retired.Any(edition => edition.SubmissionId == submission.Id && edition.RevisionId == submission.CurrentRevisionId)) continue;
            // Pending tracked retirement rows still have IsCurrent=true in SQL until
            // SaveChanges. Exclude those IDs, but preserve any separate live edition.
            var stillPublished = await db.PublishedEditions.AnyAsync(edition => edition.TenantId == tenantId
                && edition.SubmissionId == submission.Id && edition.RevisionId == submission.CurrentRevisionId
                && edition.IsCurrent && !retiredIds.Contains(edition.Id), cancellationToken);
            if (!stillPublished) submission.Status = SubmissionStatuses.Withdrawn;
        }
    }
}
