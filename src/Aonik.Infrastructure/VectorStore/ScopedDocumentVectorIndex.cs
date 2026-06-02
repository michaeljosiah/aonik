namespace Aonik.Infrastructure.VectorStore;

using System.Security.Cryptography;
using System.Text;
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
/// See <a href="../../../docs/specifications/035.extract-documents-module.html">Spec 035 §14</a>.
/// </summary>
internal sealed class ScopedDocumentVectorIndex : IDocumentSearch, IDocumentVectorIndex
{
    private const string CollectionType = "documents";
    private const float SearchScoreThreshold = 0.2f;
    private const int PurgeScrollLimit = 1000;

    /// <summary>
    /// Tenant-scoped (non-party) classifications: safe to return without an owner-party scope.
    /// Used as the default classification allow-list when a search names none — on its own for a
    /// tenant-wide search, or with <see cref="DocumentClassification.Personal"/> added for an
    /// owner-scoped search. <see cref="DocumentClassification.Sensitive"/> is never in the default;
    /// it requires an explicit purpose scope (see <see cref="ValidateScope"/>).
    /// </summary>
    private static readonly DocumentClassification[] TenantWideClassifications =
        { DocumentClassification.Public, DocumentClassification.Internal };

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
        // Fail closed BEFORE any side effect (the write-side mirror of ValidateScope): a
        // mis-scoped request must not destructively purge the document's existing vectors and
        // then fail. Rejecting party-scoped content without a real owner — or Sensitive without a
        // purpose — also guarantees we never write chunks stamped owner_party_id = Guid.Empty,
        // which would be orphaned from scoped searches yet reachable by a tenant-wide scope.
        ValidateIndexRequest(request);

        // Re-index is a full replace: purge any existing vectors for this document before
        // writing the new chunks. Deterministic ids overwrite chunks that still exist, but a
        // re-extraction yielding FEWER chunks would otherwise leave the previous higher-index
        // chunks behind as stale, still-searchable vectors. Purging first also handles a
        // re-index to an empty extraction (the document's vectors are simply removed).
        await PurgeDocumentAsync(request.DocumentId, cancellationToken);

        if (request.Chunks.Count == 0)
            return 0;

        // Classification gate: not every classification is embedded into the vector store.
        // Restricted is never indexed (direct read only); Sensitive is metadata-only until OCR +
        // redaction can safely process it (ADR-009 / DocumentClassification). The raw content is
        // therefore never sent to the embedding service. The purge above still applied, so any
        // previously-indexed vectors are removed; the search side already supports Sensitive
        // retrieval for when indexing is later enabled.
        if (request.Classification is DocumentClassification.Restricted or DocumentClassification.Sensitive)
        {
            _logger.LogInformation(
                "Skipped vector indexing for document {DocumentId}: classification {Classification} is not " +
                "embedded (Restricted is never indexed; Sensitive is metadata-only until OCR + redaction).",
                request.DocumentId, request.Classification);
            return 0;
        }

        var collection = _config.GetCollectionName(CollectionType);
        var embeddings = (await _embeddingService
            .GetEmbeddingsBatchAsync(request.Chunks, cancellationToken)).ToList();

        if (embeddings.Count != request.Chunks.Count)
            throw new InvalidOperationException(
                $"Embedding count {embeddings.Count} does not match chunk count {request.Chunks.Count} " +
                $"for document {request.DocumentId}.");

        for (var i = 0; i < request.Chunks.Count; i++)
        {
            var vectorId = ChunkPointId(request.DocumentId, i);
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

        // Fail closed before any retrieval: Personal/Sensitive documents are party-scoped
        // by contract, so a missing owner party must be rejected, not silently widened.
        ValidateScope(scope);

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

        // Page through the entire result set. A document can have more chunks than a single
        // scroll page, so stopping at the first page would silently leave the remaining
        // vectors searchable (a right-to-erasure / re-index correctness hole). Collect the
        // ids first (payload not needed), then delete. tenant_id is merged in fail-closed by
        // the vector store, so a purge can never reach across tenants even on a shared
        // collection.
        var pointIds = new List<string>();
        string? offset = null;
        while (true)
        {
            var page = await _vectorStore.ScrollPageAsync(
                collection, filter, PurgeScrollLimit, offset, withPayload: false, cancellationToken);

            if (page.Points.Count == 0)
                break;

            pointIds.AddRange(page.Points.Select(p => p.Id));

            offset = page.NextOffset;
            if (offset is null)
                break;
        }

        foreach (var id in pointIds)
            await _vectorStore.DeleteAsync(collection, filterId: id, cancellationToken: cancellationToken);

        _logger.LogDebug("Purged {Count} vectors for document {DocumentId}", pointIds.Count, documentId);
        return pointIds.Count;
    }

