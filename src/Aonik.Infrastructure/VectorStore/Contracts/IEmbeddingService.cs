namespace Aonik.Infrastructure.VectorStore.Contracts;

/// <summary>
/// Embedding service for generating vector embeddings from text.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Get embedding model name.
    /// </summary>
    string ModelName { get; }

    /// <summary>
    /// Generate embedding for a single text input.
    /// </summary>
    /// <param name="text">Text to embed</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Vector embedding (float array)</returns>
    Task<float[]> GetEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate embeddings for multiple text inputs in batch.
    /// </summary>
    /// <param name="texts">Texts to embed</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of vector embeddings</returns>
    Task<IEnumerable<float[]>> GetEmbeddingsBatchAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get dimensions of the embedding vector.
    /// </summary>
    int Dimensions { get; }
}
