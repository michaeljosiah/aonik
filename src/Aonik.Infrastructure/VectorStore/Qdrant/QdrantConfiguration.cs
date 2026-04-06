namespace Aonik.Infrastructure.VectorStore.Qdrant;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Qdrant vector store configuration from appsettings.
/// </summary>
public class QdrantConfiguration : IValidatableObject
{
    /// <summary>
    /// Qdrant HTTP API endpoint (e.g., "http://localhost:6333").
    /// </summary>
    [Required]
    public string Endpoint { get; set; } = "http://localhost:6333";

    /// <summary>
    /// Qdrant API key for authentication.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Prefix for collection names (e.g., "aonik-dev").
    /// Used to isolate collections by environment.
    /// </summary>
    [Required]
    public string CollectionPrefix { get; set; } = "aonik";

    /// <summary>
    /// Vector dimensions (must match embedding model dimensions).
    /// Default: 1536 for OpenAI text-embedding-3-small.
    /// </summary>
    [Range(1, 65536)]
    public int VectorDimensions { get; set; } = 1536;

    /// <summary>
    /// Embedding model name (e.g., "text-embedding-3-small").
    /// Used for informational purposes and validation.
    /// </summary>
    [Required]
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";

    /// <summary>
    /// HTTP request timeout in seconds.
    /// </summary>
    [Range(1, 300)]
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

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out _))
        {
            yield return new ValidationResult(
                "Endpoint must be a valid absolute URI",
                new[] { nameof(Endpoint) });
        }
    }
}
