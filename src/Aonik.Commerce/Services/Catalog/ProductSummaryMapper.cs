using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Entities.Catalog;

using Microsoft.Extensions.Logging;

namespace Aonik.Commerce.Services.Catalog;

/// <summary>
/// The one projection to the §8 summary shape, shared by browse and collection reads so a grid
/// card renders identically wherever the row came from. Requires <c>Media</c> and <c>Variants</c>
/// loaded on the product.
/// </summary>
internal static partial class ProductSummaryMapper
{
    public static ProductSummaryDto Map(Product product, IReadOnlyList<string> tags) => new(
        product.Id,
        product.Slug,
        product.Name,
        product.Status,
        product.Kind,
        product.CategoryId,
        product.Variants.Count,
        HeroImageUrl(product),
        tags,
        product.AttributesJson,
        product.UnitSurcharge);

    /// <summary>Parses tags defensively first — a malformed legacy row renders with empty tags
    /// and a warning, never a 500 (§11 / A13).</summary>
    public static ProductSummaryDto Map(Product product, ILogger logger)
    {
        var tags = StorefrontJson.ParseStringArray(product.TagsJson, out var malformed);
        if (malformed)
        {
            LogMalformedTags(logger, product.Slug, product.Id);
        }
        return Map(product, tags);
    }

    /// <summary>First ProductMedia image by SortOrder; null when the product has none (§8).</summary>
    public static string? HeroImageUrl(Product product) => product.Media
        .Where(m => m.Kind == "image")
        .OrderBy(m => m.SortOrder)
        .Select(m => m.Url)
        .FirstOrDefault();

    [LoggerMessage(
        EventId = 7002,
        Level = LogLevel.Warning,
        Message = "Product {Slug} ({ProductId}) has malformed TagsJson; rendering with empty tags.")]
    private static partial void LogMalformedTags(ILogger logger, string slug, Guid productId);
}
