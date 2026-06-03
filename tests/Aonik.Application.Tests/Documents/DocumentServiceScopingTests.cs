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
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.TestSupport.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

/// <summary>
/// Owner-party scoping (Spec 035 §14 / R7) and the DocumentType classification default (§10).
/// A customer (PersonalUser, no staff role) may only see/write their own party's documents — the
/// owner is derived from authenticated context, never request input — while staff see tenant-wide.
/// Unclassified uploads default by document type, never to a tenant-wide class.
/// </summary>
public sealed class DocumentServiceScopingTests
{
    private static readonly string[] Customer = { "PersonalUser" };
    private static readonly string[] Staff = { "Operations" };

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _customerParty = Guid.NewGuid();
    private readonly Guid _otherParty = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    private DocumentsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseInMemoryDatabase($"DocsScope_{Guid.NewGuid()}")
            .Options;
        return new DocumentsDbContext(options, new TestTenantProvider(_tenantId));
    }

    private DocumentService CreateService(
        DocumentsDbContext context, string[] roles, Guid? userId = null, Guid? resolvedParty = null)
    {
        var userContext = new Mock<ICurrentUserContext>();
        userContext.SetupGet(c => c.Roles).Returns(roles);
        userContext.SetupGet(c => c.UserId).Returns(userId);

        var resolver = new Mock<IUserPartyResolver>();
        resolver
            .Setup(r => r.GetPartyIdForUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resolvedParty);

        var fileStore = new Mock<IDocumentFileStore>();
        fileStore
            .Setup(s => s.UploadDocumentFileAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Stream>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentFileUploadResult("azure", "container", "key", "text/plain", "f.txt", 10, "hash"));

        return new DocumentService(
            context, new TestTenantProvider(_tenantId), fileStore.Object,
            Mock.Of<IDocumentVectorIndex>(), userContext.Object, resolver.Object);
    }

    private async Task<Guid> SeedAsync(DocumentsDbContext context, Guid ownerParty, string type = "tax_return")
    {
        var id = Guid.NewGuid();
        context.Documents.Add(new Document
        {
            Id = id,
            TenantId = _tenantId,
            OwnerPartyId = ownerParty,
            DocumentType = type,
            Status = "Submitted",
            Classification = DocumentClassification.Personal,
            IndexStatus = DocumentIndexStatus.Pending,
        });
        await context.SaveChangesAsync();
        return id;
    }

    // ── Owner-party scoping: reads ──────────────────────────────────────

    [Fact]
    public async Task ListDocumentsAsync_For_Customer_Returns_Only_Their_Party_Even_If_Other_Requested()
    {
        await using var context = CreateContext();
        await SeedAsync(context, _customerParty);
        await SeedAsync(context, _otherParty);
        var service = CreateService(context, Customer, _userId, _customerParty);

        // The customer tries to widen to another party — it must be ignored.
        var result = await service.ListDocumentsAsync(new ListDocumentsQuery(OwnerPartyId: _otherParty));

        result.TotalCount.Should().Be(1);
        result.Items.Should().OnlyContain(d => d.OwnerPartyId == _customerParty);
    }

    [Fact]
    public async Task ListDocumentsAsync_For_Staff_Returns_Whole_Tenant()
    {
        await using var context = CreateContext();
        await SeedAsync(context, _customerParty);
        await SeedAsync(context, _otherParty);
        var service = CreateService(context, Staff);

        var result = await service.ListDocumentsAsync(new ListDocumentsQuery());

        result.TotalCount.Should().Be(2, "staff/operations see documents across the tenant");
    }

    [Fact]
    public async Task GetDocumentAsync_For_Customer_Returns_Null_For_Another_Party_Document()
    {
        await using var context = CreateContext();
        var otherDoc = await SeedAsync(context, _otherParty);
        var service = CreateService(context, Customer, _userId, _customerParty);

        (await service.GetDocumentAsync(otherDoc)).Should().BeNull();
    }

    [Fact]
    public async Task GetDocumentAsync_For_Customer_Returns_Their_Own_Document()
    {
        await using var context = CreateContext();
        var ownDoc = await SeedAsync(context, _customerParty);
        var service = CreateService(context, Customer, _userId, _customerParty);

        (await service.GetDocumentAsync(ownDoc)).Should().NotBeNull();
    }

    [Fact]
    public async Task GetFilesAsync_For_Customer_Returns_Empty_For_Another_Party_Document()
    {
        await using var context = CreateContext();
        var otherDoc = await SeedAsync(context, _otherParty);
        var service = CreateService(context, Customer, _userId, _customerParty);

        (await service.GetFilesAsync(otherDoc)).Should().BeEmpty();
    }

    [Fact]
    public async Task Customer_Without_A_Party_Sees_Nothing()
    {
        await using var context = CreateContext();
        var doc = await SeedAsync(context, _customerParty);
        var service = CreateService(context, Customer, _userId, resolvedParty: null);

        (await service.ListDocumentsAsync(new ListDocumentsQuery())).TotalCount.Should().Be(0);
        (await service.GetDocumentAsync(doc)).Should().BeNull();
    }

    // ── Owner-party scoping: writes ─────────────────────────────────────

    [Fact]
    public async Task CreateDocumentAsync_For_Customer_Forces_Their_Own_Owner_Party()
    {
        await using var context = CreateContext();
        var service = CreateService(context, Customer, _userId, _customerParty);

        // The customer asks for another party as owner — it must be overridden to themselves.
        var dto = await service.CreateDocumentAsync(
            new CreateDocumentCommand(OwnerPartyId: _otherParty, DocumentType: "TaxReturn"));

        dto.OwnerPartyId.Should().Be(_customerParty);
    }

    [Fact]
    public async Task UploadFileAsync_For_Customer_Into_Another_Party_Document_Throws()
    {
        await using var context = CreateContext();
        var otherDoc = await SeedAsync(context, _otherParty);
        var service = CreateService(context, Customer, _userId, _customerParty);

        var act = async () => await service.UploadFileAsync(
            new UploadFileCommand(otherDoc, "f.txt", "text/plain"), new MemoryStream(new byte[] { 1 }));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateDocumentAsync_For_Customer_Without_A_Party_Throws()
    {
        await using var context = CreateContext();
        var service = CreateService(context, Customer, _userId, resolvedParty: null);

        var act = async () => await service.CreateDocumentAsync(
            new CreateDocumentCommand(_otherParty, "TaxReturn"));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── Classification default from DocumentType (§10) ──────────────────

    [Theory]
    [InlineData("TaxReturn", DocumentClassification.Personal, DocumentIndexStatus.Pending)]
    [InlineData("BankStatement", DocumentClassification.Personal, DocumentIndexStatus.Pending)]
    [InlineData("Mortgage", DocumentClassification.Personal, DocumentIndexStatus.Pending)] // unmapped → fail-closed Personal
    [InlineData("NationalId", DocumentClassification.Sensitive, DocumentIndexStatus.NotIndexable)]
    [InlineData("ProductTerms", DocumentClassification.Internal, DocumentIndexStatus.Pending)]
    public async Task CreateDocumentAsync_Defaults_Classification_From_DocumentType(
        string documentType, DocumentClassification expectedClassification, DocumentIndexStatus expectedIndexStatus)
    {
        await using var context = CreateContext();
        var service = CreateService(context, Staff); // staff → owner honoured, isolate the classification logic

        var dto = await service.CreateDocumentAsync(
            new CreateDocumentCommand(OwnerPartyId: _customerParty, DocumentType: documentType));

        dto.Classification.Should().Be(expectedClassification);
        dto.IndexStatus.Should().Be(expectedIndexStatus);
    }

    [Fact]
    public async Task CreateDocumentAsync_Honours_Explicit_Classification()
    {
        await using var context = CreateContext();
        var service = CreateService(context, Staff);

        var dto = await service.CreateDocumentAsync(new CreateDocumentCommand(
            OwnerPartyId: _customerParty, DocumentType: "TaxReturn",
            Classification: DocumentClassification.Internal));

        dto.Classification.Should().Be(DocumentClassification.Internal);
    }
}
