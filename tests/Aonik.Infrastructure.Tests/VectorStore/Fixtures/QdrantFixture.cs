namespace Aonik.Infrastructure.Tests.VectorStore.Fixtures;

using System;
using Aonik.Infrastructure.VectorStore.Contracts;
using Aonik.Infrastructure.VectorStore.Qdrant;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.Extensions.Logging;

internal sealed class QdrantFixture : IDisposable
{
    public TestTenantProvider TenantProvider { get; }
    public TestEmbeddingService EmbeddingService { get; }
    public ILogger<QdrantVectorStore> Logger { get; }

    public QdrantFixture()
    {
        TenantProvider = new TestTenantProvider();
        EmbeddingService = new TestEmbeddingService();
        Logger = new NoOpLogger<QdrantVectorStore>();
    }

    public void Dispose()
    {
    }
}
