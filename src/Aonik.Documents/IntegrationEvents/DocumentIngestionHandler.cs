using Aonik.Documents.Services;
using Aonik.SharedKernel.Events;
using Aonik.SharedKernel.Events.Integration;
using Microsoft.Extensions.Logging;

namespace Aonik.Documents.IntegrationEvents;

/// <summary>
/// Consumes <see cref="DocumentUploadedEvent"/> and drives RAG ingestion (Spec 035 §13).
/// Discovered by the SharedKernel event-handler assembly scan and invoked by the transactional
/// outbox dispatcher — which runs only in the Worker host, with the originating tenant already
/// restored — so ingestion happens exactly once, off the upload request path, with the outbox's
/// durable retry / back-off / dead-letter behaviour. The handler is deliberately thin; all
/// orchestration lives in <see cref="IDocumentIndexer"/>.
/// </summary>
internal sealed class DocumentIngestionHandler : IEventHandler<DocumentUploadedEvent>
{
    private readonly IDocumentIndexer _indexer;
    private readonly ILogger<DocumentIngestionHandler> _logger;

    public DocumentIngestionHandler(IDocumentIndexer indexer, ILogger<DocumentIngestionHandler> logger)
    {
        _indexer = indexer;
        _logger = logger;
    }

    public async Task HandleAsync(DocumentUploadedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "DocumentUploadedEvent received for document {DocumentId} file {FileId} ({Classification}); starting ingestion.",
            @event.DocumentId, @event.DocumentFileId, @event.Classification);

        await _indexer.IngestAsync(@event.DocumentId, @event.DocumentFileId, cancellationToken);
    }
}
