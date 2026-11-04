using System.Security.Cryptography;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

using Aonik.Publishing.Entities;

namespace Aonik.Publishing.Persistence;

public static class PublishingPersistenceGuard
{
    public static void Validate(ChangeTracker tracker, bool relational)
    {
        foreach (var entry in tracker.Entries())
        {
            if (entry.State is not (EntityState.Modified or EntityState.Deleted or EntityState.Added)) continue;
            if (entry.Entity is EditorialDecision or EditorialReviewSnapshot or EditorialPackageAccess or PublicationCatalogueSnapshot)
            {
                if (entry.State != EntityState.Added) throw new InvalidOperationException("Editorial history is append-only.");
            }
            if (entry.Entity is PublicationValidationReport && entry.State != EntityState.Added
                && entry.OriginalValues.GetValue<DateTime?>(nameof(PublicationValidationReport.CompletedAt)).HasValue)
                throw new InvalidOperationException("Completed validation evidence is immutable.");
            if (entry.Entity is SubmissionRevision && entry.State == EntityState.Deleted)
                throw new InvalidOperationException("Submission revisions are retained audit records.");
            if (entry.Entity is SubmissionRevision && entry.State == EntityState.Modified)
            {
                string[] operational = [nameof(SubmissionRevision.CurrentUploadSessionId), nameof(SubmissionRevision.CurrentValidationReportId),
                    nameof(SubmissionRevision.UploadedAt), nameof(SubmissionRevision.PackageVerifiedAt), "UpdatedAt", "UpdatedBy", "RowVersion"];
                if (entry.Properties.Any(p => p.IsModified && !operational.Contains(p.Metadata.Name, StringComparer.Ordinal)))
                    throw new InvalidOperationException("Changed declarations require a new submission revision.");
            }
            // InMemory has no native SQL rowversion generator. Preserve the same API concurrency boundary in tests.
            if (entry.Entity is PublicationChannelState && entry.State == EntityState.Modified
                && entry.CurrentValues.GetValue<long>(nameof(PublicationChannelState.Serial)) < entry.OriginalValues.GetValue<long>(nameof(PublicationChannelState.Serial)))
                throw new InvalidOperationException("Catalogue serials cannot decrease.");
            if (entry.Entity is PublicationChannelState && entry.State == EntityState.Modified && entry.Properties.Any(p => p.IsModified && p.Metadata.Name is "BaselineSha256" or "PublicBaseUrl" or "ChannelKey" or "TenantId"))
                throw new InvalidOperationException("Qualified channel ownership, baseline and permanent origin cannot be rewritten.");
            if (entry.Entity is PublicationOperation && entry.State != EntityState.Added)
            {
                string[] progress = ["Status", "Code", "ClaimToken", "ClaimExpiresAt", "Attempts", "RetryCycles", "CompletedAt", "CommittedSerial", "CommittedSnapshotId", "EditionId", "UpdatedAt", "UpdatedBy", "RowVersion"];
                if (entry.State == EntityState.Deleted || entry.OriginalValues.GetValue<string>("Status") == "committed" || entry.Properties.Any(p => p.IsModified && !progress.Contains(p.Metadata.Name, StringComparer.Ordinal)))
                    throw new InvalidOperationException("Publication command intent and committed outcomes are immutable.");
            }
            if (entry.Entity is PublishedEdition && entry.State != EntityState.Added
                && (entry.State == EntityState.Deleted || entry.Properties.Any(p => p.IsModified && p.Metadata.Name is not ("IsCurrent" or "WithdrawnAt" or "UpdatedAt" or "UpdatedBy" or "RowVersion"))))
                throw new InvalidOperationException("Published provenance is immutable.");
            if (entry.Entity is PublicationArtifact && entry.State != EntityState.Added
                && (entry.State == EntityState.Deleted || entry.Properties.Any(p => p.IsModified && p.Metadata.Name is not ("ReadyAt" or "PublicVerifiedAt" or "UpdatedAt" or "UpdatedBy" or "RowVersion"))))
                throw new InvalidOperationException("Public asset identity is immutable.");
            if (!relational && entry.Entity is Aonik.SharedKernel.Primitives.AuditableEntity versioned
                && entry.Entity is PublicationSubmission or PublicationOperation or PublicationChannelState or PublishedEdition
                && entry.State is EntityState.Added or EntityState.Modified)
                versioned.RowVersion = RandomNumberGenerator.GetBytes(8);
        }
    }
}
