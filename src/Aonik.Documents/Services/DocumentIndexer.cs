using System.Text.Json;

using Aonik.Documents.Entities;
using Aonik.Documents.Persistence;
using Aonik.Platform.Entities.Compliance; // Document/DocumentFile — namespace preserved (Spec 035)
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Events.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aonik.Documents.Services;

/// <summary>
/// Default <see cref="IDocumentIndexer"/>. Depends only on SharedKernel contracts (blob store,
/// text extractor, scoped vector index, AI-run writer) plus the module's own
/// <see cref="DocumentsDbContext"/>, so <c>Aonik.Documents</c> stays free of any Infrastructure
/// reference — the implementations are injected at the composition root. Runs inside the Worker's
/// outbox dispatch scope, where the tenant has already been restored from the event.
/// </summary>
internal sealed class DocumentIndexer : IDocumentIndexer
{
    private const string IngestionUseCase = "document_ingestion";
    private const string VectorCollectionName = "documents";
    private const int MaxLastErrorLength = 2000;

    private const string StatusRunning = "Running";
    private const string StatusSucceeded = "Succeeded";
    private const string StatusSkipped = "Skipped";
    private const string StatusFailed = "Failed";

    private readonly DocumentsDbContext _dbContext;
    private readonly IDocumentFileStore _fileStore;
    private readonly IDocumentTextExtractor _textExtractor;
    private readonly IDocumentVectorIndex _vectorIndex;
    private readonly IAiRunWriter _aiRunWriter;
    private readonly IClock _clock;
    private readonly ILogger<DocumentIndexer> _logger;

    public DocumentIndexer(
        DocumentsDbContext dbContext,
        IDocumentFileStore fileStore,
        IDocumentTextExtractor textExtractor,
        IDocumentVectorIndex vectorIndex,
        IAiRunWriter aiRunWriter,
        IClock clock,
        ILogger<DocumentIndexer> logger)
    {
        _dbContext = dbContext;
        _fileStore = fileStore;
        _textExtractor = textExtractor;
        _vectorIndex = vectorIndex;
        _aiRunWriter = aiRunWriter;
        _clock = clock;
        _logger = logger;
    }

    public async Task IngestAsync(
        Guid documentId,
        Guid documentFileId,
        CancellationToken cancellationToken = default)
    {
        // Queries are tenant-filtered by DocumentsDbContext; the outbox restored the event's tenant.
        var document = await _dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);
        if (document is null)
        {
            _logger.LogInformation(
                "Ingestion skipped: document {DocumentId} no longer exists (deleted before indexing).",
                documentId);
            return;
        }

        var file = await _dbContext.DocumentFiles
            .FirstOrDefaultAsync(f => f.Id == documentFileId && f.DocumentId == documentId, cancellationToken);
        if (file is null)
        {
            _logger.LogInformation(
                "Ingestion skipped: file {FileId} for document {DocumentId} no longer exists.",
                documentFileId, documentId);
            return;
        }

        // Defence in depth: the publish side only raises the event for indexable documents, but a
        // Restricted/Sensitive/NotIndexable document must never be embedded even if it slips through.
        if (!IsIndexable(document))
        {
            _logger.LogInformation(
                "Ingestion skipped: document {DocumentId} is not indexable (classification {Classification}, " +
                "status {IndexStatus}).",
                documentId, document.Classification, document.IndexStatus);
            return;
        }

        var ingestion = await _dbContext.DocumentIngestions
            .FirstOrDefaultAsync(i => i.DocumentFileId == documentFileId, cancellationToken);
        if (ingestion is null)
        {
            ingestion = new DocumentIngestion
            {
                Id = Guid.NewGuid(),
                TenantId = document.TenantId,
                DocumentId = documentId,
                DocumentFileId = documentFileId,
                VectorCollection = VectorCollectionName,
            };
            _dbContext.DocumentIngestions.Add(ingestion);
        }

        ingestion.Status = StatusRunning;
        ingestion.Attempts++;
        ingestion.LastError = null;

        var startedAt = _clock.UtcNow;
        var aiRunId = Guid.Empty;

