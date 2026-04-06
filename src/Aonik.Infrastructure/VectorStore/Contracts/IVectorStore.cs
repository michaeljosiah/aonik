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
