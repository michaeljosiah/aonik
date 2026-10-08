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
    public static ProductSummaryDto Map(
        Product product, IReadOnlyList<string> tags,
        ResolvedContentDto? content = null, ProductCategory? category = null) => new(
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
        product.UnitSurcharge,
        product.Description,
        category?.Name,
        category?.Slug,
        product.Heat,
        product.ComponentsLine,
        product.LowSugar,
        product.Freezable,
        product.ShelfLife,
        product.IsPlaceholder,
        HeroImage(product)?.AltText,
        SafeNutrition(content)?.Kcal,
        SafeNutrition(content)?.ProteinGrams,
        SafeNutrition(content)?.FibreGrams,
        content?.ServingLabel,
        content?.IsStale ?? false,
        content?.IsStandardPreparation ?? false,
        content?.ContentVersion);

    // A stale preparation cannot support a numeric card claim, filter or sort. All three
    // surfaces use this projection so a badge never contradicts the query that selected it.
    public static NutritionDto? SafeNutrition(ResolvedContentDto? content)
        => content is { IsStale: false } ? content.Nutrition : null;

    /// <summary>Parses tags defensively first — a malformed legacy row renders with empty tags
    /// and a warning, never a 500 (§11 / A13).</summary>
    public static ProductSummaryDto Map(
        Product product, ILogger logger,
        ResolvedContentDto? content = null, ProductCategory? category = null)
    {
        var tags = StorefrontJson.ParseStringArray(product.TagsJson, out var malformed);
        if (malformed)
        {
            LogMalformedTags(logger, product.Slug, product.Id);
        }
        return Map(product, tags, content, category);
    }

    /// <summary>First ProductMedia image by SortOrder; null when the product has none (§8).</summary>
    public static string? HeroImageUrl(Product product) => HeroImage(product)?.Url;

    private static ProductMedia? HeroImage(Product product) => product.Media
        .Where(m => m.Kind == "image")
        .OrderBy(m => m.SortOrder)
        .FirstOrDefault();

    [LoggerMessage(
        EventId = 7002,
        Level = LogLevel.Warning,
        Message = "Product {Slug} ({ProductId}) has malformed TagsJson; rendering with empty tags.")]
    private static partial void LogMalformedTags(ILogger logger, string slug, Guid productId);
}
