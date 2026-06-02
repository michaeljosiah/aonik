namespace Aonik.Infrastructure.VectorStore;

using Aonik.Infrastructure.VectorStore.Contracts;
using Aonik.Infrastructure.VectorStore.Qdrant;
using Aonik.SharedKernel.Abstractions.Documents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Party-scoped document RAG index over the shared <see cref="IVectorStore"/>.
///
/// The vector store already enforces <c>tenant_id</c> isolation fail-closed (it injects
/// the tenant on every upsert and adds a tenant <c>must</c> clause to every search/scroll).
/// This adapter adds the missing sub-tenant guarantees required once personal documents
/// (tax returns, statements, KYC evidence) are indexed by default: it stamps
/// <c>owner_party_id</c>, <c>classification</c>, and <c>purpose</c> on every chunk and
/// filters on <c>owner_party_id</c> at retrieval, so within a tenant one party's documents
/// are not retrievable by another party's agent. Search scope is supplied by the caller from
/// authenticated context — never from model input.
/// See <a href="../../../docs/specifications/033.extract-documents-module.html">Spec 033 §14</a>.
/// </summary>
internal sealed class ScopedDocumentVectorIndex : IDocumentSearch, IDocumentVectorIndex
{
    private const string CollectionType = "documents";
    private const float SearchScoreThreshold = 0.2f;
    private const int PurgeScrollLimit = 1000;

    private readonly IVectorStore _vectorStore;
    private readonly IEmbeddingService _embeddingService;
    private readonly QdrantConfiguration _config;
    private readonly ILogger<ScopedDocumentVectorIndex> _logger;

    public ScopedDocumentVectorIndex(
        IVectorStore vectorStore,
        IEmbeddingService embeddingService,
        IOptions<QdrantConfiguration> options,
        ILogger<ScopedDocumentVectorIndex> logger)
    {
        _vectorStore = vectorStore;
        _embeddingService = embeddingService;
        _config = options.Value;
        _logger = logger;
    }

    public async Task<int> IndexDocumentAsync(
        DocumentIndexRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Chunks.Count == 0)
            return 0;

        var collection = _config.GetCollectionName(CollectionType);
        var embeddings = (await _embeddingService
            .GetEmbeddingsBatchAsync(request.Chunks, cancellationToken)).ToList();

        if (embeddings.Count != request.Chunks.Count)
            throw new InvalidOperationException(
                $"Embedding count {embeddings.Count} does not match chunk count {request.Chunks.Count} " +
                $"for document {request.DocumentId}.");

        for (var i = 0; i < request.Chunks.Count; i++)
        {
            var vectorId = $"{request.DocumentId:N}:chunk:{i}";
            var payload = new Dictionary<string, object>
            {
                ["document_id"] = request.DocumentId.ToString(),
                ["owner_party_id"] = request.OwnerPartyId.ToString(),
                ["classification"] = request.Classification.ToString(),
                ["document_type"] = request.DocumentType,
                ["chunk_index"] = i,
                ["content"] = request.Chunks[i],
                ["created_at"] = DateTime.UtcNow,
            };
            if (!string.IsNullOrWhiteSpace(request.Purpose))
                payload["purpose"] = request.Purpose!;

            // tenant_id is injected fail-closed by the vector store.
            await _vectorStore.UpsertVectorAsync(collection, vectorId, embeddings[i], payload, cancellationToken);
        }

        _logger.LogDebug(
            "Indexed {ChunkCount} chunks for document {DocumentId} (party {PartyId}, {Classification})",
            request.Chunks.Count, request.DocumentId, request.OwnerPartyId, request.Classification);

        return request.Chunks.Count;
    }

    public async Task<IReadOnlyList<DocumentChunkHit>> SearchAsync(
        string query,
        DocumentSearchScope scope,
        int topK = 8,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query is required.", nameof(query));
        if (topK <= 0)
            topK = 8;

        var collection = _config.GetCollectionName(CollectionType);
        var queryEmbedding = await _embeddingService.GetEmbeddingAsync(query, cancellationToken);
        var filter = BuildScopeFilter(scope);

        var hits = await _vectorStore.SearchAsync(
            collection, queryEmbedding, topK, SearchScoreThreshold, filter, cancellationToken);

        var results = new List<DocumentChunkHit>();
        foreach (var hit in hits)
        {
            if (hit.Payload is not { } p)
                continue;

            results.Add(new DocumentChunkHit(
                DocumentId: GetGuid(p, "document_id"),
                ChunkIndex: GetInt(p, "chunk_index"),
                Content: GetString(p, "content"),
                Score: hit.Score,
                DocumentType: GetString(p, "document_type"),
                OwnerPartyId: GetGuid(p, "owner_party_id")));
        }

        return results;
    }

    public async Task<int> PurgeDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var collection = _config.GetCollectionName(CollectionType);
        var filter = new Dictionary<string, object>
        {
            ["must"] = new object[]
            {
                MatchClause("document_id", documentId.ToString()),
            },
        };

        // tenant_id clause is merged in fail-closed by the vector store, so a purge can
        // never reach across tenants even with a shared collection.
        var points = (await _vectorStore.ScrollAsync(collection, filter, PurgeScrollLimit, cancellationToken)).ToList();
        foreach (var point in points)
            await _vectorStore.DeleteAsync(collection, filterId: point.Id, cancellationToken: cancellationToken);

        _logger.LogDebug("Purged {Count} vectors for document {DocumentId}", points.Count, documentId);
        return points.Count;
    }

    /// <summary>
    /// Builds the additional <c>must</c> clauses layered on top of the store's tenant clause.
    /// Returns null when the scope adds no constraints (tenant-wide admin scope), in which case
    /// tenant isolation still applies.
    /// </summary>
    private static Dictionary<string, object>? BuildScopeFilter(DocumentSearchScope scope)
    {
        var must = new List<object>();

        if (scope.OwnerPartyId is { } partyId)
            must.Add(MatchClause("owner_party_id", partyId.ToString()));

        if (scope.Classifications is { Count: > 0 } classifications)
            must.Add(MatchAnyClause("classification", classifications.Select(c => (object)c.ToString())));

        if (scope.Purposes is { Count: > 0 } purposes)
            must.Add(MatchAnyClause("purpose", purposes.Select(p => (object)p)));

        return must.Count == 0
            ? null
            : new Dictionary<string, object> { ["must"] = must.ToArray() };
    }

    private static Dictionary<string, object> MatchClause(string key, string value) => new()
    {
        ["key"] = key,
        ["match"] = new Dictionary<string, object> { ["value"] = value },
    };

    private static Dictionary<string, object> MatchAnyClause(string key, IEnumerable<object> values) => new()
    {
        ["key"] = key,
        ["match"] = new Dictionary<string, object> { ["any"] = values.ToArray() },
    };

    private static Guid GetGuid(IReadOnlyDictionary<string, object> payload, string key)
        => payload.TryGetValue(key, out var v) && Guid.TryParse(v?.ToString(), out var g) ? g : Guid.Empty;

    private static int GetInt(IReadOnlyDictionary<string, object> payload, string key)
        => payload.TryGetValue(key, out var v) && int.TryParse(v?.ToString(), out var i) ? i : 0;

    private static string GetString(IReadOnlyDictionary<string, object> payload, string key)
        => payload.TryGetValue(key, out var v) ? v?.ToString() ?? string.Empty : string.Empty;
}
