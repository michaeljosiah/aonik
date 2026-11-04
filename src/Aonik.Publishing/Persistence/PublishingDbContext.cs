using Microsoft.EntityFrameworkCore;

using Aonik.Publishing.Entities;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Persistence;

namespace Aonik.Publishing.Persistence;

internal sealed class PublishingDbContext(
    DbContextOptions<PublishingDbContext> options,
    ITenantProvider? tenantProvider = null,
    ICurrentUserProvider? currentUserProvider = null,
    IClock? clock = null) : AonikDbContextBase(options, tenantProvider, currentUserProvider, clock), IPublishingDataContext
{
    public DbSet<PublicationSubmission> Submissions => Set<PublicationSubmission>();
    public DbSet<SubmissionRevision> Revisions => Set<SubmissionRevision>();
    public DbSet<PublicationUploadSession> UploadSessions => Set<PublicationUploadSession>();
    public DbSet<PublicationUploadPart> UploadParts => Set<PublicationUploadPart>();
    public DbSet<PublicationUploadQuota> UploadQuotas => Set<PublicationUploadQuota>();
    public DbSet<PublicationValidationReport> ValidationReports => Set<PublicationValidationReport>();
    public DbSet<PublicationNamespace> Namespaces => Set<PublicationNamespace>();
    public DbSet<EditorialReviewSnapshot> ReviewSnapshots => Set<EditorialReviewSnapshot>();
    public DbSet<EditorialDecision> EditorialDecisions => Set<EditorialDecision>();
    public DbSet<EditorialPackageAccess> EditorialPackageAccesses => Set<EditorialPackageAccess>();
    public DbSet<PublicationOperation> PublicationOperations => Set<PublicationOperation>();
    public DbSet<PublicationChannelState> PublicationChannels => Set<PublicationChannelState>();
    public DbSet<PublicationCatalogueSnapshot> CatalogueSnapshots => Set<PublicationCatalogueSnapshot>();
    public DbSet<PublishedEdition> PublishedEditions => Set<PublishedEdition>();
    public DbSet<PublicationArtifact> PublicationArtifacts => Set<PublicationArtifact>();

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        PublishingPersistenceGuard.Validate(ChangeTracker, Database.IsRelational());
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PublishingPersistenceGuard.Validate(ChangeTracker, Database.IsRelational());
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public async Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
    {
        if (!Database.IsRelational())
        {
            ChangeTracker.Clear();
            try { return await action(cancellationToken); }
            catch { ChangeTracker.Clear(); throw; }
        }
        return await Database.CreateExecutionStrategy().ExecuteAsync(async ct =>
        {
            // A retry must reload authoritative counters/heads, not reuse the failed attempt's tracked state.
            ChangeTracker.Clear();
            await using var transaction = await Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var result = await action(ct);
            await transaction.CommitAsync(ct);
            return result;
        }, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(SchemaNames.Default);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PublishingDbContext).Assembly);
        ConfigureRowVersions(modelBuilder);
        ApplyTenantQueryFilters(modelBuilder);
    }
}
