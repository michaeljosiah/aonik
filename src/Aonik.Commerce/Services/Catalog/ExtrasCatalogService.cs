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
    /// Raw attributes JSON, exactly as the 070 summary shape exposes it (badges, serve style...).
    string? AttributesJson,
    /// The retail unit price in the tenant currency — add-ons are ordinary retail (Spec 071 §1).
    decimal UnitPrice,
    /// The product-level per-unit surcharge that will join the charge on add (Spec 066), so the
    /// advertised pre-add price is complete.
    decimal? UnitSurcharge,
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
    private readonly ISettingProvider _settings;
    private readonly ITenantCurrencyProvider _tenantCurrency;
    private readonly IProductPricingService _pricing;
    private readonly IProductOptionService _options;
    private readonly IProductContentService _content;

    public ExtrasCatalogService(
        CommerceDbContext dbContext,
        ITenantProvider tenantProvider,
        ITenantSettingStore settingStore,
        ISettingProvider settings,
        ITenantCurrencyProvider tenantCurrency,
        IProductPricingService pricing,
        IProductOptionService options,
        IProductContentService content)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _settingStore = settingStore;
        _settings = settings;
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
        if (string.IsNullOrEmpty(slug))
        {
            // R3 — Global/configuration/registered-default scopes count too (the registered
            // default is "extras"); the literal is only the last resort.
            slug = (await _settings.GetAsync(CommerceSettingNames.StorefrontExtrasCollectionSlug, cancellationToken))?.Trim();
        }
        slug = string.IsNullOrEmpty(slug) ? DefaultSlug : slug;

        var collection = await _dbContext.Collections
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Slug == slug && c.IsActive, cancellationToken);
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
            .Where(p => p.TenantId == tenantId && memberships.Contains(p.Id)
                && p.Status == ProductStatuses.Active && p.Kind == ProductKinds.Simple)
            .ToDictionaryAsync(p => p.Id, cancellationToken);
        var variants = await _dbContext.ProductVariants
            .AsNoTracking()
            .Where(v => v.TenantId == tenantId && memberships.Contains(v.ProductId) && v.IsActive)
            .GroupBy(v => v.ProductId)
            .Select(g => g.OrderBy(v => v.CreatedAt).First())
            .ToDictionaryAsync(v => v.ProductId, cancellationToken);
        var heroImages = await _dbContext.ProductMedia
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && memberships.Contains(m.ProductId) && m.Kind == "image")
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
                product.AttributesJson,
                price.Value,
                product.UnitSurcharge,
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
