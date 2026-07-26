using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.Catalog;

/// <summary>Product + bundle pricing over <see cref="CommerceDbContext"/> (Spec 042 §9/§12).</summary>
internal sealed class ProductPricingService : IProductPricingService
{
    private readonly CommerceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;

    public ProductPricingService(CommerceDbContext dbContext, ITenantProvider tenantProvider, IClock clock)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _clock = clock;
    }

    public async Task<ProductPriceDto> SetPriceAsync(SetPriceCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var variantExists = await _dbContext.ProductVariants
            .AnyAsync(v => v.Id == command.ProductVariantId && v.TenantId == tenantId, cancellationToken);
        if (!variantExists)
        {
            throw new InvalidOperationException($"Variant '{command.ProductVariantId}' was not found.");
        }

        // Supersede any currently-active price for the same (variant, currency) so "the active price"
        // is unambiguous.
        var existing = await _dbContext.ProductPrices
            .Where(p => p.TenantId == tenantId
                && p.ProductVariantId == command.ProductVariantId
                && p.Currency == command.Currency
                && p.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var p in existing) p.IsActive = false;

        var price = new ProductPrice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductVariantId = command.ProductVariantId,
            Currency = command.Currency,
            Amount = command.Amount,
            EffectiveFrom = command.EffectiveFrom,
            EffectiveTo = command.EffectiveTo,
            IsActive = true,
        };
        _dbContext.ProductPrices.Add(price);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ProductPriceDto(price.Id, price.ProductVariantId, price.Currency, price.Amount,
            price.EffectiveFrom, price.EffectiveTo, price.IsActive);
    }

    public async Task<decimal?> ResolvePriceAsync(Guid productVariantId, string currency, DateTime? atUtc = null, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var at = atUtc ?? _clock.UtcNow;

        var price = await _dbContext.ProductPrices
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId
                && p.ProductVariantId == productVariantId
                && p.Currency == currency
                && p.IsActive
                && (p.EffectiveFrom == null || p.EffectiveFrom <= at)
                && (p.EffectiveTo == null || p.EffectiveTo > at))
            .OrderByDescending(p => p.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);

        return price?.Amount;
    }

    public async Task<IReadOnlyDictionary<Guid, decimal?>> ResolvePricesAsync(
        IReadOnlyCollection<Guid> productVariantIds,
        string currency,
        DateTime? atUtc = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var at = atUtc ?? _clock.UtcNow;
        var ids = productVariantIds.Distinct().ToList();
        var result = ids.ToDictionary(id => id, _ => (decimal?)null);
        if (ids.Count == 0)
        {
            return result;
        }

        var rows = await _dbContext.ProductPrices
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId
                && ids.Contains(p.ProductVariantId)
                && p.Currency == currency
                && p.IsActive
                && (p.EffectiveFrom == null || p.EffectiveFrom <= at)
                && (p.EffectiveTo == null || p.EffectiveTo > at))
            .ToListAsync(cancellationToken);

        // Latest EffectiveFrom wins, an open-dated row losing to any dated one —
        // the same ordering the single read applies in SQL.
        foreach (var group in rows.GroupBy(p => p.ProductVariantId))
        {
            result[group.Key] = group.OrderByDescending(p => p.EffectiveFrom ?? DateTime.MinValue).First().Amount;
        }

        return result;
    }

    public async Task<decimal> ResolveBundlePriceAsync(
        Guid bundleProductId,
        IReadOnlyCollection<BundleSelectionLine> selection,
        string currency,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var product = await _dbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == bundleProductId && p.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Bundle product '{bundleProductId}' was not found.");

        if (product.Kind != ProductKinds.Bundle)
        {
            throw new InvalidOperationException("ResolveBundlePrice requires a Bundle product.");
        }

        var slots = await _dbContext.BundleSlots
            .AsNoTracking()
            .Include(s => s.Options)
            .Where(s => s.BundleProductId == bundleProductId && s.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        ValidateSelection(slots, selection);
        await ValidateCategoryEligibilityAsync(tenantId, slots, selection, cancellationToken);

        var mode = product.BundlePricingMode ?? BundlePricingModes.SumOfComponents;

        // Spec 068 — a size-tiered bundle is priced by its size plan (box price by size), not by
        // its components; the generic bundle path has no size to price. The box cart routes own
        // these products end to end.
        if (mode == BundlePricingModes.SizeTiered)
        {
            throw new StorefrontValidationException(
                "Size-tiered bundles are priced by their size plan; use the box cart routes.");
        }

        if (mode == BundlePricingModes.Fixed)
        {
            if (product.BundleFixedAmount is not { } fixedAmount)
            {
                throw new InvalidOperationException("A Fixed-price bundle must define BundleFixedAmount.");
            }
            if (product.BundleCurrency is { } bc && !string.Equals(bc, currency, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Bundle is priced in {bc}, not {currency}.");
            }
            return fixedAmount;
        }

        // Sum-based modes: sum component prices (+ optional per-option delta) × quantity.
        decimal sum = 0m;
        foreach (var line in selection)
        {
            var unit = await ResolvePriceAsync(line.ProductVariantId, currency, null, cancellationToken)
                ?? throw new InvalidOperationException($"No {currency} price for variant '{line.ProductVariantId}'.");

            var delta = slots.SelectMany(s => s.Options)
                .FirstOrDefault(o => o.BundleSlotId == line.BundleSlotId && o.ProductVariantId == line.ProductVariantId)?.PriceDelta ?? 0m;

            sum += (unit + delta) * line.Quantity;
        }

        if (mode == BundlePricingModes.SumPlusPremium)
        {
            sum += product.BundlePremium ?? 0m;
        }

        return sum;
    }

    /// <summary>
    /// For a slot sourced from a category (<c>FromCategoryId</c>, no explicit options), every chosen
    /// variant must belong to a product in that category (Spec 042 §12). Slots with explicit options
    /// are already constrained by <see cref="ValidateSelection"/>.
    /// </summary>
    private async Task ValidateCategoryEligibilityAsync(
        Guid tenantId,
        IReadOnlyList<BundleSlot> slots,
        IReadOnlyCollection<BundleSelectionLine> selection,
        CancellationToken cancellationToken)
    {
        var categorySlots = slots.Where(s => s.FromCategoryId is not null && s.Options.Count == 0).ToList();
        if (categorySlots.Count == 0)
        {
            return;
        }

        var categoryIds = categorySlots.Select(s => s.FromCategoryId!.Value).Distinct().ToList();
        var eligible = await (
            from v in _dbContext.ProductVariants.AsNoTracking()
            join p in _dbContext.Products.AsNoTracking() on v.ProductId equals p.Id
            where v.TenantId == tenantId && p.CategoryId != null && categoryIds.Contains(p.CategoryId.Value)
            select new { VariantId = v.Id, CategoryId = p.CategoryId!.Value })
            .ToListAsync(cancellationToken);

        var allowedByCategory = eligible
            .GroupBy(x => x.CategoryId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.VariantId).ToHashSet());

        foreach (var slot in categorySlots)
        {
            var allowed = allowedByCategory.TryGetValue(slot.FromCategoryId!.Value, out var set)
                ? set
                : new HashSet<Guid>();
            var bad = selection
                .Where(l => l.BundleSlotId == slot.Id)
                .FirstOrDefault(l => !allowed.Contains(l.ProductVariantId));
            if (bad is not null)
            {
                throw new ArgumentException(
                    $"Variant '{bad.ProductVariantId}' is not in the category for slot '{slot.Name}'.");
            }
        }
    }

    private static void ValidateSelection(IReadOnlyList<BundleSlot> slots, IReadOnlyCollection<BundleSelectionLine> selection)
    {
        if (slots.Count == 0)
        {
            throw new InvalidOperationException("The bundle has no selection slots defined.");
        }

        foreach (var slot in slots)
        {
            var chosen = selection.Where(l => l.BundleSlotId == slot.Id).ToList();
            var count = (int)chosen.Sum(l => l.Quantity);

            if (count < slot.MinItems || count > slot.MaxItems)
            {
                throw new ArgumentException(
                    $"Slot '{slot.Name}' requires between {slot.MinItems} and {slot.MaxItems} items; got {count}.");
            }

            if (!slot.AllowDuplicates && chosen.Select(l => l.ProductVariantId).Distinct().Count() != chosen.Count)
            {
                throw new ArgumentException($"Slot '{slot.Name}' does not allow duplicate selections.");
            }

            // When a slot curates explicit options, every chosen variant must be one of them.
            if (slot.Options.Count > 0)
            {
                var allowed = slot.Options.Select(o => o.ProductVariantId).ToHashSet();
                var bad = chosen.FirstOrDefault(l => !allowed.Contains(l.ProductVariantId));
                if (bad is not null)
                {
                    throw new ArgumentException($"Variant '{bad.ProductVariantId}' is not an allowed option for slot '{slot.Name}'.");
                }
            }
        }

        // No selection lines may reference a slot that isn't on this bundle.
        var slotIds = slots.Select(s => s.Id).ToHashSet();
        var orphan = selection.FirstOrDefault(l => !slotIds.Contains(l.BundleSlotId));
        if (orphan is not null)
        {
            throw new ArgumentException($"Selection references unknown slot '{orphan.BundleSlotId}'.");
        }
    }
}
