namespace Aonik.Infrastructure.VectorStore.Contracts;

/// <summary>
/// Vector store abstraction for storing and retrieving embeddings.
/// Enables RAG (retrieval-augmented generation) for AI agents.
/// </summary>
public interface IVectorStore
{
    /// <summary>
    /// Upsert (insert or update) a vector into a collection with optional metadata.
    /// </summary>
    /// <param name="collectionName">Collection name (e.g., "aonik-dev-documents")</param>
    /// <param name="vectorId">Unique identifier for the vector</param>
    /// <param name="embedding">Vector embedding (float array)</param>
    /// <param name="payload">Optional metadata attached to the vector</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UpsertVectorAsync(
        string collectionName,
        string vectorId,
        float[] embedding,
        Dictionary<string, object>? payload = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Search for similar vectors in a collection.
    /// </summary>
    /// <param name="collectionName">Collection name</param>
    /// <param name="queryEmbedding">Query vector embedding</param>
    /// <param name="limit">Maximum number of results to return</param>
    /// <param name="scoreThreshold">Minimum similarity score (0-1)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Ordered list of search results with scores and metadata</returns>
    Task<IEnumerable<VectorSearchResult>> SearchAsync(
        string collectionName,
        float[] queryEmbedding,
        int limit = 10,
        float scoreThreshold = 0.5f,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Search for similar vectors in a collection with additional payload filter constraints.
    /// </summary>
    /// <param name="collectionName">Collection name</param>
    /// <param name="queryEmbedding">Query vector embedding</param>
    /// <param name="limit">Maximum number of results to return</param>
    /// <param name="scoreThreshold">Minimum similarity score (0-1)</param>
    /// <param name="additionalFilter">Additional payload filter constraints merged with tenant isolation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Ordered list of search results with scores and metadata</returns>
    Task<IEnumerable<VectorSearchResult>> SearchAsync(
        string collectionName,
        float[] queryEmbedding,
        int limit,
        float scoreThreshold,
        Dictionary<string, object>? additionalFilter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieve points by payload filter (no vector similarity).
    /// Returns point IDs and payloads matching the filter, scoped to the current tenant.
    /// </summary>
    /// <param name="collectionName">Collection name</param>
    /// <param name="additionalFilter">Additional payload filter constraints merged with tenant isolation</param>
    /// <param name="limit">Maximum number of results to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<IEnumerable<VectorPointResult>> ScrollAsync(
        string collectionName,
        Dictionary<string, object>? additionalFilter = null,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update payload fields on existing points without re-uploading vectors.
    /// </summary>
    /// <param name="collectionName">Collection name</param>
    /// <param name="pointIds">IDs of points to update</param>
    /// <param name="payload">Payload fields to set or update</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SetPayloadAsync(
        string collectionName,
        IEnumerable<string> pointIds,
        Dictionary<string, object> payload,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete vector(s) from a collection.
    /// </summary>
    /// <param name="collectionName">Collection name</param>
    /// <param name="filterId">Vector ID to delete, or null to delete all matching filter</param>
    /// <param name="filter">Optional filter predicate on payload</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteAsync(
        string collectionName,
        string? filterId = null,
        Dictionary<string, object>? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check vector store health.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if healthy</returns>
    Task<bool> HealthAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a vector search query.
/// </summary>
public record VectorSearchResult(
    string Id,
    float Score,
    Dictionary<string, object>? Payload = null);

/// <summary>
/// Result of a scroll (filter-based retrieval, no similarity score).
/// </summary>
public record VectorPointResult(
    string Id,
    Dictionary<string, object>? Payload = null);
