namespace Aonik.Application.Tests.Documents;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aonik.Documents.Persistence;
using Aonik.Documents.Services;
using Aonik.Platform.Entities.Compliance; // Document/DocumentFile — namespace preserved (Spec 035)
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Events.Outbox;
using Aonik.SharedKernel.Persistence;
using Aonik.TestSupport.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

/// <summary>
/// The document erasure path (Spec 035 §15 / R8): purge vectors → remove blobs → soft-delete rows →
/// publish DocumentDeletedEvent. Verifies the privacy-first ordering's outcomes and that the rows
/// are soft-deleted (auditable), not hard-deleted.
/// </summary>
public sealed class DocumentServiceDeleteTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly TestTenantProvider _tenantProvider;
    private readonly Mock<IDocumentFileStore> _fileStore = new();
    private readonly Mock<IDocumentVectorIndex> _vectorIndex = new();

    public DocumentServiceDeleteTests() => _tenantProvider = new TestTenantProvider(_tenantId);

    private DocumentsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseInMemoryDatabase($"DocsDelete_{Guid.NewGuid()}")
            .Options;
        return new DocumentsDbContext(options, _tenantProvider);
    }

    private DocumentService CreateService(DocumentsDbContext context) =>
        new(context, _tenantProvider, _fileStore.Object, _vectorIndex.Object,
            Mock.Of<ICurrentUserContext>(), Mock.Of<IUserPartyResolver>());

    [Fact]
    public async Task DeleteDocumentAsync_Should_Purge_Vectors_Delete_Blobs_SoftDelete_And_Publish()
    {
        await using var context = CreateContext();
        var documentId = Guid.NewGuid();
        var ownerPartyId = Guid.NewGuid();
        context.Documents.Add(new Document
        {
            Id = documentId,
            TenantId = _tenantId,
            OwnerPartyId = ownerPartyId,
            DocumentType = "tax_return",
            Status = "Submitted",
            Classification = DocumentClassification.Personal,
            IndexStatus = DocumentIndexStatus.Indexed,
        });
        context.DocumentFiles.Add(NewFile(documentId, "tenants/x/doc/a.txt"));
        context.DocumentFiles.Add(NewFile(documentId, "tenants/x/doc/b.txt"));
        await context.SaveChangesAsync();

        var deletedKeys = new List<string>();
        _fileStore
            .Setup(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((key, _) => deletedKeys.Add(key))
            .Returns(Task.CompletedTask);
        _vectorIndex
            .Setup(i => i.PurgeDocumentAsync(documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);

        await CreateService(context).DeleteDocumentAsync(documentId);

        _vectorIndex.Verify(i => i.PurgeDocumentAsync(documentId, It.IsAny<CancellationToken>()), Times.Once);
        deletedKeys.Should().BeEquivalentTo("tenants/x/doc/a.txt", "tenants/x/doc/b.txt");

        // Soft-deleted: invisible to normal reads, but the row survives with IsDeleted = true.
        (await context.Documents.FirstOrDefaultAsync(d => d.Id == documentId)).Should().BeNull();
        var soft = await context.Documents.IncludeSoftDeleted().FirstOrDefaultAsync(d => d.Id == documentId);
        soft.Should().NotBeNull();
        soft!.IsDeleted.Should().BeTrue();
        (await context.DocumentFiles.IncludeSoftDeleted()
            .CountAsync(f => f.DocumentId == documentId && f.IsDeleted)).Should().Be(2);

        context.Set<OutboxMessage>().Should()
            .ContainSingle(m => m.EventType.Contains("DocumentDeletedEvent"));
    }

    [Fact]
    public async Task DeleteDocumentAsync_Should_Throw_And_Touch_Nothing_When_Not_Found()
    {
        await using var context = CreateContext();

        var act = async () => await CreateService(context).DeleteDocumentAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>();
        _vectorIndex.Verify(
            i => i.PurgeDocumentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _fileStore.Verify(
            s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteDocumentAsync_Should_EraseExtractionPayloads_AndSourceMetadata_WhileRetainingAuditReferences()
    {
        await using var context = CreateContext(); Guid documentId = Guid.NewGuid(); Guid aiRunId = Guid.NewGuid();
        var document = new Document { Id = documentId, TenantId = _tenantId, DocumentType = "FinancialSource", Classification = DocumentClassification.Restricted,
            Title = "private source title", IssuerName = "private issuer", ReferenceNumber = "private reference", TagsJson = "[\"private\"]", AttributesJson = "{\"private\":true}" };
        context.Documents.Add(document);
        DocumentFile file = NewFile(documentId, "private:financial.txt"); file.MetadataJson = "{\"private\":\"source\"}"; file.SourceEvidenceJson = "private-source";
        context.DocumentFiles.Add(file);
        context.DocumentExtractions.Add(new Aonik.Documents.Entities.DocumentExtraction { TenantId = _tenantId, DocumentId = documentId,
            ExtractionType = "FinancialCandidate.v1", OutputJson = "{\"private\":\"extracted fields\"}", InputHash = new string('a', 64), Confidence = .99, AiRunId = aiRunId });
        context.DocumentIngestions.Add(new Aonik.Documents.Entities.DocumentIngestion { TenantId = _tenantId, DocumentId = documentId, DocumentFileId = file.Id, LastError = "private parser text" });
        await context.SaveChangesAsync();

        await CreateService(context).DeleteDocumentAsync(documentId);

        var extraction = await context.DocumentExtractions.SingleAsync();
        extraction.OutputJson.Should().Be("{}"); extraction.Confidence.Should().BeNull(); extraction.Status.Should().Be("Erased");
        extraction.InputHash.Should().HaveLength(64); extraction.AiRunId.Should().Be(aiRunId);
        (await context.DocumentIngestions.SingleAsync()).LastError.Should().BeNull();
        file.SourceEvidenceJson.Should().BeNull(); file.MetadataJson.Should().Be("{}");
        file.FileName.Should().BeNull(); file.StorageKey.Should().Be("erased:" + file.Id.ToString("N"));
        document.Title.Should().BeNull(); document.IssuerName.Should().BeNull(); document.ReferenceNumber.Should().BeNull();
        document.TagsJson.Should().Be("[]"); document.AttributesJson.Should().Be("{}");
    }

    private DocumentFile NewFile(Guid documentId, string storageKey) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = _tenantId,
        DocumentId = documentId,
        StorageProvider = "azure",
        StorageKey = storageKey,
        ContentType = "text/plain",
        FileName = "f.txt",
    };
}
