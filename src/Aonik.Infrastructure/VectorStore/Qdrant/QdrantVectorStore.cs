namespace Aonik.Infrastructure.VectorStore.Qdrant;

using System.Collections.Concurrent;
using System.Diagnostics;
using Aonik.Infrastructure.VectorStore;
using Aonik.Infrastructure.VectorStore.Contracts;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Qdrant vector store implementation.
/// Provides RAG capabilities via vector search over embeddings.
/// </summary>
internal class QdrantVectorStore : IVectorStore
{
    private readonly QdrantHttpClient _httpClient;
    private readonly QdrantConfiguration _config;
    private readonly ITenantProvider _tenantProvider;
    private readonly QdrantMetrics _metrics;
    private readonly ILogger<QdrantVectorStore> _logger;
    private readonly ConcurrentDictionary<string, bool> _knownCollections = new();

    public QdrantVectorStore(
        QdrantHttpClient httpClient,
        IOptions<QdrantConfiguration> options,
        ITenantProvider tenantProvider,
        QdrantMetrics metrics,
        ILogger<QdrantVectorStore> logger)
    {
        _httpClient = httpClient;
        _config = options.Value;
        _tenantProvider = tenantProvider;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task UpsertVectorAsync(
        string collectionName,
        string vectorId,
        float[] embedding,
        Dictionary<string, object>? payload = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = _metrics.ActivitySource.StartActivity("qdrant.vector.upsert", ActivityKind.Client);
        activity?.SetTag("db.system", "qdrant");
        activity?.SetTag("db.collection.name", collectionName);
        activity?.SetTag("aonik.vector.id", vectorId);
        activity?.SetTag("aonik.vector.dimension_count", embedding?.Length ?? 0);

        if (string.IsNullOrWhiteSpace(collectionName))
            throw new ArgumentException("Collection name required", nameof(collectionName));
        if (string.IsNullOrWhiteSpace(vectorId))
            throw new ArgumentException("Vector ID required", nameof(vectorId));
        if (embedding == null || embedding.Length == 0)
            throw new ArgumentException("Embedding required", nameof(embedding));
        if (embedding.Length != _config.VectorDimensions)
            throw new ArgumentException(
                $"Embedding dimensions {embedding.Length} do not match configured {_config.VectorDimensions}",
                nameof(embedding));

        // Ensure collection exists (cached check)
        await EnsureCollectionExistsAsync(collectionName, cancellationToken);

        // Add tenant isolation — fail-closed: throws if no tenant context
        var enhancedPayload = EnhancePayloadWithTenant(payload);

        try
        {
            await _httpClient.UpsertPointAsync(
                collectionName,
                vectorId,
                embedding,
                enhancedPayload,
                cancellationToken);

            _logger.LogDebug(
                "Upserted vector {VectorId} to collection {Collection}",
                vectorId,
                collectionName);
        }
        catch (Exception ex)
        {
            AiTelemetry.MarkError(activity, ex);
            _logger.LogError(
                ex,
                "Failed to upsert vector {VectorId} to collection {Collection}",
                vectorId,
                collectionName);
            throw;
        }
    }

    public Task<IEnumerable<VectorSearchResult>> SearchAsync(
        string collectionName,
        float[] queryEmbedding,
        int limit = 10,
        float scoreThreshold = 0.5f,
        CancellationToken cancellationToken = default)
        => SearchAsync(collectionName, queryEmbedding, limit, scoreThreshold, additionalFilter: null, cancellationToken);

    public async Task<IEnumerable<VectorSearchResult>> SearchAsync(
        string collectionName,
        float[] queryEmbedding,
        int limit,
        float scoreThreshold,
        Dictionary<string, object>? additionalFilter,
        CancellationToken cancellationToken = default)
    {
        using var activity = _metrics.ActivitySource.StartActivity("qdrant.vector.search", ActivityKind.Client);
        activity?.SetTag("db.system", "qdrant");
        activity?.SetTag("db.collection.name", collectionName);
        activity?.SetTag("aonik.vector.limit", limit);
        activity?.SetTag("aonik.vector.score_threshold", scoreThreshold);
        activity?.SetTag("aonik.vector.dimension_count", queryEmbedding?.Length ?? 0);

        if (string.IsNullOrWhiteSpace(collectionName))
            throw new ArgumentException("Collection name required", nameof(collectionName));
        if (queryEmbedding == null || queryEmbedding.Length == 0)
            throw new ArgumentException("Query embedding required", nameof(queryEmbedding));
        if (queryEmbedding.Length != _config.VectorDimensions)
            throw new ArgumentException(
                $"Query embedding dimensions {queryEmbedding.Length} do not match configured {_config.VectorDimensions}",
                nameof(queryEmbedding));
        if (limit <= 0)
            throw new ArgumentException("Limit must be > 0", nameof(limit));
        if (scoreThreshold < 0 || scoreThreshold > 1)
            throw new ArgumentException("Score threshold must be between 0 and 1", nameof(scoreThreshold));

        // Build tenant filter, merging additional constraints — fail-closed
        var filter = BuildMergedFilter(additionalFilter);

        try
        {
            var hits = await _httpClient.SearchAsync(
                collectionName,
                queryEmbedding,
                limit,
                scoreThreshold,
                filter,
                cancellationToken);

            var results = hits
                .Select(h => new VectorSearchResult(h.Id, h.Score, h.Payload))
                .ToList();

            activity?.SetTag("aonik.vector.result_count", results.Count);

            _logger.LogDebug(
                "Search in collection {Collection} returned {Count} results",
                collectionName,
                results.Count);

            return results;
        }
        catch (System.Net.Http.HttpRequestException ex)
            when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // The collection does not exist yet (nothing has been indexed into it). A search over a
            // not-yet-created collection has no matches — return empty rather than surfacing a 404 as
            // an error to the agent's document-search tool.
            _logger.LogDebug(
                "Search on collection {Collection} returned 404 (collection not yet created); treating as no matches.",
                collectionName);
            return new List<VectorSearchResult>();
        }
        catch (Exception ex)
        {
            AiTelemetry.MarkError(activity, ex);
            _logger.LogError(
                ex,
                "Search failed in collection {Collection}",
                collectionName);
            throw;
        }
    }

