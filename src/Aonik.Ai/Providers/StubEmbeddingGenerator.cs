using Microsoft.Extensions.AI;

namespace Aonik.Ai.Providers;

/// <summary>
/// Stub implementation of <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> that returns
/// deterministic mock embeddings based on text hashing. Used when <c>AI:Provider</c> is "Stub".
/// In production, this is replaced by the OpenAI embedding generator.
/// </summary>
internal sealed class StubEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly string _modelId;
    private readonly int _dimensions;

    public StubEmbeddingGenerator(string modelId, int dimensions)
    {
        _modelId = modelId;
        _dimensions = dimensions;
    }

    public EmbeddingGeneratorMetadata Metadata =>
        new("StubEmbeddingGenerator", defaultModelId: _modelId, defaultModelDimensions: _dimensions);

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var embeddings = new GeneratedEmbeddings<Embedding<float>>();
        foreach (var text in values)
        {
            embeddings.Add(new Embedding<float>(GenerateMockVector(text)));
        }

        return Task.FromResult(embeddings);
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
        => serviceType == typeof(StubEmbeddingGenerator) ? this : null;

    public void Dispose() { }

    /// <summary>
    /// Generate a deterministic mock vector from a text hash.
    /// Same algorithm as the original OpenAiEmbeddingService mock.
    /// </summary>
    private float[] GenerateMockVector(string text)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(text));

        var random = new Random(BitConverter.ToInt32(hash));
        var vector = new float[_dimensions];

        for (int i = 0; i < _dimensions; i++)
        {
            vector[i] = (float)(random.NextDouble() * 2 - 1);
        }

        // Normalize to unit length (L2 norm)
        var norm = MathF.Sqrt(vector.Sum(x => x * x));
        if (norm > 0)
        {
            for (int i = 0; i < _dimensions; i++)
            {
                vector[i] /= norm;
            }
        }

        return vector;
    }
}
