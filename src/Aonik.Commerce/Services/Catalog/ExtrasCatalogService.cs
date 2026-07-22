using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Settings;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.Catalog;

/// <summary>Spec 071 §4 — the extras merchandising read: the configured collection's members
/// with retail prices (the AddOn exception to the no-price rule), resolved content and effective
/// option groups, in curated rank order.</summary>
public interface IExtrasCatalogService
{
    Task<ExtrasListDto> GetExtrasAsync(CancellationToken cancellationToken = default);
}

public record ExtraRowDto(
    Guid ProductId,
    Guid ProductVariantId,
    string Slug,
    string Name,
    string? Description,
    string? ImageUrl,
    IReadOnlyList<string> Tags,
    /// The retail unit price in the tenant currency — add-ons are ordinary retail (Spec 071 §1).
    decimal UnitPrice,
    string Currency,
    /// The resolved standard-preparation content (Spec 067), when authored.
    ResolvedContentDto? Content,
    /// The product's effective option groups, so the picker renders without a second call.
    IReadOnlyList<EffectiveOptionGroupDto> OptionGroups);

/// <summary>Unpriceable members are omitted and COUNTED — a silent drop would read as
/// "everything served" when it did not (Spec 071 B8).</summary>
public record ExtrasListDto(IReadOnlyList<ExtraRowDto> Rows, int Skipped);

internal sealed class ExtrasCatalogService : IExtrasCatalogService
{
    private const string DefaultSlug = "extras";

    private readonly CommerceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ITenantSettingStore _settingStore;
    private readonly ITenantCurrencyProvider _tenantCurrency;
    private readonly IProductPricingService _pricing;
    private readonly IProductOptionService _options;
    private readonly IProductContentService _content;

    public ExtrasCatalogService(
        CommerceDbContext dbContext,
        ITenantProvider tenantProvider,
        ITenantSettingStore settingStore,
        ITenantCurrencyProvider tenantCurrency,
        IProductPricingService pricing,
        IProductOptionService options,
        IProductContentService content)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _settingStore = settingStore;
        _tenantCurrency = tenantCurrency;
        _pricing = pricing;
        _options = options;
        _content = content;
    }

    public async Task<ExtrasListDto> GetExtrasAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var slug = (await _settingStore.GetTenantValueAsync(
                CommerceSettingNames.StorefrontExtrasCollectionSlug, tenantId, cancellationToken))?.Trim();
        slug = string.IsNullOrEmpty(slug) ? DefaultSlug : slug;

        var collection = await _dbContext.Collections
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Slug == slug, cancellationToken);
        if (collection is null)
        {
            return new ExtrasListDto([], 0);   // unconfigured is a state — empty, never a guess
        }

        var memberships = await _dbContext.CollectionItems
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && i.CollectionId == collection.Id)
            .OrderBy(i => i.Rank)
            .Select(i => i.ProductId)
            .ToListAsync(cancellationToken);
        if (memberships.Count == 0)
        {
            return new ExtrasListDto([], 0);
        }

        var products = await _dbContext.Products
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && memberships.Contains(p.Id) && p.Status == ProductStatuses.Active)
            .ToDictionaryAsync(p => p.Id, cancellationToken);
        var variants = await _dbContext.ProductVariants
            .AsNoTracking()
            .Where(v => v.TenantId == tenantId && memberships.Contains(v.ProductId) && v.IsActive)
            .GroupBy(v => v.ProductId)
            .Select(g => g.OrderBy(v => v.CreatedAt).First())
            .ToDictionaryAsync(v => v.ProductId, cancellationToken);
        var heroImages = await _dbContext.ProductMedia
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && memberships.Contains(m.ProductId))
            .GroupBy(m => m.ProductId)
            .Select(g => g.OrderBy(m => m.SortOrder).First())
            .ToDictionaryAsync(m => m.ProductId, m => m.Url, cancellationToken);

        var currency = await _tenantCurrency.GetTenantDefaultCurrencyAsync(tenantId, cancellationToken) ?? "GBP";

        var rows = new List<ExtraRowDto>();
        var skipped = 0;
        foreach (var productId in memberships)
        {
            if (!products.TryGetValue(productId, out var product) || !variants.TryGetValue(productId, out var variant))
            {
                continue;   // inactive/retired members simply are not merchandised
            }

            var price = await _pricing.ResolvePriceAsync(variant.Id, currency, null, cancellationToken);
            if (price is null)
            {
                skipped++;   // B8 — unpriceable is counted, never served or silently dropped
                continue;
            }

            rows.Add(new ExtraRowDto(
                product.Id,
                variant.Id,
                product.Slug,
                product.Name,
                product.Description,
                heroImages.GetValueOrDefault(product.Id),
                ParseTags(product.TagsJson),
                price.Value,
                currency,
                await _content.ResolveAsync(product.Id, null, cancellationToken),
                await _options.GetEffectiveOptionsAsync(product.Id, cancellationToken)));
        }

        return new ExtrasListDto(rows, skipped);
    }

    private static IReadOnlyList<string> ParseTags(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