    public async Task<IEnumerable<VectorPointResult>> ScrollAsync(
        string collectionName,
        Dictionary<string, object>? additionalFilter = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        using var activity = _metrics.ActivitySource.StartActivity("qdrant.vector.scroll", ActivityKind.Client);
        activity?.SetTag("db.system", "qdrant");
        activity?.SetTag("db.collection.name", collectionName);
        activity?.SetTag("aonik.vector.limit", limit);

        if (string.IsNullOrWhiteSpace(collectionName))
            throw new ArgumentException("Collection name required", nameof(collectionName));
        if (limit <= 0)
            throw new ArgumentException("Limit must be > 0", nameof(limit));

        var filter = BuildMergedFilter(additionalFilter);

        try
        {
            var page = await _httpClient.ScrollAsync(
                collectionName,
                filter,
                limit,
                cancellationToken: cancellationToken);

            var results = (page.Points ?? new List<QdrantScrollPoint>())
                .Select(p => new VectorPointResult(p.Id, p.Payload))
                .ToList();

            activity?.SetTag("aonik.vector.result_count", results.Count);

            _logger.LogDebug(
                "Scroll in collection {Collection} returned {Count} results",
                collectionName,
                results.Count);

            return results;
        }
        catch (Exception ex)
        {
            AiTelemetry.MarkError(activity, ex);
            _logger.LogError(
                ex,
                "Scroll failed in collection {Collection}",
                collectionName);
            throw;
        }
    }

