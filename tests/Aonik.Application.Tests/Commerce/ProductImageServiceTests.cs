using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Services.Catalog;
using Aonik.Infrastructure.Storage;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Storage;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Aonik.Application.Tests.Commerce;

public class ProductImageServiceTests
{
    [Fact]
    public async Task Upload_Should_StoreAProductJpegDraft_WithoutReplacingMediaOrApprovingTheDish()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var context = CommerceTestHarness.CreateContext(options, tenantId);
        var product = new Product { TenantId = tenantId, Name = "Dish", Slug = "dish" };
        context.Products.Add(product);
        await context.SaveChangesAsync();
        byte[]? stored = null;
        var files = new Mock<IFileStore>(MockBehavior.Strict);
        files.Setup(x => x.UploadAsync(tenantId, product.Id, It.IsAny<Stream>(), "product.jpg", "image/jpeg", It.IsAny<CancellationToken>()))
            .Returns((Guid _, Guid _, Stream body, string _, string _, CancellationToken _) =>
            {
                using var copy = new MemoryStream();
                body.CopyTo(copy);
                stored = copy.ToArray();
                return Task.FromResult(new FileUploadResult("Local", "products", "photo.jpg", "image/jpeg", "product.jpg", stored.Length, "hash"));
            });
        files.Setup(x => x.GetUrl("photo.jpg")).Returns("/storage/products/photo.jpg");
        var service = new ProductImageService(context, new TestTenantProvider(tenantId), new ImageProcessingService(), files.Object);
        using var png = new MemoryStream();
        using (var image = new Image<Rgba32>(2300, 10)) await image.SaveAsPngAsync(png);
        png.Position = 0;

        var result = await service.UploadAsync(product.Id, png, "  A plate of jollof rice  ");

        result.Url.Should().Be("/storage/products/photo.jpg");
        result.AltText.Should().Be("A plate of jollof rice");
        using var decoded = Image.Load(stored!);
        decoded.Width.Should().Be(1920);
        decoded.Metadata.DecodedImageFormat!.DefaultMimeType.Should().Be("image/jpeg");
        (await context.ProductMedia.CountAsync()).Should().Be(0, "upload returns a draft for the existing ordered media save");
        product.IsPlaceholder.Should().BeTrue();
        files.VerifyAll();
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("oversized")]
    [InlineData("html")]
    [InlineData("tiff")]
    [InlineData("truncated-png")]
    [InlineData("missing-alt")]
    [InlineData("long-alt")]
    public async Task Upload_Should_RejectInvalidInput_BeforeWritingStorage(string scenario)
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var context = CommerceTestHarness.CreateContext(options, tenantId);
        var product = new Product { TenantId = tenantId, Name = "Dish", Slug = "dish" };
        context.Products.Add(product);
        await context.SaveChangesAsync();
        var files = new Mock<IFileStore>(MockBehavior.Strict);
        var service = new ProductImageService(context, new TestTenantProvider(tenantId), new ImageProcessingService(), files.Object);
        var bytes = scenario switch
        {
            "oversized" => new byte[ProductImageService.MaxBytes + 1],
            "html" => "<html>not a photo</html>"u8.ToArray(),
            "tiff" => new byte[] { 0x49, 0x49, 0x2a, 0, 8, 0, 0, 0, 0, 0 },
            "truncated-png" => new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 },
            _ => Array.Empty<byte>()
        };
        using var input = new MemoryStream(bytes);
        var alt = scenario == "missing-alt" ? " " : scenario == "long-alt" ? new string('a', 501) : "A dish";

        var action = () => service.UploadAsync(product.Id, input, alt);

        await action.Should().ThrowAsync<StorefrontValidationException>();
        files.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Upload_Should_RejectOtherTenants_BeforeInspectingTheFile()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        var foreignId = Guid.NewGuid();
        await using (var foreign = CommerceTestHarness.CreateContext(options, foreignId))
        {
            foreign.Products.Add(new Product { Id = foreignId, TenantId = foreignId, Name = "Other", Slug = "other" });
            await foreign.SaveChangesAsync();
        }
        await using var context = CommerceTestHarness.CreateContext(options, tenantId);
        var files = new Mock<IFileStore>(MockBehavior.Strict);
        var images = new Mock<IImageProcessingService>(MockBehavior.Strict);
        var service = new ProductImageService(context, new TestTenantProvider(tenantId), images.Object, files.Object);
        using var input = new MemoryStream("unread"u8.ToArray());

        var action = () => service.UploadAsync(foreignId, input, "A dish");

        await action.Should().ThrowAsync<NotFoundException>();
        input.Position.Should().Be(0);
        images.VerifyNoOtherCalls();
        files.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Upload_Should_RejectExcessiveDimensions_BeforeDecodingOrStorage()
    {
        var (options, tenantId) = CommerceTestHarness.NewDb();
        await using var context = CommerceTestHarness.CreateContext(options, tenantId);
        var product = new Product { TenantId = tenantId, Name = "Dish", Slug = "dish" };
        context.Products.Add(product);
        await context.SaveChangesAsync();
        var files = new Mock<IFileStore>(MockBehavior.Strict);
        var images = new Mock<IImageProcessingService>(MockBehavior.Strict);
        images.Setup(x => x.DetectContentTypeAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>())).ReturnsAsync("image/png");
        images.Setup(x => x.GetImageDimensionsAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>())).ReturnsAsync((10_000, 10_000));
        var service = new ProductImageService(context, new TestTenantProvider(tenantId), images.Object, files.Object);
        using var input = new MemoryStream(new byte[] { 1 });

        var action = () => service.UploadAsync(product.Id, input, "A dish");

        await action.Should().ThrowAsync<StorefrontValidationException>();
        images.VerifyAll();
        files.VerifyNoOtherCalls();
    }
}
