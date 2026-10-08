using Aonik.Commerce.Contracts.Models.Catalog;

namespace Aonik.Commerce.Services.Catalog;

public interface IProductImageService
{
    Task<ProductImageUploadDto> UploadAsync(Guid productId, Stream content, string altText,
        CancellationToken cancellationToken = default);
}