    public async Task<VectorScrollPage> ScrollPageAsync(
        string collectionName,
        Dictionary<string, object>? additionalFilter,
        int limit,
        string? offset,
        bool withPayload = true,
        CancellationToken cancellationToken = default)
    {
        using var activity = _metrics.ActivitySource.StartActivity("qdrant.vector.scroll", ActivityKind.Client);
        activity?.SetTag("db.system", "qdrant");
        activity?.SetTag("db.collection.name", collectionName);
        activity?.SetTag("aonik.vector.limit", limit);

        if (string.IsNullOrWhiteSpace(collectionName))
            throw new ArgumentException("Collection name required", nameof(collectionName));
        if (limit <= 0)
            throw new ArgumentException("Limit must be > 0", nameof(limit));

        // Merge tenant isolation — fail-closed: throws if no tenant context.
        var filter = BuildMergedFilter(additionalFilter);

        try
        {
            var page = await _httpClient.ScrollAsync(
                collectionName,
                filter,
                limit,
                offset,
                withPayload,
                cancellationToken);

            var points = (page.Points ?? new List<QdrantScrollPoint>())
                .Select(p => new VectorPointResult(p.Id, p.Payload))
                .ToList();

            activity?.SetTag("aonik.vector.result_count", points.Count);

            return new VectorScrollPage(points, page.NextPageOffset);
        }
        catch (System.Net.Http.HttpRequestException ex)
            when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // The collection does not exist yet. This is expected for the first-ever document
            // indexed into a collection: the ingestion pipeline purges (scrolls + deletes) a
            // document's existing chunks BEFORE upserting, and it is the first upsert that lazily
            // creates the collection (see EnsureCollectionExistsAsync, called only on upsert). A
            // scroll over a not-yet-created collection is logically empty, not an error — return an
            // empty page so the caller proceeds to the upsert that creates the collection.
            _logger.LogDebug(
                "Scroll on collection {Collection} returned 404 (collection not yet created); treating as empty.",
                collectionName);
            return new VectorScrollPage(new List<VectorPointResult>(), null);
        }
        catch (Exception ex)
        {
            AiTelemetry.MarkError(activity, ex);
            _logger.LogError(
                ex,
                "Scroll page failed in collection {Collection}",
                collectionName);
            throw;
        }
    }

    public async Task SetPayloadAsync(
        string collectionName,
        IEnumerable<string> pointIds,
        Dictionary<string, object> payload,
        CancellationToken cancellationToken = default)
    {
        var pointIdList = pointIds as IReadOnlyCollection<string> ?? pointIds.ToList();
        using var activity = _metrics.ActivitySource.StartActivity("qdrant.vector.set_payload", ActivityKind.Client);
        activity?.SetTag("db.system", "qdrant");
        activity?.SetTag("db.collection.name", collectionName);
        activity?.SetTag("aonik.vector.point_count", pointIdList.Count);

        if (string.IsNullOrWhiteSpace(collectionName))
            throw new ArgumentException("Collection name required", nameof(collectionName));

        try
        {
            await _httpClient.SetPayloadAsync(
                collectionName,
                pointIdList,
                payload,
                cancellationToken);

            _logger.LogDebug(
                "Updated payload on points in collection {Collection}",
                collectionName);
        }
        catch (Exception ex)
        {
            AiTelemetry.MarkError(activity, ex);
            _logger.LogError(
                ex,
                "Failed to update payload in collection {Collection}",
                collectionName);
            throw;
        }
    }

    public async Task DeleteAsync(
        string collectionName,
        string? filterId = null,
        Dictionary<string, object>? filter = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(collectionName))
            throw new ArgumentException("Collection name required", nameof(collectionName));

        try
        {
            var pointIds = new List<string>();
            if (!string.IsNullOrWhiteSpace(filterId))
            {
                pointIds.Add(filterId);
            }

            if (pointIds.Any())
            {
                await _httpClient.DeletePointsAsync(
                    collectionName,
                    pointIds,
                    cancellationToken);

                _logger.LogDebug(
                    "Deleted {Count} vectors from collection {Collection}",
                    pointIds.Count,
                    collectionName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to delete from collection {Collection}",
                collectionName);
            throw;
        }
    }

    public async Task<bool> HealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var healthy = await _httpClient.HealthAsync(cancellationToken);
            if (!healthy)
            {
                _logger.LogWarning("Qdrant health check failed");
            }
            return healthy;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Qdrant health check threw exception");
            return false;
        }
    }

    /// <summary>
    /// Ensure collection exists, creating it if necessary.
    /// Uses in-memory cache to avoid repeated HTTP calls.
    /// </summary>
    private async Task EnsureCollectionExistsAsync(
        string collectionName,
        CancellationToken cancellationToken)
    {
        if (_knownCollections.ContainsKey(collectionName))
            return;

        var exists = await _httpClient.CollectionExistsAsync(collectionName, cancellationToken);
        if (!exists)
        {
            _logger.LogInformation("Creating collection {Collection}", collectionName);
            await _httpClient.CreateCollectionAsync(collectionName, cancellationToken);
        }

        _knownCollections.TryAdd(collectionName, true);
    }

    /// <summary>
    /// Add tenant isolation to payload. Fail-closed: throws if no tenant context.
    /// </summary>
    private Dictionary<string, object> EnhancePayloadWithTenant(
        Dictionary<string, object>? payload)
    {
        var enhanced = new Dictionary<string, object>(payload ?? new Dictionary<string, object>());

        if (!_tenantProvider.TryGetCurrentTenantId(out var tenantId))
        {
            throw new InvalidOperationException(
                "Tenant context is required for vector store operations. " +
                "Cannot upsert vectors without tenant isolation.");
        }

        enhanced["tenant_id"] = tenantId.ToString();
        return enhanced;
    }

    /// <summary>
    /// Build search filter for current tenant. Fail-closed: throws if no tenant context.
    /// </summary>
    private Dictionary<string, object> BuildTenantFilter()
        => BuildMergedFilter(additionalFilter: null);

    /// <summary>
    /// Build a filter that always includes the tenant isolation clause, and optionally
    /// merges additional <c>must</c> and <c>must_not</c> clauses from the caller.
    /// Fail-closed: throws if no tenant context.
    /// </summary>
    private Dictionary<string, object> BuildMergedFilter(Dictionary<string, object>? additionalFilter)
    {
        if (!_tenantProvider.TryGetCurrentTenantId(out var tenantId))
        {
            throw new InvalidOperationException(
                "Tenant context is required for vector store operations. " +
                "Cannot operate on vectors without tenant isolation.");
        }

        var tenantClause = new Dictionary<string, object>
        {
            { "key", "tenant_id" },
            { "match", new Dictionary<string, object> { { "value", tenantId.ToString() } } }
        };

        // Start must array with tenant clause
        var mustClauses = new List<object> { tenantClause };

        // Merge additional must clauses
        if (additionalFilter?.TryGetValue("must", out var extraMust) == true
            && extraMust is IEnumerable<object> extraMustArray)
        {
            mustClauses.AddRange(extraMustArray);
        }

        var filter = new Dictionary<string, object>
        {
            { "must", mustClauses.ToArray() }
        };

        // Pass through must_not clauses if present
        if (additionalFilter?.TryGetValue("must_not", out var mustNot) == true)
        {
            filter["must_not"] = mustNot;
        }

        return filter;
    }
}
