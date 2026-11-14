using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

using Aonik.Documents.Persistence;
using Aonik.Documents.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.TestSupport.Multitenancy;

namespace Aonik.Application.Tests.Documents;

public class FinancialSourceUploadTests
{
    [Fact]
    public async Task Upload_Should_CanonicalizeFinancialSources_ScanBeforePrivateStorage_AndRefuseAnotherFile()
    {
        Guid tenantId = Guid.NewGuid(); var tenant = new TestTenantProvider(tenantId);
        using var db = new DocumentsDbContext(new DbContextOptionsBuilder<DocumentsDbContext>().UseInMemoryDatabase("FinancialUpload_" + Guid.NewGuid()).Options, tenant);
        List<string> calls = []; var files = new Mock<IDocumentFileStore>(); var scanner = new Mock<IDocumentSafetyScanner>();
        scanner.Setup(value => value.ScanAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>())).Callback(() => calls.Add("scan")).Returns(Task.CompletedTask);
        files.Setup(value => value.UploadPrivateDocumentFileAsync(tenantId, It.IsAny<Guid>(), It.IsAny<Stream>(), "invoice.txt", "text/plain", It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("private")).ReturnsAsync(new DocumentFileUploadResult("Local", "documents-private", "private:invoice.txt", "text/plain", "invoice.txt", 7, new string('a', 64)));
        var service = new DocumentService(db, tenant, files.Object, Mock.Of<IDocumentVectorIndex>(), Mock.Of<ICurrentUserContext>(), Mock.Of<IUserPartyResolver>(), scanner.Object);
        var document = await service.CreateDocumentAsync(new(Guid.NewGuid(), "financial_source"));
        using var first = new MemoryStream("invoice"u8.ToArray());

        await service.UploadFileAsync(new(document.DocumentId, "invoice.txt", "text/plain"), first);

        document.DocumentType.Should().Be("FinancialSource"); document.Classification.Should().Be(DocumentClassification.Restricted);
        calls.Should().Equal("scan", "private"); files.Verify(value => value.UploadDocumentFileAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        using var second = new MemoryStream([1]);
        Func<Task> replace = async () => { await service.UploadFileAsync(new(document.DocumentId, "invoice.txt", "text/plain"), second); };
        await replace.Should().ThrowAsync<InvalidOperationException>().WithMessage("*one immutable*");
        calls.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Upload_Should_RefuseUnsafeOrUnscannableFinancialSources_WithoutWritingAnyBlob(bool configured)
    {
        Guid tenantId = Guid.NewGuid(); var tenant = new TestTenantProvider(tenantId);
        using var db = new DocumentsDbContext(new DbContextOptionsBuilder<DocumentsDbContext>().UseInMemoryDatabase("FinancialUpload_" + Guid.NewGuid()).Options, tenant);
        var files = new Mock<IDocumentFileStore>(); var scanner = new Mock<IDocumentSafetyScanner>();
        scanner.Setup(value => value.ScanAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>())).ThrowsAsync(new DocumentTextExtractionException("DocumentUnsafe"));
        var service = new DocumentService(db, tenant, files.Object, Mock.Of<IDocumentVectorIndex>(), Mock.Of<ICurrentUserContext>(), Mock.Of<IUserPartyResolver>(), configured ? scanner.Object : null);
        var document = await service.CreateDocumentAsync(new(Guid.NewGuid(), "FinancialSource")); using var bytes = new MemoryStream([1]);

        Func<Task> upload = async () => { await service.UploadFileAsync(new(document.DocumentId, "invoice.pdf", "application/pdf"), bytes); };

        await upload.Should().ThrowAsync<InvalidOperationException>();
        (await db.DocumentFiles.CountAsync()).Should().Be(0); files.VerifyNoOtherCalls();
    }
}