        try
        {
            var inputRefs = JsonSerializer.Serialize(new
            {
                documentId,
                documentFileId,
                document.DocumentType,
                classification = document.Classification.ToString(),
            });

            // Records the embedding run as an auditable AiRun (Spec 035 R9). May throw if the
            // tenant's agent kill-switch is engaged — we let that propagate so the outbox retries
            // with back-off and the document indexes once the operator lifts the pause.
            aiRunId = await _aiRunWriter.StartRunAsync(IngestionUseCase, inputRefs, cancellationToken);
            ingestion.AiRunId = aiRunId;

            await using var blob = await _fileStore.OpenReadAsync(file.StorageKey, cancellationToken);
            var extraction = await _textExtractor.ExtractTextAsync(blob, file.ContentType, cancellationToken);
            file.ExtractedTextStatus = extraction.Status;

            var chunks = extraction.HasText ? TextChunker.Chunk(extraction.Text) : Array.Empty<string>();

            if (chunks.Count == 0)
            {
                // No embeddable text yet (OCR deferred / unsupported / empty). This is not a
                // failure: consume the event and record the deferral. The document stays Pending so
                // a future OCR backfill can re-publish and complete it.
                ingestion.Status = StatusSkipped;
                ingestion.ChunkCount = 0;
                ingestion.CompletedAt = _clock.UtcNow;
                ingestion.LastError = Truncate(
                    $"No embeddable text extracted (status: {extraction.Status}).");

                await _aiRunWriter.MarkRunCompletedAsync(
                    aiRunId, $"skipped:{extraction.Status}", cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Ingestion deferred for document {DocumentId}: {Status} ({ContentType}).",
                    documentId, extraction.Status, file.ContentType);
                return;
            }

            var result = await _vectorIndex.IndexDocumentAsync(
                new DocumentIndexRequest(
                    DocumentId: documentId,
                    OwnerPartyId: document.OwnerPartyId,
                    Classification: document.Classification,
                    DocumentType: document.DocumentType,
                    Purpose: null,
                    Chunks: chunks),
                cancellationToken);

            var completedAt = _clock.UtcNow;

            ingestion.Status = StatusSucceeded;
            ingestion.ChunkCount = result.ChunkCount;
            ingestion.EmbeddingModel = result.EmbeddingModel;
            ingestion.EmbeddingCost = result.EstimatedCost;
            ingestion.CompletedAt = completedAt;

            document.IndexStatus = DocumentIndexStatus.Indexed;
            document.IndexedAt = completedAt;

            await _aiRunWriter.MarkRunCompletedWithMetricsAsync(
                aiRunId,
                tokensUsed: 0,
                latencyMs: ElapsedMs(startedAt, completedAt),
                costEstimate: result.EstimatedCost,
                outputRef: $"chunks:{result.ChunkCount}",
                cancellationToken);

            // Lets consumers react once the document becomes searchable (Spec 035 §11). Enqueued on
            // the same context so it commits atomically with the Indexed transition below.
            _dbContext.EnqueueIntegrationEvent(
                new DocumentIndexedEvent(document.TenantId, documentId, result.ChunkCount));

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Indexed document {DocumentId}: {ChunkCount} chunks via {Model} (party {PartyId}, {Classification}).",
                documentId, result.ChunkCount, result.EmbeddingModel, document.OwnerPartyId, document.Classification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Ingestion failed for document {DocumentId} file {FileId} (attempt {Attempt}); the outbox will retry.",
                documentId, documentFileId, ingestion.Attempts);

            ingestion.Status = StatusFailed;
            ingestion.LastError = Truncate(ex.Message);
            ingestion.CompletedAt = _clock.UtcNow;
            document.IndexStatus = DocumentIndexStatus.Failed;

            await TryRecordTerminalFailureAsync(aiRunId, ex.Message, cancellationToken);

            // Rethrow so the outbox increments its own attempt count, backs off, and dead-letters
            // after the configured maximum. The DocumentIngestion row captures the per-run audit.
            throw;
        }
    }

    private static bool IsIndexable(Document document) =>
        document.IndexStatus is DocumentIndexStatus.Pending or DocumentIndexStatus.Indexed or DocumentIndexStatus.Failed
        && document.Classification is not (DocumentClassification.Restricted or DocumentClassification.Sensitive);

    /// <summary>
    /// Best-effort persistence of the failure state and AiRun outcome. Never throws: a failure here
    /// must not mask the original ingestion exception that the caller is about to rethrow.
    /// </summary>
    private async Task TryRecordTerminalFailureAsync(Guid aiRunId, string reason, CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception saveEx)
        {
            _logger.LogWarning(saveEx, "Failed to persist ingestion failure state.");
        }

        if (aiRunId == Guid.Empty)
        {
            return;
        }

        try
        {
            await _aiRunWriter.MarkRunFailedAsync(aiRunId, reason, cancellationToken);
        }
        catch (Exception runEx)
        {
            _logger.LogWarning(runEx, "Failed to mark AiRun {AiRunId} as failed.", aiRunId);
        }
    }

    private static int ElapsedMs(DateTime start, DateTime end)
    {
        var ms = (end - start).TotalMilliseconds;
        return ms is > 0 and < int.MaxValue ? (int)ms : 0;
    }

    private static string Truncate(string value) =>
        value.Length <= MaxLastErrorLength ? value : value[..MaxLastErrorLength];
}
