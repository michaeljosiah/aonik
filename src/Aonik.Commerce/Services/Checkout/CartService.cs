using System.Data;

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
    private readonly IClock _clock;

    public CartService(CommerceDbContext dbContext, ITenantProvider tenantProvider, IProductPricingService pricing, IClock clock)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _pricing = pricing;
        _clock = clock;
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
            LastActivityAtUtc = _clock.UtcNow,
        };
        _dbContext.Carts.Add(cart);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(cart) with { AnonymousToken = cart.AnonymousToken };
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

    private async Task<CartDto> LoadDtoAsync(Guid cartId, CartAccessContext access, CancellationToken cancellationToken)
        // Adoption may revoke guest access between the committed edit and this fresh read.
        => await GetCartAsync(cartId, access, cancellationToken)
            ?? throw new NotFoundException($"Cart '{cartId}' was not found.");

    public async Task<CartCheckoutDraftResponse> SaveCheckoutDraftAsync(Guid cartId, CartCheckoutDraftDto draft,
        CartAccessContext access, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var cart = await LoadAuthorizedAsync(cartId, tenantId, access, cancellationToken);
        CartWriteGuard.RequireCurrent(cart, access);
        var json = CartDraftData.Serialize(draft);
        if (cart.CheckoutDraftJson != json)
        {
            cart.CheckoutDraftJson = json;
            await SaveCartEditAsync(cart, cancellationToken);
        }
        return new CartCheckoutDraftResponse(cart.Id, Convert.ToBase64String(cart.RowVersion),
            cart.Status, cart.OrderId, CartDraftData.Read(cart));
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

        // Parent version and line commit together so a stale write leaves no partial line.
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

        await SaveCartEditAsync(cart, cancellationToken);
        return await LoadDtoAsync(cart.Id, access, cancellationToken);
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
        await SaveCartEditAsync(cart, cancellationToken);
        return await LoadDtoAsync(cart.Id, access, cancellationToken);
    }

    public async Task<CartDto> RemoveItemAsync(Guid cartId, Guid cartItemId, CartAccessContext access, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var cart = await ValidateOpenCartAsync(cartId, tenantId, access, cancellationToken);
        var item = await _dbContext.CartItems
            .FirstOrDefaultAsync(i => i.Id == cartItemId && i.CartId == cartId && i.TenantId == tenantId, cancellationToken);
        if (item is not null)
        {
            _dbContext.CartItems.Remove(item);
            await SaveCartEditAsync(cart, cancellationToken);
        }
        return await LoadDtoAsync(cartId, access, cancellationToken);
    }

    public Task<CartDto> AdoptAsync(Guid cartId, Guid partyId, CartAccessContext access, CancellationToken cancellationToken = default)
        => AdoptCoreAsync(cartId, partyId, access, null, cancellationToken);

    public Task<CartDto> AdoptAsync(Guid cartId, Guid partyId, CartAccessContext access, AdoptCartChoice choice,
        CancellationToken cancellationToken = default)
        => AdoptCoreAsync(cartId, partyId, access, choice, cancellationToken);

    private async Task<CartDto> AdoptCoreAsync(Guid cartId, Guid partyId, CartAccessContext access,
        AdoptCartChoice? choice, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var source = await _dbContext.Carts.AsNoTracking()
            .SingleOrDefaultAsync(cart => cart.Id == cartId && cart.TenantId == tenantId, cancellationToken);
        AuthorizeAdoption(source, partyId, access);
        if (source!.BoxBundleProductId is null)
        {
            if (choice is not null)
                throw new StorefrontValidationException("Keep/use-saved choices apply only to box carts.");
            return await AdoptGenericAsync(cartId, partyId, access, cancellationToken);
        }

        if (choice is not null && (choice.Decision is not (CartAdoptionDecisions.KeepGuest or CartAdoptionDecisions.UseSaved)
            || choice.ExpectedSavedCartId == Guid.Empty || choice.ExpectedSavedCartId == cartId))
            throw new StorefrontValidationException("Choose KeepGuest or UseSaved and identify the saved box shown.");

        var touchedIds = new HashSet<Guid> { cartId };
        if (choice is not null) touchedIds.Add(choice.ExpectedSavedCartId);
        Guid selectedId;
        try
        {
            selectedId = await _dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async ct =>
            {
                DetachAdoptionCarts(touchedIds);
                await using var transaction = _dbContext.Database.IsRelational()
                    ? await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;

                var guest = await _dbContext.Carts.Include(cart => cart.Items)
                    .SingleOrDefaultAsync(cart => cart.Id == cartId && cart.TenantId == tenantId, ct);
                AuthorizeAdoption(guest, partyId, access);

                // Replays only read an outcome already owned by this party. They never select
                // a replacement target or repeat an archive using freshly loaded versions.
                var replayId = await ReadAdoptionReplayAsync(guest!, partyId, choice, ct);
                if (replayId.HasValue) return replayId.Value;
                if (choice is not null && guest!.BuyerPartyId == partyId
                    && guest.Status == CartStatuses.Abandoned && guest.OrderId is null)
                    throw ActiveBoxCarts.Conflict(ActiveBoxConflictException.StaleChoice, guest, []);
                EnsureAdoptable(guest!);
                if (choice is null) CartWriteGuard.RequireCurrent(guest!, access);

                // Serializable starts before this indexed range read: two different guest
                // carts must not both become the account's single active box.
                var candidates = await ActiveBoxCarts.ForParty(_dbContext, tenantId, partyId)
                    .Where(cart => cart.Id != cartId).AsNoTracking().Include(cart => cart.Items)
                    .OrderBy(cart => cart.Id).Take(ActiveBoxCarts.CandidateLimit + 1).ToListAsync(ct);
                if (candidates.Count > 1)
                    throw ActiveBoxCarts.Conflict(ActiveBoxConflictException.Multiple, guest, candidates);

                if (candidates.Count == 0)
                {
                    if (choice is not null)
                        throw ActiveBoxCarts.Conflict(ActiveBoxConflictException.StaleChoice, guest, candidates);
                    guest!.BuyerPartyId = partyId;
                    guest.AnonymousToken = null;
                    CartActivity.UserEdit(_dbContext, guest, _clock);
                    await _dbContext.SaveChangesAsync(ct);
                    if (transaction is not null) await transaction.CommitAsync(ct);
                    return cartId;
                }

                if (choice is null)
                    throw ActiveBoxCarts.Conflict(ActiveBoxConflictException.ChoiceRequired, guest, candidates);
                var candidate = candidates[0];
                if (candidate.Id != choice.ExpectedSavedCartId
                    || !ActiveBoxCarts.MatchesVersion(guest!, choice.ExpectedGuestCartVersion)
                    || !ActiveBoxCarts.MatchesVersion(candidate, choice.ExpectedSavedCartVersion))
                    throw ActiveBoxCarts.Conflict(ActiveBoxConflictException.StaleChoice, guest, candidates);

                // Load only the parent for the write; candidate item snapshots stay detached.
                var saved = await _dbContext.Carts.SingleAsync(cart => cart.Id == candidate.Id && cart.TenantId == tenantId, ct);
                touchedIds.Add(saved.Id);
                guest!.BuyerPartyId = partyId;
                guest.AnonymousToken = null;
                if (choice.Decision == CartAdoptionDecisions.KeepGuest)
                    saved.Status = CartStatuses.Abandoned;
                else
                    guest.Status = CartStatuses.Abandoned;

                CartActivity.UserEdit(_dbContext, guest, _clock);
                CartActivity.UserEdit(_dbContext, saved, _clock);
                await _dbContext.SaveChangesAsync(ct);
                if (transaction is not null) await transaction.CommitAsync(ct);
                return choice.Decision == CartAdoptionDecisions.KeepGuest ? cartId : saved.Id;
            }, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            DetachAdoptionCarts(touchedIds);
            var winner = await _dbContext.Carts.AsNoTracking()
                .SingleOrDefaultAsync(cart => cart.Id == cartId && cart.TenantId == tenantId, cancellationToken);
            AuthorizeAdoption(winner, partyId, access);
            var replayId = await ReadAdoptionReplayAsync(winner!, partyId, choice, cancellationToken);
            if (!replayId.HasValue) throw;
            selectedId = replayId.Value;
        }
        catch
        {
            DetachAdoptionCarts(touchedIds);
            throw;
        }

        // Response reads are outside the retrying write, so a rendering failure cannot
        // re-adopt a cart or repeat a customer's destructive choice.
        return await LoadDtoAsync(selectedId, CartAccessContext.ForParty(partyId), cancellationToken);
    }

    private static void AuthorizeAdoption(Entities.Cart.Cart? cart, Guid partyId, CartAccessContext access)
    {
        if (cart is null || partyId == Guid.Empty
            || (cart.BuyerPartyId != partyId && (cart.BuyerPartyId is not null
                || !CartAccess.IsAuthorized(cart, CartAccessContext.ForGuest(access.GuestToken)))))
            throw new NotFoundException("Cart was not found.");
    }

    private async Task<Guid?> ReadAdoptionReplayAsync(Entities.Cart.Cart source, Guid partyId,
        AdoptCartChoice? choice, CancellationToken cancellationToken)
    {
        if (source.BuyerPartyId != partyId) return null;
        if (choice is null)
        {
            EnsureAdoptable(source);
            return source.Id;
        }
        if (source.OrderId is not null || source.AnonymousToken is not null) return null;
        var target = await _dbContext.Carts.AsNoTracking().SingleOrDefaultAsync(cart =>
            cart.Id == choice.ExpectedSavedCartId && cart.TenantId == source.TenantId
            && cart.BuyerPartyId == partyId && cart.BoxBundleProductId != null && cart.OrderId == null,
            cancellationToken);
        if (target is null) return null;
        if (choice.Decision == CartAdoptionDecisions.KeepGuest && source.Status == CartStatuses.Open
            && target.Status == CartStatuses.Abandoned) return source.Id;
        if (choice.Decision == CartAdoptionDecisions.UseSaved && source.Status == CartStatuses.Abandoned
            && target.Status == CartStatuses.Open) return target.Id;
        return null;
    }

    private void DetachAdoptionCarts(HashSet<Guid> cartIds)
    {
        foreach (var cartId in cartIds)
            CartTracking.Detach(_dbContext, _tenantProvider.GetCurrentTenantId(), cartId);
    }

    private async Task<CartDto> AdoptGenericAsync(Guid cartId, Guid partyId, CartAccessContext access, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        CartTracking.Detach(_dbContext, tenantId, cartId);
        var cart = await _dbContext.Carts
            .FirstOrDefaultAsync(c => c.Id == cartId && c.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException($"Cart '{cartId}' was not found.");

        // Idempotent for the party that already owns it — but the Z4 state guard still applies:
        // a retried adopt of a cart that has since been checked out or closed must not read as a
        // fresh successful adoption. Owner-ness is established first so this Z4 is never an
        // oracle for anyone else (a foreign party still falls through to the 404 below).
        if (cart.BuyerPartyId == partyId)
        {
            EnsureAdoptable(cart);
            return await LoadDtoAsync(cartId, CartAccessContext.ForParty(partyId), cancellationToken);
        }

        // Z2 — a cart bound to ANOTHER party, or a wrong/absent guest token, is the same 404 an
        // unknown cart gets: adoption needs possession AND identity, with no oracle between.
        if (cart.BuyerPartyId is not null
            || !CartAccess.IsAuthorized(cart, CartAccessContext.ForGuest(access.GuestToken)))
        {
            throw new NotFoundException($"Cart '{cartId}' was not found.");
        }

        EnsureAdoptable(cart);
        CartWriteGuard.RequireCurrent(cart, access);

        cart.BuyerPartyId = partyId;
        // Z3 — a leaked pre-adoption token must be dead afterwards.
        cart.AnonymousToken = null;
        CartActivity.UserEdit(_dbContext, cart, _clock);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Two adoptions raced on the Cart row version. Answer from what actually committed:
            // the same party winning via its other request is the promised idempotent success;
            // anyone else gets the same 404 as every other unauthorized access (Z2 — losing a
            // race must not become an oracle that the cart exists and someone claimed it).
            _dbContext.Entry(cart).State = EntityState.Detached;
            var winner = await _dbContext.Carts.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == cartId && c.TenantId == tenantId, cancellationToken)
                ?? throw new NotFoundException($"Cart '{cartId}' was not found.");
            if (winner.BuyerPartyId != partyId)
            {
                throw new NotFoundException($"Cart '{cartId}' was not found.");
            }
            EnsureAdoptable(winner);
        }
        catch
        {
            CartTracking.Detach(_dbContext, tenantId, cartId);
            throw;
        }

        return await LoadDtoAsync(cartId, CartAccessContext.ForParty(partyId), cancellationToken);
    }

    /// <summary>Z4 — once checkout has stamped an order (or the cart has left Open), the buyer is
    /// fixed with its order, reservation and payment amount.</summary>
    private static void EnsureAdoptable(Entities.Cart.Cart cart)
    {
        if (!CartWriteGuard.IsEditable(cart) || cart.OrderId is not null)
        {
            throw new StorefrontValidationException("Z4: this cart has been checked out; its buyer cannot change.");
        }
    }

    private async Task SaveCartEditAsync(Entities.Cart.Cart cart, CancellationToken cancellationToken)
    {
        CartActivity.UserEdit(_dbContext, cart, _clock);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            CartTracking.Detach(_dbContext, cart.TenantId, cart.Id);
            throw;
        }
    }

    private async Task<Entities.Cart.Cart> LoadAuthorizedAsync(Guid cartId, Guid tenantId,
        CartAccessContext access, CancellationToken cancellationToken)
    {
        CartTracking.Detach(_dbContext, tenantId, cartId);
        var cart = await _dbContext.Carts
            .FirstOrDefaultAsync(c => c.Id == cartId && c.TenantId == tenantId, cancellationToken);
        if (cart is null || !CartAccess.IsAuthorized(cart, access))
            throw new NotFoundException($"Cart '{cartId}' was not found.");
        return cart;
    }

    private async Task<Entities.Cart.Cart> ValidateOpenCartAsync(Guid cartId, Guid tenantId, CartAccessContext access, CancellationToken cancellationToken)
    {
        var cart = await LoadAuthorizedAsync(cartId, tenantId, access, cancellationToken);
        CartWriteGuard.RequireCurrent(cart, access);

        // R11 — a box session is writable only through kind-aware routes: a kind-blind insert
        // would land a line that capacity, slot, personalisation, merge and quote rules cannot
        // classify. This guards kind-blindness, not add-ons.
        if (cart.BoxBundleProductId is not null)
        {
            throw new StorefrontValidationException(
                "R11: this cart is a box session — use the box routes (/commerce/carts/{id}/lines).");
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
            items.Sum(i => i.LineTotal), items, cart.BoxBundleProductId, Convert.ToBase64String(cart.RowVersion), CartDraftData.Read(cart));
    }
}
