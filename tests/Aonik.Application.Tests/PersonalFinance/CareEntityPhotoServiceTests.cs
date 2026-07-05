using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Entities;
using Aonik.PersonalFinance.Services;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Application.Tests.PersonalFinance;

/// <summary>
/// Spec 049 Part B acceptance: banner upload sets the pointer and resolves a signed URL,
/// replace erases the prior document, remove clears it, validation fails closed (no document
/// written), and per-user isolation holds (404, not revealed).
/// </summary>
public class CareEntityPhotoServiceTests
{
    private sealed class TestTenantProvider(Guid tenantId) : ITenantProvider
    {
        public Guid GetCurrentTenantId() => tenantId;
        public bool TryGetCurrentTenantId(out Guid id) { id = tenantId; return true; }
    }

    private sealed class TestCurrentUserProvider(Guid userId) : ICurrentUserProvider
    {
        public Guid? GetCurrentUserId() => userId;
        public bool TryGetCurrentUserId(out Guid id) { id = userId; return true; }
    }

    /// <summary>In-memory stand-in for the Documents module: records writes, serves files back.</summary>
    private sealed class FakeDocuments : IDocumentWriter, IDocumentReader
    {
        private readonly Dictionary<Guid, List<DocumentFileDto>> _files = new();
        public int CreatedCount { get; private set; }
        public int UploadedCount { get; private set; }
        public List<Guid> DeletedIds { get; } = new();

        public Task<DocumentDto> CreateDocumentAsync(CreateDocumentCommand command, CancellationToken ct = default)
        {
            CreatedCount++;
            var id = Guid.NewGuid();
            _files[id] = new List<DocumentFileDto>();
            var dto = new DocumentDto(
                id, command.OwnerPartyId, command.DocumentType,
                command.Classification ?? DocumentClassification.Personal,
                command.Status ?? "active", command.Source ?? string.Empty,
                default, null, command.IssuedOn, command.ExpiresOn, command.IssuerName,
                command.CountryCode, command.ReferenceNumber, command.Tags ?? [],
                command.AttributesJson ?? "{}", DateTime.UtcNow, null, command.Title);
            return Task.FromResult(dto);
        }

        public Task<DocumentFileDto> UploadFileAsync(UploadFileCommand command, Stream content, CancellationToken ct = default)
        {
            UploadedCount++;
            var file = new DocumentFileDto(
                Guid.NewGuid(), command.DocumentId, "test", null, "key",
                command.ContentType, command.FileName, content.CanSeek ? content.Length : null,
                null, command.PageIndex, command.Side, DateTime.UtcNow);
            if (!_files.TryGetValue(command.DocumentId, out var list))
            {
                _files[command.DocumentId] = list = new List<DocumentFileDto>();
            }
            list.Add(file);
            return Task.FromResult(file);
        }

        public Task DeleteDocumentAsync(Guid documentId, CancellationToken ct = default)
        {
            DeletedIds.Add(documentId);
            _files.Remove(documentId);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<DocumentFileDto>> GetFilesAsync(Guid documentId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<DocumentFileDto>>(
                _files.TryGetValue(documentId, out var list) ? list : []);

        public Task<Uri> GetReadUrlAsync(Guid documentFileId, TimeSpan ttl, CancellationToken ct = default)
            => Task.FromResult(new Uri($"https://blob.test/{documentFileId}"));

        public Task<DocumentDto?> GetDocumentAsync(Guid documentId, CancellationToken ct = default)
            => Task.FromResult<DocumentDto?>(null);

        public Task<PagedResult<DocumentListItem>> ListDocumentsAsync(ListDocumentsQuery query, CancellationToken ct = default)
            => Task.FromResult(new PagedResult<DocumentListItem>([], 0, query.PageNumber, query.PageSize));
    }

    private static PersonalFinanceDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<PersonalFinanceDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;
        return new PersonalFinanceDbContext(options, new TestTenantProvider(tenantId));
    }

    private static CareEntityService CreateEntityService(
        PersonalFinanceDbContext context, Guid tenantId, Guid userId, IDocumentReader reader)
        => new(context, new TestTenantProvider(tenantId), new TestCurrentUserProvider(userId),
            reader, NullLogger<CareEntityService>.Instance);

    private static CareEntityPhotoService CreatePhotoService(
        PersonalFinanceDbContext context, Guid tenantId, Guid userId, FakeDocuments docs)
        => new(context, new TestTenantProvider(tenantId), new TestCurrentUserProvider(userId),
            docs, CreateEntityService(context, tenantId, userId, docs));

    private static async Task SeedProfileAsync(PersonalFinanceDbContext context, Guid tenantId, Guid userId)
    {
        context.PersonalProfiles.Add(new PersonalProfile
        {
            TenantId = tenantId,
            UserId = userId,
            PartyId = Guid.NewGuid(),
        });
        await context.SaveChangesAsync();
    }

