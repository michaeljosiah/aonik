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
    private readonly IVectorStore _vectorStore;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<RagContextProvider> _logger;
    private const string DefaultCollectionPrefix = "aonik";

    public RagContextProvider(
        IVectorStore vectorStore,
        IEmbeddingService embeddingService,
        ILogger<RagContextProvider> logger)
    {
        _vectorStore = vectorStore;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    /// <summary>
    /// Get RAG context for a query.
    /// Embeds the query, searches for similar vectors, and returns assembled context string.
    /// </summary>
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
            var queryEmbedding = await _embeddingService.GetEmbeddingAsync(query, cancellationToken);

            var collectionName = $"{DefaultCollectionPrefix}-{collectionType}";
            var results = await _vectorStore.SearchAsync(
                collectionName,
                queryEmbedding,
                limit: topK,
                scoreThreshold: scoreThreshold,
                cancellationToken: cancellationToken);

            var resultList = results.ToList();
            if (!resultList.Any())
            {
                _logger.LogDebug(
                    "No RAG context found for query in collection {Collection}",
                    collectionName);
                return string.Empty;
            }

            var contextParts = resultList
                .Select(r => ExtractContent(r.Payload))
                .Where(c => !string.IsNullOrEmpty(c))
                .Select((c, i) => $"[Score: {resultList[i].Score:F2}] {c}");

            var context = string.Join("\n\n", contextParts);

            _logger.LogDebug(
                "Retrieved {Count} RAG context chunks (total {Length} chars) for query",
                resultList.Count,
                context.Length);

            return context;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve RAG context for query");
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
            var queryEmbedding = await _embeddingService.GetEmbeddingAsync(query, cancellationToken);
            var collectionName = $"{DefaultCollectionPrefix}-{collectionType}";
            var results = await _vectorStore.SearchAsync(
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
            _logger.LogError(ex, "Failed to retrieve RAG context with metadata");
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
