namespace Aonik.Infrastructure.Tests.VectorStore.Fixtures;

using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Aonik.Infrastructure.VectorStore.Contracts;

/// <summary>
/// Deterministic test embedding service for unit tests.
/// Generates consistent vectors based on text hash for reproducible tests.
/// </summary>
internal sealed class TestEmbeddingService : IEmbeddingService
{
    private const int DefaultDimensions = 1536;
    private readonly int dimensions;

    public TestEmbeddingService(int dimensions = DefaultDimensions)
    {
        this.dimensions = dimensions;
    }

    public string ModelName => "test-embedding-model";

    public int Dimensions => dimensions;

    public async Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return GenerateEmbedding(text);
    }

    public async Task<IEnumerable<float[]>> GetEmbeddingsBatchAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return texts.Select(GenerateEmbedding).ToList();
    }

    public float[] GenerateEmbedding(string text)
    {
        // Use SHA256 hash as seed for deterministic pseudo-random vectors
        using (var sha = SHA256.Create())
        {
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
            var seed = BitConverter.ToInt32(hash, 0);
            var random = new Random(seed);

            var vector = new float[dimensions];
            for (int i = 0; i < dimensions; i++)
            {
                vector[i] = (float)random.NextGaussian();
            }

            // Normalize to unit vector
            float magnitude = (float)Math.Sqrt(vector.Sum(v => v * v));
            if (magnitude > 0)
            {
                for (int i = 0; i < dimensions; i++)
                {
                    vector[i] /= magnitude;
                }
            }

            return vector;
        }
    }
}

/// <summary>
/// Extension to Random for Gaussian distribution sampling.
/// </summary>
internal static class RandomExtensions
{
    public static double NextGaussian(this Random random)
    {
        // Box-Muller transform
        double u1 = random.NextDouble();
        double u2 = random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }
}
