using Microsoft.EntityFrameworkCore;

using Aonik.Publishing.Entities;
using Aonik.SharedKernel.Events;

namespace Aonik.Publishing.Persistence;

public interface IPublishingDataContext
{
    DbSet<PublicationSubmission> Submissions { get; }
    DbSet<SubmissionRevision> Revisions { get; }
    DbSet<PublicationUploadSession> UploadSessions { get; }
    DbSet<PublicationUploadPart> UploadParts { get; }
    DbSet<PublicationUploadQuota> UploadQuotas { get; }
    DbSet<PublicationValidationReport> ValidationReports { get; }
    DbSet<PublicationNamespace> Namespaces { get; }
    DbSet<EditorialReviewSnapshot> ReviewSnapshots { get; }
    DbSet<EditorialDecision> EditorialDecisions { get; }
    DbSet<EditorialPackageAccess> EditorialPackageAccesses { get; }
    DbSet<PublicationOperation> PublicationOperations { get; }
    DbSet<PublicationChannelState> PublicationChannels { get; }
    DbSet<PublicationCatalogueSnapshot> CatalogueSnapshots { get; }
    DbSet<PublishedEdition> PublishedEditions { get; }
    DbSet<PublicationArtifact> PublicationArtifacts { get; }
    void EnqueueIntegrationEvent(IIntegrationEvent integrationEvent);
    Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
