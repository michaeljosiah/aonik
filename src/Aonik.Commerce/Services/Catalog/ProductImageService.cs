using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Storage;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Commerce.Services.Catalog;

internal sealed class ProductImageService(
    CommerceDbContext context,
    ITenantProvider tenants,
    IImageProcessingService images,
    [FromKeyedServices(FileStoreKeys.ProductImages)] IFileStore files) : IProductImageService
{
    public const int MaxBytes = 10 * 1024 * 1024;
    public const int MaxAltTextLength = 500;

    public async Task<ProductImageUploadDto> UploadAsync(Guid productId, Stream content, string altText,
        CancellationToken cancellationToken = default)
    {
        var tenantId = tenants.GetCurrentTenantId();
        if (!await context.Products.AnyAsync(p => p.Id == productId && p.TenantId == tenantId, cancellationToken))
            throw new NotFoundException("Product was not found.");
        if (string.IsNullOrWhiteSpace(altText) || altText.Length > MaxAltTextLength)
            throw new StorefrontValidationException("Image alt text must contain 1 to 500 characters.");

        // Bound actual bytes, including non-seekable callers; do not trust multipart length or MIME.
        using var input = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (input.Length + read > MaxBytes)
                throw new StorefrontValidationException("Product images must be at most 10 MiB.");
            await input.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        if (input.Length == 0) throw new StorefrontValidationException("An image file is required.");

        input.Position = 0;
        var type = await images.DetectContentTypeAsync(input, cancellationToken);
        // Check headers before Identify/decode: unsupported containers never reach their decoders.
        if (type is not ("image/jpeg" or "image/png"))
            throw new StorefrontValidationException("Choose a valid JPEG or PNG image.");

        using var jpeg = new MemoryStream();
        try
        {
            input.Position = 0;
            var (width, height) = await images.GetImageDimensionsAsync(input, cancellationToken);
            if (width < 1 || height < 1 || width > 10_000 || height > 10_000 || (long)width * height > 40_000_000)
                throw new StorefrontValidationException("Product images must be at most 40 megapixels and 10,000 pixels per side.");
            input.Position = 0;
            await images.ResizeImageAsync(input, jpeg, 1920, 1920, quality: 90,
                stripMetadata: true, cancellationToken: cancellationToken);
        }
        catch (ArgumentException)
        {
            throw new StorefrontValidationException("Choose a valid JPEG or PNG image.");
        }

        jpeg.Position = 0;
        var uploaded = await files.UploadAsync(tenantId, productId, jpeg, "product.jpg", "image/jpeg", cancellationToken);
        return new ProductImageUploadDto(files.GetUrl(uploaded.StorageKey), altText.Trim());
    }
}