    private static Stream Image() => new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF, 0x00 });

    private static CreateCareEntityRequest OrgRequest()
        => new("organization", null, "St. Stephen's", "NG", "my parish", "⛪", null, null);

    [Fact]
    public async Task SetPhotoAsync_Should_StoreDocument_SetPointer_AndResolveUrl()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        await SeedProfileAsync(context, tenantId, userId);
        var docs = new FakeDocuments();
        var entityService = CreateEntityService(context, tenantId, userId, docs);
        var photoService = CreatePhotoService(context, tenantId, userId, docs);

        var entity = await entityService.CreateAsync(OrgRequest());

        var result = await photoService.SetPhotoAsync(entity.Id, Image(), "church.jpg", "image/jpeg", 4);

        result.Should().NotBeNull();
        result!.PhotoDocumentId.Should().NotBeNull();
        result.PhotoUrl.Should().NotBeNull();
        docs.CreatedCount.Should().Be(1);
        docs.UploadedCount.Should().Be(1);
    }

    [Fact]
    public async Task SetPhotoAsync_Should_ErasePreviousDocument_When_Replaced()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        await SeedProfileAsync(context, tenantId, userId);
        var docs = new FakeDocuments();
        var entityService = CreateEntityService(context, tenantId, userId, docs);
        var photoService = CreatePhotoService(context, tenantId, userId, docs);

        var entity = await entityService.CreateAsync(OrgRequest());

        var first = await photoService.SetPhotoAsync(entity.Id, Image(), "a.jpg", "image/jpeg", 4);
        var firstDocId = first!.PhotoDocumentId!.Value;

        var second = await photoService.SetPhotoAsync(entity.Id, Image(), "b.png", "image/png", 4);

        second!.PhotoDocumentId.Should().NotBe(firstDocId);
        docs.DeletedIds.Should().ContainSingle().Which.Should().Be(firstDocId);
    }

    [Theory]
    [InlineData("application/pdf", 1024)]
    [InlineData("image/gif", 1024)]
    [InlineData("image/jpeg", 0)]
    [InlineData("image/jpeg", CareEntityBannerImage.MaxBytes + 1)]
    public async Task SetPhotoAsync_Should_Reject_AndWriteNothing_When_InvalidImage(string contentType, long length)
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        await SeedProfileAsync(context, tenantId, userId);
        var docs = new FakeDocuments();
        var entityService = CreateEntityService(context, tenantId, userId, docs);
        var photoService = CreatePhotoService(context, tenantId, userId, docs);

        var entity = await entityService.CreateAsync(OrgRequest());

        var act = async () => await photoService.SetPhotoAsync(entity.Id, Image(), "x", contentType, length);

        await act.Should().ThrowAsync<ArgumentException>();
        docs.CreatedCount.Should().Be(0);
        docs.UploadedCount.Should().Be(0);
    }

    [Fact]
    public async Task SetPhotoAsync_Should_ReturnNull_When_EntityBelongsToAnotherUser()
    {
        var tenantId = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        await SeedProfileAsync(context, tenantId, owner);
        await SeedProfileAsync(context, tenantId, stranger);
        var docs = new FakeDocuments();

        var entity = await CreateEntityService(context, tenantId, owner, docs).CreateAsync(OrgRequest());
        var strangerPhotoService = CreatePhotoService(context, tenantId, stranger, docs);

        var result = await strangerPhotoService.SetPhotoAsync(entity.Id, Image(), "x.jpg", "image/jpeg", 4);

        result.Should().BeNull();
        docs.CreatedCount.Should().Be(0);
    }

    [Fact]
    public async Task RemovePhotoAsync_Should_ClearPointer_AndEraseDocument()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        await SeedProfileAsync(context, tenantId, userId);
        var docs = new FakeDocuments();
        var entityService = CreateEntityService(context, tenantId, userId, docs);
        var photoService = CreatePhotoService(context, tenantId, userId, docs);

        var entity = await entityService.CreateAsync(OrgRequest());
        var withPhoto = await photoService.SetPhotoAsync(entity.Id, Image(), "x.jpg", "image/jpeg", 4);
        var docId = withPhoto!.PhotoDocumentId!.Value;

        var removed = await photoService.RemovePhotoAsync(entity.Id);

        removed.Should().BeTrue();
        docs.DeletedIds.Should().Contain(docId);
        (await entityService.GetAsync(entity.Id))!.PhotoDocumentId.Should().BeNull();
        (await entityService.GetAsync(entity.Id))!.PhotoUrl.Should().BeNull();
    }

    [Fact]
    public async Task RemovePhotoAsync_Should_ReturnFalse_When_EntityBelongsToAnotherUser()
    {
        var tenantId = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        await SeedProfileAsync(context, tenantId, owner);
        await SeedProfileAsync(context, tenantId, stranger);
        var docs = new FakeDocuments();

        var entity = await CreateEntityService(context, tenantId, owner, docs).CreateAsync(OrgRequest());

        var removed = await CreatePhotoService(context, tenantId, stranger, docs).RemovePhotoAsync(entity.Id);

        removed.Should().BeFalse();
    }
}
