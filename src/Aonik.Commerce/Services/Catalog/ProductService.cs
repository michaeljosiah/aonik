using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.Catalog;

/// <summary>Catalog management over <see cref="CommerceDbContext"/> (Spec 042 §8/§12).</summary>
internal sealed class ProductService : IProductService
{
    private readonly CommerceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;

    public ProductService(CommerceDbContext dbContext, ITenantProvider tenantProvider)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
    }

    public async Task<ProductDto> CreateProductAsync(CreateProductCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        if (await _dbContext.Products.AnyAsync(p => p.TenantId == tenantId && p.Slug == command.Slug, cancellationToken))
        {
            throw new InvalidOperationException($"A product with slug '{command.Slug}' already exists.");
        }

        var product = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Slug = command.Slug,
            Name = command.Name,
            Description = command.Description,
            Status = command.Status,
            Kind = command.Kind,
            CategoryId = command.CategoryId,
            TagsJson = command.TagsJson ?? "[]",
            AttributesJson = command.AttributesJson ?? "{}",
            BundlePricingMode = command.BundlePricingMode,
            BundleFixedAmount = command.BundleFixedAmount,
            BundlePremium = command.BundlePremium,
            BundleCurrency = command.BundleCurrency,
        };

        foreach (var line in command.Variants ?? Array.Empty<CreateVariantLine>())
        {
            product.Variants.Add(new ProductVariant
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProductId = product.Id,
                Sku = line.Sku,
                Name = line.Name,
                OptionsJson = line.OptionsJson ?? "{}",
                WeightGrams = line.WeightGrams,
                IsActive = true,
            });
        }

        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return (await GetProductAsync(product.Id, cancellationToken))!;
    }

    public async Task<ProductDto?> GetProductAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var product = await QueryWithGraph()
            .FirstOrDefaultAsync(p => p.Id == productId && p.TenantId == tenantId, cancellationToken);
        return product is null ? null : Map(product);
    }

    public async Task<ProductDto?> GetProductBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var product = await QueryWithGraph()
            .FirstOrDefaultAsync(p => p.Slug == slug && p.TenantId == tenantId, cancellationToken);
        return product is null ? null : Map(product);
    }

    public async Task<PagedResult<ProductSummaryDto>> ListProductsAsync(ListProductsQuery query, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var q = _dbContext.Products.AsNoTracking().Where(p => p.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(query.Kind)) q = q.Where(p => p.Kind == query.Kind);
        if (query.CategoryId is { } cat) q = q.Where(p => p.CategoryId == cat);
        if (!string.IsNullOrWhiteSpace(query.Status)) q = q.Where(p => p.Status == query.Status);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            q = q.Where(p => p.Name.Contains(term) || p.Slug.Contains(term));
        }

        var total = await q.CountAsync(cancellationToken);
        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, 200);

        var items = await q
            .OrderBy(p => p.Name)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(p => new ProductSummaryDto(
                p.Id, p.Slug, p.Name, p.Status, p.Kind, p.CategoryId, p.Variants.Count))
            .ToListAsync(cancellationToken);

        return new PagedResult<ProductSummaryDto>(items, total, page, size);
    }

    public async Task<ProductVariantDto> AddVariantAsync(AddVariantCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var productExists = await _dbContext.Products
            .AnyAsync(p => p.Id == command.ProductId && p.TenantId == tenantId, cancellationToken);
        if (!productExists)
        {
            throw new InvalidOperationException($"Product '{command.ProductId}' was not found.");
        }

        var variant = new ProductVariant
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = command.ProductId,
            Sku = command.Sku,
            Name = command.Name,
            OptionsJson = command.OptionsJson ?? "{}",
            WeightGrams = command.WeightGrams,
            IsActive = true,
        };
        _dbContext.ProductVariants.Add(variant);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapVariant(variant, Array.Empty<ProductPrice>());
    }

    public async Task<ProductCategoryDto> CreateCategoryAsync(CreateCategoryCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var category = new ProductCategory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Slug = command.Slug,
            Name = command.Name,
            ParentCategoryId = command.ParentCategoryId,
            SortOrder = command.SortOrder,
        };
        _dbContext.ProductCategories.Add(category);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ProductCategoryDto(category.Id, category.Slug, category.Name, category.ParentCategoryId, category.SortOrder);
    }

    public async Task<BundleSlotDto> AddBundleSlotAsync(AddBundleSlotCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var product = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == command.BundleProductId && p.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Bundle product '{command.BundleProductId}' was not found.");

        if (product.Kind != ProductKinds.Bundle)
        {
            throw new InvalidOperationException("Selection slots can only be added to a Bundle product.");
        }
        if (command.MinItems < 0 || command.MaxItems < command.MinItems || command.MaxItems == 0)
        {
            throw new ArgumentException("A bundle slot requires 0 <= MinItems <= MaxItems and MaxItems > 0.");
        }

        var slot = new BundleSlot
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BundleProductId = command.BundleProductId,
            Name = command.Name,
            MinItems = command.MinItems,
            MaxItems = command.MaxItems,
            FromCategoryId = command.FromCategoryId,
            AllowDuplicates = command.AllowDuplicates,
            SortOrder = command.SortOrder,
        };

        foreach (var opt in command.Options ?? Array.Empty<AddBundleSlotOptionLine>())
        {
            slot.Options.Add(new BundleSlotOption
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                BundleSlotId = slot.Id,
                ProductVariantId = opt.ProductVariantId,
                PriceDelta = opt.PriceDelta,
            });
        }

        _dbContext.BundleSlots.Add(slot);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapSlot(slot);
    }

    private IQueryable<Product> QueryWithGraph()
        => _dbContext.Products
            .AsNoTracking()
            .Include(p => p.Variants).ThenInclude(v => v.Prices)
            .Include(p => p.Media)
            .Include(p => p.BundleSlots).ThenInclude(s => s.Options);

    private static ProductDto Map(Product p) => new(
        p.Id, p.Slug, p.Name, p.Description, p.Status, p.Kind, p.CategoryId, p.TagsJson, p.AttributesJson,
        p.BundlePricingMode, p.BundleFixedAmount, p.BundlePremium, p.BundleCurrency, p.TargetMarginPct,
        p.Variants.OrderBy(v => v.Name).Select(v => MapVariant(v, v.Prices)).ToList(),
        p.Media.OrderBy(m => m.SortOrder).Select(m => new ProductMediaDto(m.Id, m.Url, m.Kind, m.SortOrder)).ToList(),
        p.BundleSlots.OrderBy(s => s.SortOrder).Select(MapSlot).ToList());

    private static ProductVariantDto MapVariant(ProductVariant v, IEnumerable<ProductPrice> prices) => new(
        v.Id, v.ProductId, v.Sku, v.Name, v.OptionsJson, v.WeightGrams, v.IsActive,
        prices.Select(pr => new ProductPriceDto(pr.Id, pr.ProductVariantId, pr.Currency, pr.Amount, pr.EffectiveFrom, pr.EffectiveTo, pr.IsActive)).ToList());

    private static BundleSlotDto MapSlot(BundleSlot s) => new(
        s.Id, s.Name, s.MinItems, s.MaxItems, s.FromCategoryId, s.AllowDuplicates, s.SortOrder,
        s.Options.Select(o => new BundleSlotOptionDto(o.Id, o.ProductVariantId, o.PriceDelta)).ToList());
}
