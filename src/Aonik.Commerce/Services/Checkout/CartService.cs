using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Catalog;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.Checkout;

/// <summary>Cart management over <see cref="CommerceDbContext"/> (Spec 042 §11/§12).</summary>
internal sealed class CartService : ICartService
{
    private readonly CommerceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IProductPricingService _pricing;

    public CartService(CommerceDbContext dbContext, ITenantProvider tenantProvider, IProductPricingService pricing)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _pricing = pricing;
    }

    public async Task<CartDto> CreateCartAsync(CreateCartCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var cart = new Entities.Cart.Cart
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BuyerPartyId = command.BuyerPartyId,
            // R10 — server-minted, never client-supplied: the old store-verbatim contract let a
            // caller register an empty or guessable token. Disclosed exactly once, below.
            AnonymousToken = CartAccess.MintToken(),
            Status = CartStatuses.Open,
            Currency = command.Currency,
        };
        _dbContext.Carts.Add(cart);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return (await LoadDtoAsync(cart.Id, tenantId, cancellationToken))! with { AnonymousToken = cart.AnonymousToken };
    }

    public async Task<CartDto?> GetCartAsync(Guid cartId, CartAccessContext access, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var cart = await _dbContext.Carts.AsNoTracking()
            .Include(c => c.Items).ThenInclude(i => i.Selections)
            .FirstOrDefaultAsync(c => c.Id == cartId && c.TenantId == tenantId, cancellationToken);

        // R10 — unknown and unauthorized are the same null (→ 404); no oracle.
        return cart is null || !CartAccess.IsAuthorized(cart, access) ? null : Map(cart);
    }

    private async Task<CartDto?> LoadDtoAsync(Guid cartId, Guid tenantId, CancellationToken cancellationToken)
    {
        var cart = await _dbContext.Carts.AsNoTracking()
            .Include(c => c.Items).ThenInclude(i => i.Selections)
            .FirstOrDefaultAsync(c => c.Id == cartId && c.TenantId == tenantId, cancellationToken);
        return cart is null ? null : Map(cart);
    }

    public async Task<CartDto> AddItemAsync(AddCartItemCommand command, CartAccessContext access, CancellationToken cancellationToken = default)
    {
        if (command.Quantity <= 0)
        {
            throw new ArgumentException("Quantity must be greater than zero.", nameof(command));
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var cart = await ValidateOpenCartAsync(command.CartId, tenantId, access, cancellationToken);

        var variant = await _dbContext.ProductVariants.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == command.ProductVariantId && v.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Variant '{command.ProductVariantId}' was not found.");

        var unit = await _pricing.ResolvePriceAsync(variant.Id, cart.Currency, null, cancellationToken)
            ?? throw new InvalidOperationException($"No {cart.Currency} price for variant '{variant.Id}'.");

        // Insert the line directly — the cart parent is never re-tracked/updated (it owns no totals).
        _dbContext.CartItems.Add(new CartItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CartId = cart.Id,
            ProductVariantId = variant.Id,
            IsBundle = false,
            Quantity = command.Quantity,
            UnitPriceSnapshot = unit,
            Sku = variant.Sku,
            NameSnapshot = variant.Name,
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (await LoadDtoAsync(cart.Id, tenantId, cancellationToken))!;
    }

    public async Task<CartDto> AddBundleAsync(AddBundleToCartCommand command, CartAccessContext access, CancellationToken cancellationToken = default)
    {
        if (command.Selection is null || command.Selection.Count == 0)
        {
            throw new ArgumentException("A bundle requires at least one selected component.", nameof(command));
        }
        if (command.Selection.Any(s => s.Quantity <= 0))
        {
            throw new ArgumentException("Selection quantities must be greater than zero.", nameof(command));
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var cart = await ValidateOpenCartAsync(command.CartId, tenantId, access, cancellationToken);

        var product = await _dbContext.Products.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == command.BundleProductId && p.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Bundle product '{command.BundleProductId}' was not found.");
        if (product.Kind != ProductKinds.Bundle)
        {
            throw new InvalidOperationException("AddBundle requires a Bundle product.");
        }

        // Resolving the bundle price also validates the selection against the bundle's slots (§12).
        var bundlePrice = await _pricing.ResolveBundlePriceAsync(product.Id, command.Selection, cart.Currency, cancellationToken);

        // Snapshot each chosen component (sku/name/price) for the box contents.
        var variantIds = command.Selection.Select(s => s.ProductVariantId).Distinct().ToList();
        var variants = await _dbContext.ProductVariants.AsNoTracking()
            .Where(v => v.TenantId == tenantId && variantIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, cancellationToken);

        var item = new CartItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CartId = cart.Id,
            ProductVariantId = product.Id, // the bundle product id
            IsBundle = true,
            BundleProductId = product.Id,
            Quantity = 1m,
            UnitPriceSnapshot = bundlePrice,
            Sku = product.Slug,
            NameSnapshot = product.Name,
        };

        foreach (var line in command.Selection)
        {
            variants.TryGetValue(line.ProductVariantId, out var v);
            var componentPrice = await _pricing.ResolvePriceAsync(line.ProductVariantId, cart.Currency, null, cancellationToken) ?? 0m;
            item.Selections.Add(new CartItemSelection
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CartItemId = item.Id,
                BundleSlotId = line.BundleSlotId,
                ProductVariantId = line.ProductVariantId,
                Quantity = line.Quantity,
                UnitPriceSnapshot = componentPrice,
                Sku = v?.Sku ?? string.Empty,
                NameSnapshot = v?.Name ?? string.Empty,
            });
        }

        _dbContext.CartItems.Add(item);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return (await LoadDtoAsync(cart.Id, tenantId, cancellationToken))!;
    }

    public async Task<CartDto> RemoveItemAsync(Guid cartId, Guid cartItemId, CartAccessContext access, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        await ValidateOpenCartAsync(cartId, tenantId, access, cancellationToken);
        var item = await _dbContext.CartItems
            .FirstOrDefaultAsync(i => i.Id == cartItemId && i.CartId == cartId && i.TenantId == tenantId, cancellationToken);
        if (item is not null)
        {
            _dbContext.CartItems.Remove(item);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        return (await LoadDtoAsync(cartId, tenantId, cancellationToken))!;
    }

    public async Task<CartDto> AdoptAsync(Guid cartId, Guid partyId, CartAccessContext access, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var cart = await _dbContext.Carts
            .FirstOrDefaultAsync(c => c.Id == cartId && c.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException($"Cart '{cartId}' was not found.");

        // Idempotent for the party that already owns it — a retried adopt is a no-op.
        if (cart.BuyerPartyId == partyId)
        {
            return (await LoadDtoAsync(cartId, tenantId, cancellationToken))!;
        }

        // Z2 — a cart bound to ANOTHER party, or a wrong/absent guest token, is the same 404 an
        // unknown cart gets: adoption needs possession AND identity, with no oracle between.
        if (cart.BuyerPartyId is not null
            || !CartAccess.IsAuthorized(cart, CartAccessContext.ForGuest(access.GuestToken)))
        {
            throw new NotFoundException($"Cart '{cartId}' was not found.");
        }

        // Z4 — an ordered cart's buyer is fixed with its order, reservation and payment amount.
        if (cart.Status != CartStatuses.Open || cart.OrderId is not null)
        {
            throw new StorefrontValidationException("Z4: this cart has been checked out; its buyer cannot change.");
        }

        cart.BuyerPartyId = partyId;
        // Z3 — a leaked pre-adoption token must be dead afterwards.
        cart.AnonymousToken = null;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return (await LoadDtoAsync(cartId, tenantId, cancellationToken))!;
    }

    private async Task<Entities.Cart.Cart> ValidateOpenCartAsync(Guid cartId, Guid tenantId, CartAccessContext access, CancellationToken cancellationToken)
    {
        var cart = await _dbContext.Carts.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == cartId && c.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException($"Cart '{cartId}' was not found.");

        // R10 — unauthorized is the same 404 an unknown id gets; no oracle.
        if (!CartAccess.IsAuthorized(cart, access))
        {
            throw new NotFoundException($"Cart '{cartId}' was not found.");
        }

        // R11 — a box session is writable only through kind-aware routes: a kind-blind insert
        // would land a line that capacity, slot, personalisation, merge and quote rules cannot
        // classify. This guards kind-blindness, not add-ons.
        if (cart.BoxBundleProductId is not null)
        {
            throw new StorefrontValidationException(
                "R11: this cart is a box session — use the box routes (/commerce/carts/{id}/lines).");
        }

        if (cart.Status != CartStatuses.Open)
        {
            throw new InvalidOperationException($"Cart '{cartId}' is {cart.Status}, not Open.");
        }
        // A cart stays Open until payment completes, but once checkout has stamped an OrderId the
        // order/reservation/payment amount are fixed — block further edits so the cart can't diverge.
        if (cart.OrderId is not null)
        {
            throw new InvalidOperationException($"Cart '{cartId}' is checked out (pending payment) and can no longer be edited.");
        }
        return cart;
    }

    private static CartDto Map(Entities.Cart.Cart cart)
    {
        var items = cart.Items.Select(i => new CartItemDto(
            i.Id, i.ProductVariantId, i.IsBundle, i.BundleProductId, i.Quantity, i.UnitPriceSnapshot, i.Sku, i.NameSnapshot,
            i.Quantity * i.UnitPriceSnapshot,
            i.Selections.Select(s => new CartItemSelectionDto(
                s.Id, s.BundleSlotId, s.ProductVariantId, s.Quantity, s.UnitPriceSnapshot, s.Sku, s.NameSnapshot)).ToList())).ToList();

        // R10 — the token is disclosed exactly once, by create; every other read carries null.
        return new CartDto(cart.Id, cart.BuyerPartyId, null, cart.Status, cart.Currency, cart.OrderId,
            items.Sum(i => i.LineTotal), items, cart.BoxBundleProductId);
    }
}
