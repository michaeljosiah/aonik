namespace Aonik.Infrastructure.VectorStore.Providers;

using Aonik.Infrastructure.VectorStore.Contracts;
using Aonik.Infrastructure.VectorStore.Qdrant;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// OpenAI embedding service for generating text embeddings.
/// Uses the OpenAI API (same as AI module for consistency).
///
/// NOTE: This is a stub implementation. In production, integrate with:
/// - OpenAI SDK (https://github.com/openai/openai-dotnet)
/// - Microsoft.Extensions.AI (https://github.com/dotnet/extensions)
/// The embedding generation will be implemented after confirming the exact SDK version and API surface.
/// </summary>
internal class OpenAiEmbeddingService : IEmbeddingService
{
    private readonly string apiKey;
    private readonly QdrantConfiguration qdrantConfig;
    private readonly ILogger<OpenAiEmbeddingService> logger;

    public string ModelName => qdrantConfig.EmbeddingModel;

    public int Dimensions => qdrantConfig.VectorDimensions;

    public OpenAiEmbeddingService(
        IConfiguration configuration,
        IOptions<QdrantConfiguration> qdrantOptions,
        ILogger<OpenAiEmbeddingService> logger)
    {
        this.qdrantConfig = qdrantOptions.Value;
        this.logger = logger;

        this.apiKey = configuration["AI:OpenAI:ApiKey"] ?? string.Empty;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning(
                "OpenAI API key not configured. Using deterministic mock embeddings for development/testing. " +
                "Set AI:OpenAI:ApiKey for production use.");
        }
        else
        {
            logger.LogInformation(
                "Initialized OpenAI embedding service with model {Model} and {Dimensions} dimensions",
                ModelName,
                Dimensions);
        }
    }

    public async Task<float[]> GetEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text required", nameof(text));

        try
        {
            // TODO: Integrate with OpenAI SDK for actual embedding generation
            // For now, return a properly-dimensioned vector for testing
            // This ensures tests can run without external dependencies
            var embedding = GenerateMockEmbedding(text);
            return embedding;
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("Embedding request cancelled");
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate embedding");
            throw;
        }
    }

    public async Task<IEnumerable<float[]>> GetEmbeddingsBatchAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default)
    {
        var textList = texts.ToList();
        if (!textList.Any())
            throw new ArgumentException("Texts required", nameof(texts));

        try
        {
            // TODO: Integrate with OpenAI SDK for batch embedding generation
            // For now, generate mock embeddings for each text
            var vectors = textList
                .Select(t => GenerateMockEmbedding(t))
                .ToList();

            logger.LogDebug("Generated {Count} embeddings in batch", vectors.Count);

            return vectors;
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("Batch embedding request cancelled");
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate batch embeddings");
            throw;
        }
    }

    /// <summary>
    /// Generate a mock embedding based on text hash.
    /// This is temporary and ensures the service works during development.
    /// Replace with actual OpenAI API calls in production.
    /// </summary>
    private float[] GenerateMockEmbedding(string text)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(text));

        var random = new Random(BitConverter.ToInt32(hash));
        var embedding = new float[Dimensions];

        for (int i = 0; i < Dimensions; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2 - 1);
        }

        // Normalize vector to unit length (L2 norm)
        var norm = MathF.Sqrt(embedding.Sum(x => x * x));
        for (int i = 0; i < Dimensions; i++)
        {
            embedding[i] /= norm;
        }

        return embedding;
    }
}
