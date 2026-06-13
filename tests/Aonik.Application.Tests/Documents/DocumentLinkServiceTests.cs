namespace Aonik.Application.Tests.Documents;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aonik.Documents.Persistence;
using Aonik.Documents.Services;
using Aonik.Platform.Entities.Compliance;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.TestSupport.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

/// <summary>
/// Spec 046: document linking + the owner-scoped cross-module reader. A link to
/// another party's target can never surface another party's document (the reader
/// filters on the document's owner).
/// </summary>
public sealed class DocumentLinkServiceTests
{
    private static readonly string[] Staff = { "Operations" };
    private static readonly string[] Customer = { "PersonalUser" };

    private readonly Guid _tenantId = Guid.NewGuid();

    private DocumentsDbContext CreateContext()
        => new(
            new DbContextOptionsBuilder<DocumentsDbContext>()
                .UseInMemoryDatabase($"DocLinks_{Guid.NewGuid()}").Options,
            new TestTenantProvider(_tenantId));

    private DocumentLinkService CreateService(DocumentsDbContext context, string[] roles, Guid? userId = null, Guid? resolvedParty = null)
    {
        var userContext = new Mock<ICurrentUserContext>();
        userContext.SetupGet(c => c.Roles).Returns(roles);
        userContext.SetupGet(c => c.UserId).Returns(userId);

        var resolver = new Mock<IUserPartyResolver>();
        resolver
            .Setup(r => r.GetPartyIdForUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resolvedParty);

        return new DocumentLinkService(context, new TestTenantProvider(_tenantId), userContext.Object, resolver.Object);
    }

    private async Task<Guid> SeedDocumentAsync(DocumentsDbContext context, Guid ownerParty, string type = "receipt", string? title = null, string? fileName = null)
    {
        var id = Guid.NewGuid();
        context.Documents.Add(new Document
        {
            Id = id,
            TenantId = _tenantId,
            OwnerPartyId = ownerParty,
            DocumentType = type,
            Status = "Submitted",
            Title = title,
        });
        if (fileName is not null)
        {
            context.DocumentFiles.Add(new DocumentFile
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                DocumentId = id,
                StorageProvider = "local",
                StorageKey = $"k/{id:N}",
                ContentType = "image/jpeg",
                FileName = fileName,
                PageIndex = 0,
                MetadataJson = "{}",
            });
        }
        await context.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task AddLink_ThenList_ReturnsLink_AndIsIdempotent()
    {
        using var context = CreateContext();
        var service = CreateService(context, Staff);
        var docId = await SeedDocumentAsync(context, Guid.NewGuid());
        var entityId = Guid.NewGuid();

        var link = await service.AddLinkAsync(docId, "careEntity", entityId);
        var dup = await service.AddLinkAsync(docId, "careEntity", entityId);
        var list = await service.ListLinksAsync(docId);

        link!.TargetId.Should().Be(entityId);
        dup!.Id.Should().Be(link.Id); // idempotent
        list!.Should().ContainSingle();
    }

    [Fact]
    public async Task RemoveLink_LeavesDocumentIntact()
    {
        using var context = CreateContext();
        var service = CreateService(context, Staff);
        var docId = await SeedDocumentAsync(context, Guid.NewGuid());
        var link = await service.AddLinkAsync(docId, "paymentLog", Guid.NewGuid());

        var removed = await service.RemoveLinkAsync(docId, link!.Id);

        removed.Should().BeTrue();
        (await service.ListLinksAsync(docId))!.Should().BeEmpty();
        (await context.Documents.FindAsync(docId)).Should().NotBeNull(); // document untouched
    }

    [Fact]
    public async Task GetForTarget_ReturnsLinkedDocRefs_WithTitleTypeFileName()
    {
        using var context = CreateContext();
        var service = CreateService(context, Staff);
        var entityId = Guid.NewGuid();
        var docId = await SeedDocumentAsync(context, Guid.NewGuid(), type: "receipt", title: "Repair invoice", fileName: "invoice.jpg");
        await SeedDocumentAsync(context, Guid.NewGuid()); // unlinked, must not appear
        await service.AddLinkAsync(docId, "careEntity", entityId);

        var refs = await service.GetForTargetAsync("careEntity", entityId);

        refs.Should().ContainSingle();
        refs[0].DocumentId.Should().Be(docId);
        refs[0].Title.Should().Be("Repair invoice");
        refs[0].DocumentType.Should().Be("receipt");
        refs[0].FileName.Should().Be("invoice.jpg");
    }

    [Fact]
    public async Task GetForTarget_OwnerScoped_HidesOtherPartysDocuments()
    {
        using var context = CreateContext();
        var entityId = Guid.NewGuid();
        var myParty = Guid.NewGuid();
        var otherParty = Guid.NewGuid();

        // Seed via a staff service (tenant-wide writer), then read as a customer.
        var staff = CreateService(context, Staff);
        var myDoc = await SeedDocumentAsync(context, myParty);
        var foreignDoc = await SeedDocumentAsync(context, otherParty);
        await staff.AddLinkAsync(myDoc, "careEntity", entityId);
        await staff.AddLinkAsync(foreignDoc, "careEntity", entityId); // another user's doc, same entity id

        var customer = CreateService(context, Customer, userId: Guid.NewGuid(), resolvedParty: myParty);
        var refs = await customer.GetForTargetAsync("careEntity", entityId);

        refs.Should().ContainSingle();
        refs[0].DocumentId.Should().Be(myDoc); // foreign-party doc is invisible
    }

    [Fact]
    public async Task CountForEntities_CountsPerEntity()
    {
        using var context = CreateContext();
        var service = CreateService(context, Staff);
        var entityX = Guid.NewGuid();
        var entityY = Guid.NewGuid();
        var d1 = await SeedDocumentAsync(context, Guid.NewGuid());
        var d2 = await SeedDocumentAsync(context, Guid.NewGuid());
        var d3 = await SeedDocumentAsync(context, Guid.NewGuid());
        await service.AddLinkAsync(d1, "careEntity", entityX);
        await service.AddLinkAsync(d2, "careEntity", entityX);
        await service.AddLinkAsync(d3, "careEntity", entityY);

        var counts = await service.CountForEntitiesAsync(new[] { entityX, entityY });

        counts[entityX].Should().Be(2);
        counts[entityY].Should().Be(1);
    }

    [Fact]
    public async Task AddLink_ReturnsNull_When_DocumentNotOwnedByCaller()
    {
        using var context = CreateContext();
        var otherParty = Guid.NewGuid();
        var foreignDoc = await SeedDocumentAsync(context, otherParty);

        var customer = CreateService(context, Customer, userId: Guid.NewGuid(), resolvedParty: Guid.NewGuid());
        var result = await customer.AddLinkAsync(foreignDoc, "careEntity", Guid.NewGuid());

        result.Should().BeNull();
    }
}
