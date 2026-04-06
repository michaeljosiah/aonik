namespace Aonik.Agents.Framework;

using Microsoft.Extensions.Logging;

/// <summary>
/// Vector store abstraction used by RagContextProvider.
/// </summary>
public interface IVectorStore
{
    Task<IEnumerable<VectorSearchResult>> SearchAsync(
        string collectionName,
        float[] queryEmbedding,
        int limit = 10,
        float scoreThreshold = 0.5f,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Embedding service abstraction used by RagContextProvider.
/// </summary>
public interface IEmbeddingService
{
    string ModelName { get; }
    int Dimensions { get; }
    Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default);
    Task<IEnumerable<float[]>> GetEmbeddingsBatchAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default);
}

/// <summary>
/// Vector search result.
/// </summary>
public record VectorSearchResult(string Id, float Score, Dictionary<string, object>? Payload = null);

/// <summary>
/// Provides RAG (retrieval-augmented generation) context for agents.
/// Retrieves relevant document chunks from the vector store based on user queries.
/// Agents use this to inject domain context before calling the LLM.
/// </summary>
public class RagContextProvider
{
    private readonly IVectorStore vectorStore;
    private readonly IEmbeddingService embeddingService;
    private readonly ILogger<RagContextProvider> logger;
    private const string DefaultCollectionPrefix = "aonik";

    public RagContextProvider(
        IVectorStore vectorStore,
        IEmbeddingService embeddingService,
        ILogger<RagContextProvider> logger)
    {
        this.vectorStore = vectorStore;
        this.embeddingService = embeddingService;
        this.logger = logger;
    }

    /// <summary>
    /// Get RAG context for a query.
    /// Embeds the query, searches for similar vectors, and returns assembled context string.
    /// </summary>
    /// <param name="query">Query text to find context for</param>
    /// <param name="collectionType">Collection type (e.g., "documents")</param>
    /// <param name="topK">Number of results to return</param>
    /// <param name="scoreThreshold">Minimum similarity score (0-1)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Context string assembled from search results, or empty string if no results</returns>
    public async Task<string> GetContextAsync(
        string query,
        string collectionType = "documents",
        int topK = 5,
        float scoreThreshold = 0.6f,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return string.Empty;

        try
        {
            // 1. Embed the query
            var queryEmbedding = await embeddingService.GetEmbeddingAsync(query, cancellationToken);

            // 2. Search for similar vectors
            var collectionName = $"{DefaultCollectionPrefix}-{collectionType}";
            var results = await vectorStore.SearchAsync(
                collectionName,
                queryEmbedding,
                limit: topK,
                scoreThreshold: scoreThreshold,
                cancellationToken: cancellationToken);

            var resultList = results.ToList();
            if (!resultList.Any())
            {
                logger.LogDebug(
                    "No RAG context found for query in collection {Collection}",
                    collectionName);
                return string.Empty;
            }

            // 3. Assemble context from results
            var contextParts = new List<string>();
            foreach (var result in resultList)
            {
                if (result.Payload?.TryGetValue("content", out var content) == true && content is string contentStr)
                {
                    contextParts.Add($"[Score: {result.Score:F2}] {contentStr}");
                }
                else if (result.Payload?.TryGetValue("text", out var text) == true && text is string textStr)
                {
                    contextParts.Add($"[Score: {result.Score:F2}] {textStr}");
                }
            }

            var context = string.Join("\n\n", contextParts);

            logger.LogDebug(
                "Retrieved {Count} RAG context chunks (total {Length} chars) for query",
                resultList.Count,
                context.Length);

            return context;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to retrieve RAG context for query");
            // Return empty context on error rather than throwing - allows graceful degradation
            return string.Empty;
        }
    }

    /// <summary>
    /// Get RAG context with full metadata for debugging or advanced use cases.
    /// </summary>
    public async Task<IEnumerable<RagContextResult>> GetContextWithMetadataAsync(
        string query,
        string collectionType = "documents",
        int topK = 5,
        float scoreThreshold = 0.6f,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Enumerable.Empty<RagContextResult>();

        try
        {
            var queryEmbedding = await embeddingService.GetEmbeddingAsync(query, cancellationToken);
            var collectionName = $"{DefaultCollectionPrefix}-{collectionType}";
            var results = await vectorStore.SearchAsync(
                collectionName,
                queryEmbedding,
                limit: topK,
                scoreThreshold: scoreThreshold,
                cancellationToken: cancellationToken);

            return results
                .Select(r => new RagContextResult(
                    r.Id,
                    r.Score,
                    ExtractContent(r.Payload),
                    r.Payload ?? new Dictionary<string, object>()))
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve RAG context with metadata");
            return Enumerable.Empty<RagContextResult>();
        }
    }

    private static string ExtractContent(Dictionary<string, object>? payload)
    {
        if (payload == null) return string.Empty;

        if (payload.TryGetValue("content", out var content) && content is string contentStr)
            return contentStr;
        if (payload.TryGetValue("text", out var text) && text is string textStr)
            return textStr;

        return string.Empty;
    }
}

/// <summary>
/// RAG context result with metadata.
/// </summary>
public record RagContextResult(
    string Id,
    float Score,
    string Content,
    Dictionary<string, object> Metadata);
