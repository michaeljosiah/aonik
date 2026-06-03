namespace Aonik.Application.Tests.Documents;

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aonik.Documents.Persistence;
using Aonik.Documents.Services;
using Aonik.Platform.Entities.Compliance; // Document/DocumentFile — namespace preserved (Spec 035)
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Events.Outbox;
using Aonik.TestSupport.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

/// <summary>
/// Covers the ingestion orchestration (Spec 035 §13) over an InMemory <see cref="DocumentsDbContext"/>
/// with mocked SharedKernel collaborators: the happy path (Indexed + audit row + DocumentIndexedEvent),
/// graceful OCR deferral, fail-and-rethrow (so the outbox retries), and the defensive non-indexable skip.
/// </summary>
public sealed class DocumentIndexerTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _partyId = Guid.NewGuid();
    private readonly Guid _aiRunId = Guid.NewGuid();

    private readonly Mock<IDocumentFileStore> _fileStore = new();
    private readonly Mock<IDocumentTextExtractor> _extractor = new();
    private readonly Mock<IDocumentVectorIndex> _vectorIndex = new();
    private readonly Mock<IAiRunWriter> _aiRunWriter = new();
    private readonly Mock<IClock> _clock = new();

    public DocumentIndexerTests()
    {
        _clock.Setup(c => c.UtcNow).Returns(new DateTime(2026, 6, 3, 12, 0, 0, DateTimeKind.Utc));
        _aiRunWriter
            .Setup(w => w.StartRunAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_aiRunId);
        _fileStore
            .Setup(s => s.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(new byte[] { 1, 2, 3 }));
    }

    private DocumentsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseInMemoryDatabase($"DocsIngest_{Guid.NewGuid()}")
            .Options;
        return new DocumentsDbContext(options, new TestTenantProvider(_tenantId));
    }

    private DocumentIndexer CreateIndexer(DocumentsDbContext context) => new(
        context, _fileStore.Object, _extractor.Object, _vectorIndex.Object,
        _aiRunWriter.Object, _clock.Object, NullLogger<DocumentIndexer>.Instance);

    private async Task<(Guid DocumentId, Guid FileId)> SeedAsync(
        DocumentsDbContext context,
        DocumentClassification classification = DocumentClassification.Personal,
        DocumentIndexStatus indexStatus = DocumentIndexStatus.Pending)
    {
        var documentId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        context.Documents.Add(new Document
        {
            Id = documentId,
            TenantId = _tenantId,
            OwnerPartyId = _partyId,
            DocumentType = "tax_return",
            Status = "Submitted",
            Classification = classification,
            IndexStatus = indexStatus,
        });
        context.DocumentFiles.Add(new DocumentFile
        {
            Id = fileId,
            TenantId = _tenantId,
            DocumentId = documentId,
            StorageProvider = "azure",
            StorageKey = "tenants/x/y/z.txt",
            ContentType = "text/plain",
            FileName = "tax.txt",
            ExtractedTextStatus = ExtractedTextStatus.Native,
        });
        await context.SaveChangesAsync();
        return (documentId, fileId);
    }

    [Fact]
    public async Task IngestAsync_Should_Index_And_Record_Audit_On_Success()
    {
        await using var context = CreateContext();
        var (documentId, fileId) = await SeedAsync(context);
        _extractor
            .Setup(e => e.ExtractTextAsync(It.IsAny<Stream>(), "text/plain", It.IsAny<CancellationToken>()))
            .ReturnsAsync(DocumentTextExtractionResult.Native("the quick brown fox jumps over the lazy dog"));
        _vectorIndex
            .Setup(i => i.IndexDocumentAsync(It.IsAny<DocumentIndexRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentIndexResult(2, "text-embedding-3-small", 0.0005m));

        await CreateIndexer(context).IngestAsync(documentId, fileId);

        var document = await context.Documents.FirstAsync(d => d.Id == documentId);
        document.IndexStatus.Should().Be(DocumentIndexStatus.Indexed);
        document.IndexedAt.Should().NotBeNull();

        var ingestion = await context.DocumentIngestions.FirstAsync(i => i.DocumentFileId == fileId);
        ingestion.Status.Should().Be("Succeeded");
        ingestion.ChunkCount.Should().Be(2);
        ingestion.EmbeddingModel.Should().Be("text-embedding-3-small");
        ingestion.EmbeddingCost.Should().Be(0.0005m);
        ingestion.AiRunId.Should().Be(_aiRunId);
        ingestion.Attempts.Should().Be(1);

        context.Set<OutboxMessage>().Should()
            .ContainSingle(m => m.EventType.Contains("DocumentIndexedEvent"),
                "consumers react to a document becoming searchable");
        _aiRunWriter.Verify(
            w => w.MarkRunCompletedWithMetricsAsync(
                _aiRunId, It.IsAny<int>(), It.IsAny<int>(), 0.0005m, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task IngestAsync_Should_Pass_Owner_And_Classification_Scope_To_The_Index()
    {
        await using var context = CreateContext();
        var (documentId, fileId) = await SeedAsync(context);
        DocumentIndexRequest? captured = null;
        _extractor
            .Setup(e => e.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DocumentTextExtractionResult.Native("alpha beta gamma delta"));
        _vectorIndex
            .Setup(i => i.IndexDocumentAsync(It.IsAny<DocumentIndexRequest>(), It.IsAny<CancellationToken>()))
            .Callback<DocumentIndexRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(new DocumentIndexResult(1, "text-embedding-3-small", 0.0001m));

        await CreateIndexer(context).IngestAsync(documentId, fileId);

        captured.Should().NotBeNull();
        captured!.OwnerPartyId.Should().Be(_partyId, "the party scope is derived from the document, not model input");
        captured.Classification.Should().Be(DocumentClassification.Personal);
        captured.DocumentId.Should().Be(documentId);
        captured.Chunks.Should().NotBeEmpty();
    }

    [Fact]
    public async Task IngestAsync_Should_Defer_When_No_Embeddable_Text()
    {
        await using var context = CreateContext();
        var (documentId, fileId) = await SeedAsync(context);
        _extractor
            .Setup(e => e.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DocumentTextExtractionResult.OcrRequired());

        await CreateIndexer(context).IngestAsync(documentId, fileId);

        var document = await context.Documents.FirstAsync(d => d.Id == documentId);
        document.IndexStatus.Should().Be(DocumentIndexStatus.Pending,
            "an OCR-deferred document stays pending for a later backfill, not failed");

        var ingestion = await context.DocumentIngestions.FirstAsync(i => i.DocumentFileId == fileId);
        ingestion.Status.Should().Be("Skipped");
        _vectorIndex.Verify(
            i => i.IndexDocumentAsync(It.IsAny<DocumentIndexRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        context.Set<OutboxMessage>().Should().NotContain(m => m.EventType.Contains("DocumentIndexedEvent"));
    }

    [Fact]
    public async Task IngestAsync_Should_Mark_Failed_And_Rethrow_When_Indexing_Throws()
    {
        await using var context = CreateContext();
        var (documentId, fileId) = await SeedAsync(context);
        _extractor
            .Setup(e => e.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DocumentTextExtractionResult.Native("some text here to chunk and embed"));
        _vectorIndex
            .Setup(i => i.IndexDocumentAsync(It.IsAny<DocumentIndexRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("qdrant unavailable"));

        var act = async () => await CreateIndexer(context).IngestAsync(documentId, fileId);

        await act.Should().ThrowAsync<InvalidOperationException>(
            "rethrowing drives the outbox retry/back-off/dead-letter");

        var document = await context.Documents.FirstAsync(d => d.Id == documentId);
        document.IndexStatus.Should().Be(DocumentIndexStatus.Failed);

        var ingestion = await context.DocumentIngestions.FirstAsync(i => i.DocumentFileId == fileId);
        ingestion.Status.Should().Be("Failed");
        ingestion.LastError.Should().Contain("qdrant unavailable");
        _aiRunWriter.Verify(
            w => w.MarkRunFailedAsync(_aiRunId, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task IngestAsync_Should_Skip_NonIndexable_Document_Defensively()
    {
        await using var context = CreateContext();
        var (documentId, fileId) = await SeedAsync(
            context, DocumentClassification.Sensitive, DocumentIndexStatus.NotIndexable);

        await CreateIndexer(context).IngestAsync(documentId, fileId);

        _aiRunWriter.Verify(
            w => w.StartRunAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never, "a non-indexable document must never start an embedding run");
        _vectorIndex.Verify(
            i => i.IndexDocumentAsync(It.IsAny<DocumentIndexRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        (await context.DocumentIngestions.AnyAsync()).Should().BeFalse();
    }
}