    /// <summary>
    /// Builds the additional <c>must</c> clauses layered on top of the store's tenant clause.
    /// When no classification filter is supplied, retrieval defaults to a fail-closed, non-Sensitive
    /// allow-list (Public/Internal, plus Personal when an owner party is set), so a generic lookup
    /// can never surface another party's chunks or any Sensitive content. Reaching Sensitive
    /// requires an explicit, purpose-scoped request (see <see cref="ValidateScope"/>).
    /// </summary>
    private static Dictionary<string, object>? BuildScopeFilter(DocumentSearchScope scope)
    {
        var must = new List<object>();

        if (scope.OwnerPartyId is { } partyId)
            must.Add(MatchClause("owner_party_id", partyId.ToString()));

        if (scope.Classifications is { Count: > 0 } classifications)
        {
            must.Add(MatchAnyClause("classification", classifications.Select(c => (object)c.ToString())));
        }
        else
        {
            // No explicit classification filter. Apply a fail-closed default that never includes
            // Sensitive — it requires an explicit purpose scope (see ValidateScope), so a generic
            // lookup (even an owner-scoped one) must not surface it. Public/Internal are
            // tenant-wide; Personal is added only when an owner party scopes the search. Using a
            // positive allow-list means any future party-scoped/sensitive classification is
            // excluded by default rather than leaking through a no-classification lookup.
            var defaults = TenantWideClassifications.Select(c => (object)c.ToString()).ToList();
            if (scope.OwnerPartyId is not null)
                defaults.Add(DocumentClassification.Personal.ToString());
            must.Add(MatchAnyClause("classification", defaults));
        }

        if (scope.Purposes is { Count: > 0 } purposes)
            must.Add(MatchAnyClause("purpose", purposes.Select(p => (object)p)));

        return must.Count == 0
            ? null
            : new Dictionary<string, object> { ["must"] = must.ToArray() };
    }

    /// <summary>
    /// Fail-closed scope validation. Personal and Sensitive documents are party-scoped by
    /// contract (see <see cref="DocumentSearchScope"/> / <see cref="DocumentClassification"/>),
    /// so the caller must supply an owner party; Sensitive additionally requires a purpose.
    /// Without these we refuse rather than fall back to a tenant-wide filter that would return
    /// every matching party's chunks — the leak this adapter exists to prevent.
    /// </summary>
    private static void ValidateScope(DocumentSearchScope scope)
    {
        if (scope.Classifications is not { Count: > 0 } classifications)
            return;

        var requiresOwnerParty = classifications.Any(c =>
            c is DocumentClassification.Personal or DocumentClassification.Sensitive);
        if (requiresOwnerParty && scope.OwnerPartyId is null)
            throw new ArgumentException(
                "OwnerPartyId is required to search Personal or Sensitive documents; the sub-tenant " +
                "isolation boundary will not widen retrieval across parties within a tenant.",
                nameof(scope));

        if (classifications.Contains(DocumentClassification.Sensitive)
            && scope.Purposes is not { Count: > 0 })
            throw new ArgumentException(
                "At least one Purpose is required to search Sensitive documents.",
                nameof(scope));
    }

    /// <summary>
    /// Write-side fail-closed validation, the mirror of <see cref="ValidateScope"/>. Party-scoped
    /// content (Personal/Sensitive) must carry a real owner party, and Sensitive must carry a
    /// purpose. Rejecting here prevents writing chunks stamped with an empty <c>owner_party_id</c>,
    /// which would be orphaned from properly-scoped searches yet reachable by a tenant-wide scope.
    /// See <see cref="DocumentClassification"/>.
    /// </summary>
    private static void ValidateIndexRequest(DocumentIndexRequest request)
    {
        var partyScoped = request.Classification
            is DocumentClassification.Personal or DocumentClassification.Sensitive;
        if (partyScoped && request.OwnerPartyId == Guid.Empty)
            throw new ArgumentException(
                $"OwnerPartyId is required to index {request.Classification} documents; party-scoped " +
                "content must have a real owner party.",
                nameof(request));

        if (request.Classification is DocumentClassification.Sensitive
            && string.IsNullOrWhiteSpace(request.Purpose))
            throw new ArgumentException(
                "A Purpose is required to index Sensitive documents.",
                nameof(request));
    }

    /// <summary>
    /// Builds a stable Qdrant point id for a chunk. Qdrant only accepts a UUID or uint64 as a
    /// point id, so the readable "&lt;document&gt;:chunk:&lt;n&gt;" form is invalid and is kept in the
    /// payload (document_id / chunk_index) instead. The id is derived deterministically from
    /// (documentId, chunkIndex) so re-indexing a chunk overwrites its point in place rather than
    /// creating a duplicate. Qdrant requires only a well-formed UUID (no particular version), so
    /// the hash bytes are used directly.
    /// </summary>
    private static string ChunkPointId(Guid documentId, int chunkIndex)
    {
        var seed = Encoding.UTF8.GetBytes($"{documentId:N}:{chunkIndex}");
        var hash = SHA256.HashData(seed);
        return new Guid(hash.AsSpan(0, 16)).ToString();
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
