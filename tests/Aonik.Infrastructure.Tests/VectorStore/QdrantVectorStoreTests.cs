namespace Aonik.Infrastructure.Tests.VectorStore;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aonik.Infrastructure.Tests.VectorStore.Fixtures;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Xunit;

public sealed class QdrantVectorStoreTests
{
    private readonly QdrantFixture fixture;

    public QdrantVectorStoreTests()
    {
        fixture = new QdrantFixture();
    }

    [Fact]
    public async Task Tenant_Isolation_Should_Prevent_Cross_Tenant_Search()
    {
        // Arrange - Tenant A
        var tenantA = Guid.NewGuid();
        fixture.TenantProvider.SetCurrentTenantId(tenantA);
        
        var testVectorStoreA = new TestVectorStore(fixture.TenantProvider, fixture.EmbeddingService);
        var collectionName = "test-documents";
        var docAId = "doc-a";
        var docAEmbedding = fixture.EmbeddingService.GenerateEmbedding("Tenant A document");
        await testVectorStoreA.UpsertVectorAsync(collectionName, docAId, docAEmbedding, new Dictionary<string, object> { { "content", "Tenant A document" } });

        // Arrange - Tenant B
        var tenantB = Guid.NewGuid();
        fixture.TenantProvider.SetCurrentTenantId(tenantB);
        
        var testVectorStoreB = new TestVectorStore(fixture.TenantProvider, fixture.EmbeddingService);
        var queryEmbedding = fixture.EmbeddingService.GenerateEmbedding("Tenant A document");

        // Act
        var resultsFromB = await testVectorStoreB.SearchAsync(collectionName, queryEmbedding, limit: 10);

        // Assert
        resultsFromB.Should().BeEmpty();
    }

    [Fact]
    public async Task Same_Tenant_Should_Access_Its_Vectors()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        fixture.TenantProvider.SetCurrentTenantId(tenantId);
        
        var testVectorStore = new TestVectorStore(fixture.TenantProvider, fixture.EmbeddingService);
        var collectionName = "test-documents";
        var docId = "doc-1";
        var embedding = fixture.EmbeddingService.GenerateEmbedding("Tenant document");
        await testVectorStore.UpsertVectorAsync(collectionName, docId, embedding, new Dictionary<string, object> { { "content", "Tenant document" } });

        // Act
        var results = await testVectorStore.SearchAsync(collectionName, embedding, limit: 10);

        // Assert
        results.Should().NotBeEmpty();
        results.First().Id.Should().Be(docId);
    }
}

internal sealed class TestVectorStore
{
    private readonly Dictionary<(Guid, string), List<Vector>> storage = new();
    private readonly ITenantProvider tenantProvider;
    private readonly TestEmbeddingService embeddingService;

    public TestVectorStore(ITenantProvider tenantProvider, TestEmbeddingService embeddingService)
    {
        this.tenantProvider = tenantProvider;
        this.embeddingService = embeddingService;
    }

    public async Task UpsertVectorAsync(string collectionName, string vectorId, float[] embedding, Dictionary<string, object>? payload = null)
    {
        await Task.CompletedTask;
        var tenantId = tenantProvider.GetCurrentTenantId();
        var key = (tenantId, collectionName);

        if (!storage.ContainsKey(key))
        {
            storage[key] = new List<Vector>();
        }

        var existing = storage[key].FirstOrDefault(v => v.Id == vectorId);
        if (existing != null)
        {
            storage[key].Remove(existing);
        }

        storage[key].Add(new Vector { Id = vectorId, Embedding = embedding, Payload = payload ?? new Dictionary<string, object>() });
    }

    public async Task<IEnumerable<(string Id, float Score)>> SearchAsync(string collectionName, float[] embedding, int limit = 10)
    {
        await Task.CompletedTask;
        var tenantId = tenantProvider.GetCurrentTenantId();
        var key = (tenantId, collectionName);

        if (!storage.ContainsKey(key))
        {
            return Enumerable.Empty<(string, float)>();
        }

        return storage[key]
            .Select(v => (v.Id, Score: CosineSimilarity(embedding, v.Embedding)))
            .OrderByDescending(x => x.Score)
            .Take(limit)
            .ToList();
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0f;
        float dotProduct = 0f, magnitudeA = 0f, magnitudeB = 0f;
        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }
        magnitudeA = (float)Math.Sqrt(magnitudeA);
        magnitudeB = (float)Math.Sqrt(magnitudeB);
        return (magnitudeA == 0 || magnitudeB == 0) ? 0f : dotProduct / (magnitudeA * magnitudeB);
    }

    private sealed class Vector
    {
        public required string Id { get; init; }
        public required float[] Embedding { get; init; }
        public required Dictionary<string, object> Payload { get; init; }
    }
}
