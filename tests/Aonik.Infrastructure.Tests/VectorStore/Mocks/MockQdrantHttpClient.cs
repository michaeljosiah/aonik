namespace Aonik.Infrastructure.Tests.VectorStore.Mocks;

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aonik.Infrastructure.VectorStore.Qdrant;

/// <summary>
/// In-memory mock of QdrantHttpClient for unit testing without a real Qdrant instance.
/// </summary>
internal sealed class MockQdrantHttpClient
{
    private readonly Dictionary<string, List<MockPoint>> collections = new();
    private bool healthy = true;

    public bool IsHealthy
    {
        get => healthy;
        set => healthy = value;
    }

    public async Task CreateCollectionAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        if (!collections.ContainsKey(collectionName))
        {
            collections[collectionName] = new List<MockPoint>();
        }
    }

    public async Task<bool> CollectionExistsAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return collections.ContainsKey(collectionName);
    }

    public async Task UpsertPointAsync(
        string collectionName,
        string pointId,
        float[] vector,
        Dictionary<string, object>? payload,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        if (!collections.TryGetValue(collectionName, out var collection))
        {
            collection = new List<MockPoint>();
            collections[collectionName] = collection;
        }

        var existing = collection.FirstOrDefault(p => p.Id == pointId);
        if (existing != null)
        {
            collection.Remove(existing);
        }

        collection.Add(new MockPoint
        {
            Id = pointId,
            Vector = vector,
            Payload = payload ?? new Dictionary<string, object>()
        });
    }

    public async Task<IEnumerable<QdrantSearchHit>> SearchAsync(
        string collectionName,
        float[] vector,
        int limit,
        float scoreThreshold,
        Dictionary<string, object>? filter = null,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        if (!collections.TryGetValue(collectionName, out var collection))
        {
            return Enumerable.Empty<QdrantSearchHit>();
        }

        // Simple cosine similarity scoring
        var results = collection
            .Select(point => new
            {
                Point = point,
                Score = CosineSimilarity(vector, point.Vector)
            })
            .Where(x => x.Score >= scoreThreshold)
            .OrderByDescending(x => x.Score)
            .Take(limit)
            .Select(x => new QdrantSearchHit(
                x.Point.Id,
                x.Score,
                x.Point.Payload))
            .ToList();

        return results;
    }

    public async Task DeletePointsAsync(string collectionName, IEnumerable<string> pointIds, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        if (collections.TryGetValue(collectionName, out var collection))
        {
            var idsToDelete = pointIds.ToHashSet();
            collections[collectionName] = collection.Where(p => !idsToDelete.Contains(p.Id)).ToList();
        }
    }

    public async Task<bool> HealthAsync(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return healthy;
    }

    public void Clear()
    {
        collections.Clear();
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            return 0f;

        float dotProduct = 0f;
        float magnitudeA = 0f;
        float magnitudeB = 0f;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        magnitudeA = (float)Math.Sqrt(magnitudeA);
        magnitudeB = (float)Math.Sqrt(magnitudeB);

        if (magnitudeA == 0 || magnitudeB == 0)
            return 0f;

        return dotProduct / (magnitudeA * magnitudeB);
    }

    private sealed class MockPoint
    {
        public required string Id { get; init; }
        public required float[] Vector { get; init; }
        public required Dictionary<string, object> Payload { get; init; }
    }
}
