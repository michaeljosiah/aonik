namespace Aonik.Infrastructure.VectorStore.Qdrant;

/// <summary>
/// Qdrant vector store configuration from appsettings.
/// </summary>
public class QdrantConfiguration
{
    /// <summary>
    /// Qdrant HTTP API endpoint (e.g., "http://localhost:6333").
    /// </summary>
    public string Endpoint { get; set; } = "http://localhost:6333";

    /// <summary>
    /// Qdrant API key for authentication.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Prefix for collection names (e.g., "aonik-dev").
    /// Used to isolate collections by environment.
    /// </summary>
    public string CollectionPrefix { get; set; } = "aonik";

    /// <summary>
    /// Vector dimensions (must match embedding model dimensions).
    /// Default: 1536 for OpenAI text-embedding-3-small.
    /// </summary>
    public int VectorDimensions { get; set; } = 1536;

    /// <summary>
    /// Embedding model name (e.g., "text-embedding-3-small").
    /// Used for informational purposes and validation.
    /// </summary>
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";

    /// <summary>
    /// HTTP request timeout in seconds.
    /// </summary>
    public int Timeout { get; set; } = 30;

    /// <summary>
    /// Enable OpenTelemetry metrics and traces.
    /// </summary>
    public bool EnableTelemetry { get; set; } = true;

    /// <summary>
    /// Get fully qualified collection name with environment prefix.
    /// </summary>
    public string GetCollectionName(string collectionType) =>
        $"{CollectionPrefix}-{collectionType}";
}
