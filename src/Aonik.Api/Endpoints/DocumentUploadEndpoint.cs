namespace Aonik.Api.Endpoints;

using Aonik.Infrastructure.VectorStore.Contracts;
using Aonik.Infrastructure.VectorStore.Qdrant;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IO;
using System.Text;

/// <summary>
/// Document upload endpoint for ingesting documents into the vector store.
/// Accepts documents, extracts text, chunks content, generates embeddings, and stores vectors.
/// </summary>
internal sealed class DocumentUploadEndpoint : Endpoint<DocumentUploadRequest, DocumentUploadResponse>
{
    private readonly IVectorStore _vectorStore;
    private readonly IEmbeddingService _embeddingService;
    private readonly QdrantConfiguration _qdrantConfig;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<DocumentUploadEndpoint> _logger;

    private const long MaxDocumentSizeBytes = 10 * 1024 * 1024; // 10 MB

    public DocumentUploadEndpoint(
        IVectorStore vectorStore,
        IEmbeddingService embeddingService,
        IOptions<QdrantConfiguration> qdrantOptions,
        ITenantProvider tenantProvider,
        ILogger<DocumentUploadEndpoint> logger)
    {
        _vectorStore = vectorStore;
        _embeddingService = embeddingService;
        _qdrantConfig = qdrantOptions.Value;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("/ai/documents/upload");
        AllowFileUploads();
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Upload document for RAG";
            s.Description = "Upload a document (text, PDF, docx) to be chunked, embedded, and stored in the vector store for RAG retrieval.";
            s.Response(201, "Document processed and stored");
            s.Response(400, "Invalid document or processing error");
            s.Response(401, "Not authenticated");
            s.Response(413, "Document too large");
        });
        Options(x => x.WithTags("AI - Documents"));
    }

    public override async Task HandleAsync(DocumentUploadRequest req, CancellationToken ct)
    {
        if (req.Document == null || req.Document.Length == 0)
        {
            await Send.ErrorsAsync(400, ct);
            return;
        }

        if (req.Document.Length > MaxDocumentSizeBytes)
        {
            ThrowError("Document exceeds maximum size of 10 MB");
            return;
        }

        try
        {
            // Validate file type
            if (!IsValidContentType(req.Document.ContentType))
            {
                ThrowError(
                    $"Unsupported file type: {req.Document.ContentType}. Supported: text/plain, application/pdf, application/vnd.openxmlformats-officedocument.wordprocessingml.document");
                return;
            }

            // Sanitize filename to prevent path traversal
            var safeFilename = SanitizeFilename(req.Document.FileName);

            // 1. Extract text from document
            var text = await ExtractTextAsync(req.Document, ct);
            if (string.IsNullOrWhiteSpace(text))
            {
                ThrowError("No text content extracted from document");
                return;
            }

            // 2. Chunk text into smaller pieces for embedding
            var chunks = ChunkText(text, chunkSize: 512, overlapSize: 100);
            if (!chunks.Any())
            {
                ThrowError("Document produced no chunks after processing");
                return;
            }

            // 3. Batch embed chunks
            var embeddings = await _embeddingService.GetEmbeddingsBatchAsync(
                chunks.Select(c => c.Content),
                ct);

            var embeddingList = embeddings.ToList();
            if (embeddingList.Count != chunks.Count)
            {
                ThrowError("Embedding count mismatch with chunk count");
                return;
            }

            // 4. Upsert vectors to Qdrant
            var documentId = Guid.NewGuid().ToString();
            var collectionName = _qdrantConfig.GetCollectionName("documents");
            var chunkCount = 0;

            for (int i = 0; i < chunks.Count; i++)
            {
                var vectorId = $"{documentId}:chunk:{i}";
                var payload = new Dictionary<string, object>
                {
                    { "document_id", documentId },
                    { "chunk_index", i },
                    { "source", req.SourceName ?? "uploaded_document" },
                    { "content", chunks[i].Content },
                    { "created_at", DateTime.UtcNow },
                    { "filename", safeFilename }
                };

                await _vectorStore.UpsertVectorAsync(
                    collectionName,
                    vectorId,
                    embeddingList[i],
                    payload,
                    ct);

                chunkCount++;
            }

            _logger.LogInformation(
                "Successfully processed document {DocumentId} with {ChunkCount} chunks for tenant {Tenant}",
                documentId,
                chunkCount,
                _tenantProvider.TryGetCurrentTenantId(out var tid) ? tid : Guid.Empty);

            // 5. Return response
            var estimatedCost = (embeddingList.Sum(e => e.Length) / 1000000.0) * 0.02;
            var response = new DocumentUploadResponse
            {
                DocumentId = documentId,
                ChunksStored = chunkCount,
                EmbeddingCost = (decimal)estimatedCost
            };

            await Send.CreatedAtAsync<GetDocumentEndpoint>(
                new { documentId },
                response,
                generateAbsoluteUrl: false,
                cancellation: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Document upload failed");
            ThrowError($"Document processing failed: {ex.Message}");
        }
    }

    private static string SanitizeFilename(string? filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
            return "unnamed_document";

        // Strip path components and invalid characters
        var name = Path.GetFileName(filename);
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Where(c => !invalidChars.Contains(c)).ToArray());

        return string.IsNullOrWhiteSpace(sanitized) ? "unnamed_document" : sanitized;
    }

    private static bool IsValidContentType(string? contentType) =>
        contentType switch
        {
            "text/plain" => true,
            "application/pdf" => true,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => true,
            _ => false
        };

    private static async Task<string> ExtractTextAsync(IFormFile file, CancellationToken ct)
    {
        if (file.ContentType == "text/plain")
        {
            using var reader = new StreamReader(file.OpenReadStream());
            return await reader.ReadToEndAsync(ct);
        }

        // Placeholder for PDF/DOCX extraction
        throw new NotImplementedException(
            $"Text extraction for {file.ContentType} not yet implemented. Currently supports text/plain.");
    }

    private static List<TextChunk> ChunkText(string text, int chunkSize = 512, int overlapSize = 100)
    {
        var chunks = new List<TextChunk>();
        var words = text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);

        var currentChunk = new StringBuilder();
        var currentWordCount = 0;

        foreach (var word in words)
        {
            currentChunk.Append(word).Append(' ');
            currentWordCount++;

            if (currentWordCount >= chunkSize)
            {
                chunks.Add(new TextChunk { Content = currentChunk.ToString().Trim() });

                // Create overlap by keeping last N words
                var wordsArray = currentChunk.ToString().Split(' ');
                var overlapWords = wordsArray.TakeLast(overlapSize).ToList();
                currentChunk.Clear();
                currentChunk.AppendJoin(" ", overlapWords);
                if (overlapWords.Any()) currentChunk.Append(' ');
                currentWordCount = overlapWords.Count;
            }
        }

        // Add remaining chunk
        if (currentChunk.Length > 0)
        {
            chunks.Add(new TextChunk { Content = currentChunk.ToString().Trim() });
        }

        return chunks;
    }

    private record TextChunk
    {
        public required string Content { get; init; }
    }

    // Stub endpoint for CreatedAt reference
    private sealed class GetDocumentEndpoint : EndpointWithoutRequest
    {
        public override void Configure()
        {
            Get("/ai/documents/{documentId}");
            Policies("UserPolicy");
        }

        public override Task HandleAsync(CancellationToken ct) => Task.CompletedTask;
    }
}

public sealed class DocumentUploadRequest
{
    public required IFormFile Document { get; set; }
    public string? SourceName { get; set; }
}

public sealed record DocumentUploadResponse
{
    public required string DocumentId { get; set; }
    public required int ChunksStored { get; set; }
    public required decimal EmbeddingCost { get; set; }
}
