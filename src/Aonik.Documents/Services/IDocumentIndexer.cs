namespace Aonik.Documents.Services;

/// <summary>
/// Orchestrates RAG ingestion of one uploaded document file (Spec 035 §13): load the blob,
/// extract text, chunk, embed + upsert through the party-scoped vector index, and record an
/// auditable <c>DocumentIngestion</c> run carrying its <c>AiRunId</c>. Invoked asynchronously by
/// <c>DocumentIngestionHandler</c> consuming <c>DocumentUploadedEvent</c> in the Worker, so the
/// upload request never blocks on embedding. Throws on a genuine processing failure so the outbox
/// retries with back-off; a non-embeddable file (OCR deferred) is recorded and consumed, not thrown.
/// </summary>
internal interface IDocumentIndexer
{
    Task IngestAsync(Guid documentId, Guid documentFileId, CancellationToken cancellationToken = default);
}
