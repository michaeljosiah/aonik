using Aonik.Documents.Persistence;
using Aonik.Platform.Entities.Operations;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aonik.Worker.Jobs;

/// <summary>
/// Opt-in catch-up job (Spec 035 Phase 4) that re-publishes <see cref="DocumentUploadedEvent"/> for
/// indexable documents that never completed ingestion — e.g. a file whose original event was
/// dead-lettered. It re-uses the normal pipeline (the events flow through the outbox to the
/// ingestion handler), which is idempotent (deterministic chunk ids + find-or-create ingestion), so
/// the backfill is self-limiting: a document indexed between runs is skipped on the next pass.
/// <para>
/// Deliberately conservative — it only touches documents already classified indexable
/// (<see cref="DocumentIndexStatus.Pending"/>); it does NOT reclassify legacy
/// <c>Internal</c>/<c>NotIndexable</c> evidence, which is a separate, opt-in data-policy migration.
/// <strong>Disabled by default</strong> (see <c>DocumentIngestionBackfillJobOptions.Enabled</c>); an
/// operator turns it on to drain, then off.
/// </para>
/// </summary>
[DisallowConcurrentExecution]
internal sealed class DocumentIngestionBackfillJob : IJob
{
    public static readonly JobKey Key = new("DocumentIngestionBackfillJob", ScheduledJobGroups.ScheduledJobs);

    private const string SucceededIngestionStatus = "Succeeded";

    private readonly DocumentsDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ScheduledJobOptions _jobOptions;
    private readonly ILogger<DocumentIngestionBackfillJob> _logger;

    public DocumentIngestionBackfillJob(
        DocumentsDbContext dbContext,
        ITenantContext tenantContext,
        IOptions<ScheduledJobOptions> jobOptions,
        ILogger<DocumentIngestionBackfillJob> logger)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _jobOptions = jobOptions.Value;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var published = await RunAsync(context.CancellationToken);
        context.Result = $"Re-published {published} document(s) for ingestion backfill.";
    }

    /// <summary>Testable core: returns the number of documents re-published for ingestion.</summary>
    internal async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var batchSize = Math.Max(_jobOptions.DocumentIngestionBackfill.BatchSize, 1);

        // Read across tenants. The tenant query filter is fail-CLOSED (a null tenant sees only
        // global rows), so a cross-tenant scan must opt out via AcrossTenants — which also disables
        // soft-delete, hence the explicit !IsDeleted guards below so erased evidence is never
        // re-indexed. Per-file: the parent document is still Pending and the file has no successful
        // ingestion yet. (Ingestion is keyed per DocumentFile, so eligibility is per file.)
        var candidates = await (
            from file in _dbContext.DocumentFiles.AcrossTenants().AsNoTracking()
            join document in _dbContext.Documents.AcrossTenants().AsNoTracking()
                on file.DocumentId equals document.Id
            where !document.IsDeleted
                && !file.IsDeleted
                && document.IndexStatus == DocumentIndexStatus.Pending
                && !_dbContext.DocumentIngestions.AcrossTenants()
                    .Any(i => i.DocumentFileId == file.Id && i.Status == SucceededIngestionStatus)
            orderby file.CreatedAt
            select new
            {
                file.TenantId,
                DocumentId = document.Id,
                FileId = file.Id,
                document.OwnerPartyId,
                document.Classification,
                file.ContentType,
            })
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return 0;
        }

        var published = 0;
        foreach (var candidate in candidates)
        {
            // Stamp the originating tenant so the outbox row (and the handler that consumes it) runs
            // under the document's tenant; commit per document so each is picked up independently.
            _tenantContext.TenantId = candidate.TenantId;
            _tenantContext.ResolutionSource = "backfill";
            try
            {
                _dbContext.EnqueueIntegrationEvent(new DocumentUploadedEvent(
                    candidate.TenantId,
                    candidate.DocumentId,
                    candidate.FileId,
                    candidate.OwnerPartyId,
                    candidate.Classification,
                    candidate.ContentType));
                await _dbContext.SaveChangesAsync(cancellationToken);
                published++;
            }
            finally
            {
                _tenantContext.TenantId = null;
            }
        }

        _logger.LogInformation(
            "Document ingestion backfill re-published {Published} of {Candidates} candidate document file(s).",
            published, candidates.Count);

        return published;
    }
}
