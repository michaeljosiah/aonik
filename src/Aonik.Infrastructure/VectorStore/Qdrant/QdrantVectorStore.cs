namespace Aonik.Infrastructure.VectorStore.Qdrant;

using Aonik.Infrastructure.VectorStore.Contracts;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Qdrant vector store implementation.
/// Provides RAG capabilities via vector search over embeddings.
/// </summary>
internal class QdrantVectorStore : IVectorStore
{
    private readonly QdrantHttpClient httpClient;
    private readonly QdrantConfiguration config;
    private readonly ITenantProvider tenantProvider;
    private readonly ILogger<QdrantVectorStore> logger;

    public QdrantVectorStore(
        QdrantHttpClient httpClient,
        IOptions<QdrantConfiguration> options,
        ITenantProvider tenantProvider,
        ILogger<QdrantVectorStore> logger)
    {
        this.httpClient = httpClient;
        this.config = options.Value;
        this.tenantProvider = tenantProvider;
        this.logger = logger;
    }

    public async Task UpsertVectorAsync(
        string collectionName,
        string vectorId,
        float[] embedding,
        Dictionary<string, object>? payload = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(collectionName))
            throw new ArgumentException("Collection name required", nameof(collectionName));
        if (string.IsNullOrWhiteSpace(vectorId))
            throw new ArgumentException("Vector ID required", nameof(vectorId));
        if (embedding == null || embedding.Length == 0)
            throw new ArgumentException("Embedding required", nameof(embedding));
        if (embedding.Length != config.VectorDimensions)
            throw new ArgumentException(
                $"Embedding dimensions {embedding.Length} do not match configured {config.VectorDimensions}",
                nameof(embedding));

        // Ensure collection exists
        await EnsureCollectionExistsAsync(collectionName, cancellationToken);

        // Add tenant isolation
        var enhancedPayload = EnhancePayloadWithTenant(payload);

        try
        {
            await httpClient.UpsertPointAsync(
                collectionName,
                vectorId,
                embedding,
                enhancedPayload,
                cancellationToken);

            logger.LogDebug(
                "Upserted vector {VectorId} to collection {Collection}",
                vectorId,
                collectionName);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to upsert vector {VectorId} to collection {Collection}",
                vectorId,
                collectionName);
            throw;
        }
    }

    public async Task<IEnumerable<VectorSearchResult>> SearchAsync(
        string collectionName,
        float[] queryEmbedding,
        int limit = 10,
        float scoreThreshold = 0.5f,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(collectionName))
            throw new ArgumentException("Collection name required", nameof(collectionName));
        if (queryEmbedding == null || queryEmbedding.Length == 0)
            throw new ArgumentException("Query embedding required", nameof(queryEmbedding));
        if (queryEmbedding.Length != config.VectorDimensions)
            throw new ArgumentException(
                $"Query embedding dimensions {queryEmbedding.Length} do not match configured {config.VectorDimensions}",
                nameof(queryEmbedding));
        if (limit <= 0)
            throw new ArgumentException("Limit must be > 0", nameof(limit));
        if (scoreThreshold < 0 || scoreThreshold > 1)
            throw new ArgumentException("Score threshold must be between 0 and 1", nameof(scoreThreshold));

        // Build tenant filter
        var filter = BuildTenantFilter();

        try
        {
            var hits = await httpClient.SearchAsync(
                collectionName,
                queryEmbedding,
                limit,
                scoreThreshold,
                filter,
                cancellationToken);

            var results = hits
                .Select(h => new VectorSearchResult(h.Id, h.Score, h.Payload))
                .ToList();

            logger.LogDebug(
                "Search in collection {Collection} returned {Count} results",
                collectionName,
                results.Count);

            return results;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Search failed in collection {Collection}",
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
                await httpClient.DeletePointsAsync(
                    collectionName,
                    pointIds,
                    cancellationToken);

                logger.LogDebug(
                    "Deleted {Count} vectors from collection {Collection}",
                    pointIds.Count,
                    collectionName);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(
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
            var healthy = await httpClient.HealthAsync(cancellationToken);
            if (!healthy)
            {
                logger.LogWarning("Qdrant health check failed");
            }
            return healthy;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Qdrant health check threw exception");
            return false;
        }
    }

    /// <summary>
    /// Ensure collection exists, creating it if necessary.
    /// </summary>
    private async Task EnsureCollectionExistsAsync(
        string collectionName,
        CancellationToken cancellationToken)
    {
        var exists = await httpClient.CollectionExistsAsync(collectionName, cancellationToken);
        if (!exists)
        {
            logger.LogInformation("Creating collection {Collection}", collectionName);
            await httpClient.CreateCollectionAsync(collectionName, cancellationToken);
        }
    }

    /// <summary>
    /// Add tenant isolation to payload.
    /// </summary>
    private Dictionary<string, object> EnhancePayloadWithTenant(
        Dictionary<string, object>? payload)
    {
        var enhanced = new Dictionary<string, object>(payload ?? new Dictionary<string, object>());

        try
        {
            if (tenantProvider.TryGetCurrentTenantId(out var tenantId))
            {
                enhanced["tenant_id"] = tenantId.ToString();
            }
        }
        catch
        {
            // If tenant context not available, continue without tenant isolation
            logger.LogDebug("Tenant context not available for vector isolation");
        }

        return enhanced;
    }

    /// <summary>
    /// Build search filter for current tenant.
    /// </summary>
    private Dictionary<string, object>? BuildTenantFilter()
    {
        try
        {
            if (!tenantProvider.TryGetCurrentTenantId(out var tenantId))
            {
                return null;
            }

            // Qdrant filter format for exact match on tenant_id
            return new Dictionary<string, object>
            {
                {
                    "must", new[]
                    {
                        new Dictionary<string, object>
                        {
                            { "key", "tenant_id" },
                            { "match", new Dictionary<string, object> { { "value", tenantId.ToString() } } }
                        }
                    }
                }
            };
        }
        catch
        {
            logger.LogDebug("Failed to build tenant filter");
            return null;
        }
    }
}
