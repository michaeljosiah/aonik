using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.Catalog;

/// <summary>Spec 068 §5/§11/§12 — size-plan authoring (rules A1–A5) and reads.</summary>
internal sealed class BundleSizePlanService : IBundleSizePlanService
{
    private readonly CommerceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;

    public BundleSizePlanService(CommerceDbContext dbContext, ITenantProvider tenantProvider)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
    }

    public async Task<BoxPlanDto> UpsertAsync(Guid productId, UpsertBundleSizePlanCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var product = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == productId && p.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException($"Product '{productId}' was not found.");

        // A3 — size plans attach to bundles priced by size. A null mode is adopted (authoring a
        // plan IS the declaration of size-tiered pricing); any other explicit mode is a different
        // pricing regime and must be changed deliberately, not silently overwritten.
        if (product.Kind != ProductKinds.Bundle)
        {
            throw new StorefrontValidationException("A3: size plans attach to bundle products only.");
        }
        if (product.BundlePricingMode is not null && product.BundlePricingMode != BundlePricingModes.SizeTiered)
        {
            throw new StorefrontValidationException(
                $"A3: product '{product.Slug}' uses pricing mode '{product.BundlePricingMode}'; " +
                "a size plan requires SizeTiered — change the pricing mode first.");
        }

        ValidateStructure(command);

        var currency = command.Currency.Trim().ToUpperInvariant();

        var plan = await _dbContext.BundleSizePlans
            .Include(p => p.Presets)
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.BundleProductId == productId, cancellationToken);

        // A4 — the currency denominates every open BUILD session's quote; repricing them
        // mid-build is not a thing. A cart whose OrderId is stamped is excluded: its order,
        // reservation and payment amount are already fixed, a currency change no longer affects
        // it — and the A6 sweep deliberately never touches ordered carts, so counting them here
        // would let one abandoned payment pin plan authoring forever.
        if (plan is not null && !string.Equals(plan.Currency, currency, StringComparison.Ordinal))
        {
            var openSessions = await _dbContext.Carts.CountAsync(
                c => c.TenantId == tenantId && c.BoxBundleProductId == productId
                    && c.Status == CartStatuses.Open && c.OrderId == null,
                cancellationToken);
            if (openSessions > 0)
            {
                throw new StorefrontValidationException(
                    $"A4: {openSessions} open box session(s) reference this plan; " +
                    "its currency cannot change until they complete or are abandoned.");
            }
        }

        if (plan is null)
        {
            plan = new BundleSizePlan
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                BundleProductId = productId,
            };
            _dbContext.BundleSizePlans.Add(plan);
        }

        plan.MinSize = command.MinSize;
        plan.MaxSize = command.MaxSize;
        plan.BaseSize = command.BaseSize;
        // K8 — amounts persist at decimal(19,4); storing the caller's unrounded value would make
        // the admin response disagree with every later read and quote.
        plan.BasePrice = Math.Round(command.BasePrice, 4, MidpointRounding.AwayFromZero);
        plan.PerSpacePrice = Math.Round(command.PerSpacePrice, 4, MidpointRounding.AwayFromZero);
        plan.Currency = currency;

        // Full replace, matched by size: same-size rows update in place (a price edit is the
        // common case and must not churn the filtered unique index), removed sizes soft-delete,
        // new sizes insert. Delete-all-recreate could interleave the insert before the delete in
        // one batch and trip the per-statement unique index.
        var live = plan.Presets.Where(p => !p.IsDeleted).ToList();
        foreach (var preset in live.Where(p => command.Presets.All(c => c.Size != p.Size)))
        {
            _dbContext.BundleSizePresets.Remove(preset);
        }
        foreach (var spec in command.Presets)
        {
            var existing = live.FirstOrDefault(p => p.Size == spec.Size);
            if (existing is null)
            {
                var preset = new BundleSizePreset
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    BundleSizePlanId = plan.Id,
                    Size = spec.Size,
                    Price = Math.Round(spec.Price, 4, MidpointRounding.AwayFromZero),
                    Badge = spec.Badge,
                    Blurb = spec.Blurb,
                    SavingAmount = spec.SavingAmount is { } saving
                        ? Math.Round(saving, 4, MidpointRounding.AwayFromZero)
                        : null,
                    SortOrder = spec.SortOrder,
                };
                // Explicit Add: a pre-set key discovered via navigation fixup from a TRACKED plan
                // would be treated as an existing row (Modified) and fail the update.
                if (_dbContext.Entry(plan).State != EntityState.Added)
                {
                    _dbContext.BundleSizePresets.Add(preset);
                }
                if (!plan.Presets.Contains(preset))
                {
                    plan.Presets.Add(preset);
                }
            }
            else
            {
                existing.Price = Math.Round(spec.Price, 4, MidpointRounding.AwayFromZero);
                existing.Badge = spec.Badge;
                existing.Blurb = spec.Blurb;
                existing.SavingAmount = spec.SavingAmount is { } saving
                    ? Math.Round(saving, 4, MidpointRounding.AwayFromZero)
                    : null;
                existing.SortOrder = spec.SortOrder;
            }
        }

        // Authoring a plan on a mode-less bundle adopts SizeTiered (see A3 above); from here the
        // generic bundle pricing path rejects the product — the box routes own it.
        product.BundlePricingMode ??= BundlePricingModes.SizeTiered;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Map(plan);
    }

    public async Task<BoxPlanDto?> GetForProductAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var plan = await _dbContext.BundleSizePlans
            .AsNoTracking()
            .Include(p => p.Presets)
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.BundleProductId == productId, cancellationToken);
        return plan is null ? null : Map(plan);
    }

    public async Task<BoxPlanDto?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var product = await _dbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.TenantId == tenantId && p.Slug == slug && p.Status == ProductStatuses.Active,
                cancellationToken);
        if (product is null || product.Kind != ProductKinds.Bundle)
        {
            return null;
        }

        return await GetForProductAsync(product.Id, cancellationToken);
    }

    private static void ValidateStructure(UpsertBundleSizePlanCommand command)
    {
        // A5 — structural and monetary bounds: an empty, inverted or zero/negative-quoting plan
        // must never reach order and payment creation.
        if (command.MinSize < 1)
        {
            throw new StorefrontValidationException("A5: MinSize must be at least 1.");
        }
        if (command.MaxSize < command.MinSize)
        {
            throw new StorefrontValidationException("A5: MaxSize must be at least MinSize.");
        }
        // A2 — the formula anchor must be a sellable size.
        if (command.BaseSize < command.MinSize || command.BaseSize > command.MaxSize)
        {
            throw new StorefrontValidationException("A2: BaseSize must lie within [MinSize, MaxSize].");
        }
        if (command.BasePrice <= 0)
        {
            throw new StorefrontValidationException("A5: BasePrice must be greater than zero.");
        }
        if (command.PerSpacePrice < 0)
        {
            throw new StorefrontValidationException("A5: PerSpacePrice cannot be negative.");
        }
        var currencyCode = command.Currency?.Trim() ?? string.Empty;
        if (currencyCode.Length != 3 || !currencyCode.All(char.IsAsciiLetter))
        {
            // A malformed code would ride into quotes, orders, invoices and payment initiation
            // and fail only in a financial integration much later.
            throw new StorefrontValidationException("A5: Currency must be a 3-letter ISO code.");
        }

        // PerSpacePrice ≥ 0 makes the formula non-decreasing in size, so its floor over
        // [MinSize, MaxSize] is at MinSize; presets are each checked individually below.
        var formulaFloor = command.BasePrice + (command.MinSize - command.BaseSize) * command.PerSpacePrice;
        if (formulaFloor <= 0)
        {
            throw new StorefrontValidationException(
                $"A5: the formula must price every size above zero — at MinSize it quotes {formulaFloor:0.####}.");
        }

        var seenSizes = new HashSet<int>();
        foreach (var preset in command.Presets)
        {
            // A1 — a preset outside the sellable range can never be quoted; authoring it is a mistake.
            if (preset.Size < command.MinSize || preset.Size > command.MaxSize)
            {
                throw new StorefrontValidationException(
                    $"A1: preset size {preset.Size} lies outside [{command.MinSize}, {command.MaxSize}].");
            }
            if (!seenSizes.Add(preset.Size))
            {
                throw new StorefrontValidationException($"A1: duplicate preset for size {preset.Size}.");
            }
            if (preset.Price <= 0)
            {
                throw new StorefrontValidationException($"A5: preset price for size {preset.Size} must be greater than zero.");
            }
            if (preset.SavingAmount is < 0)
            {
                throw new StorefrontValidationException($"A5: preset saving for size {preset.Size} cannot be negative.");
            }
            // K7 — the mapped columns are nvarchar(64)/nvarchar(256); overlong text must reject
            // at authoring, not surface as a SQL truncation failure InMemory never sees.
            if (preset.Badge is { Length: > 64 })
            {
                throw new StorefrontValidationException($"A5: preset badge for size {preset.Size} is at most 64 characters.");
            }
            if (preset.Blurb is { Length: > 256 })
            {
                throw new StorefrontValidationException($"A5: preset blurb for size {preset.Size} is at most 256 characters.");
            }
        }
    }

    private static BoxPlanDto Map(BundleSizePlan plan) => new(
        plan.BundleProductId,
        plan.MinSize,
        plan.MaxSize,
        plan.BaseSize,
        plan.BasePrice,
        plan.PerSpacePrice,
        plan.Currency,
        plan.Presets
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Size)
            .Select(p => new BoxPlanPresetDto(p.Size, p.Price, p.Badge, p.Blurb, p.SavingAmount, p.SortOrder))
            .ToList());
}
