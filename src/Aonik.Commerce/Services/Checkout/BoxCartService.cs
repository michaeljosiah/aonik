using System.Globalization;
using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Entities.Promotions;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Catalog;
using Aonik.Commerce.Services.Inventory;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Settings;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.Checkout;

/// <summary>
/// Spec 068 — the box-building session. Every operation loads fresh state, authorizes (R10),
/// applies §8 drift repair, recomputes every price from scratch (R7 — no client-supplied amount
/// is read anywhere) and returns the whole box + quote. Capacity-affecting writes serialize on
/// the Cart row's concurrency token (A17): the loser revalidates against fresh state and retries
/// once, then surfaces the conflict as a 409.
/// </summary>
internal sealed class BoxCartService : IBoxCartService, IBoxCheckoutSupport
{
    private readonly CommerceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IOptionSelectionService _selections;
    private readonly IInventoryService _inventory;
    private readonly ITenantSettingStore _settingStore;
    private readonly ISettingProvider _settings;

    public BoxCartService(
        CommerceDbContext dbContext,
        ITenantProvider tenantProvider,
        IOptionSelectionService selections,
        IInventoryService inventory,
        ITenantSettingStore settingStore,
        ISettingProvider settings)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _selections = selections;
        _inventory = inventory;
        _settingStore = settingStore;
        _settings = settings;
    }

    // ─── Create ──────────────────────────────────────────────────────────────

    public async Task<BoxCartDto> CreateAsync(CreateBoxCartCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var product = await _dbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == command.BundleProductId && p.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException($"Product '{command.BundleProductId}' was not found.");
        if (product.Kind != ProductKinds.Bundle
            || product.Status != ProductStatuses.Active
            || product.BundlePricingMode != BundlePricingModes.SizeTiered)
        {
            throw new StorefrontValidationException(
                "Box sessions require an Active bundle product with size-tiered pricing.");
        }

        var plan = await LoadPlanAsync(tenantId, product.Id, cancellationToken);
        ValidateSize(plan, command.Size);

        var cart = new Entities.Cart.Cart
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BuyerPartyId = command.BuyerPartyId,
            // R10 — server-minted, ignoring anything a client may have supplied elsewhere;
            // disclosed exactly once, in this response.
            AnonymousToken = CartAccess.MintToken(),
            Status = CartStatuses.Open,
            Currency = plan.Currency,
            BoxBundleProductId = product.Id,
            BoxSize = command.Size,
        };
        _dbContext.Carts.Add(cart);

        var changes = new List<BoxChangeDto>();
        if (command.FirstLine is { } firstLine)
        {
            // The dish-detail → Step 1 handoff, atomically: an invalid first line fails the whole
            // create rather than stranding an empty session behind an error.
            await AddLineCoreAsync(tenantId, cart, plan, firstLine, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await BuildDtoAsync(tenantId, cart, plan, changes, cart.AnonymousToken, cancellationToken);
    }

    // ─── Reads and writes through the serialized core ────────────────────────

    public Task<BoxCartDto> GetAsync(Guid cartId, CartAccessContext access, CancellationToken cancellationToken = default)
        => RunAsync(cartId, access, requireOpen: false, touchCart: false, mutate: null, cancellationToken);

    public Task<BoxCartDto> QuoteAsync(Guid cartId, CartAccessContext access, CancellationToken cancellationToken = default)
        => RunAsync(cartId, access, requireOpen: false, touchCart: false, mutate: null, cancellationToken);

    public Task<BoxCartDto> ChangeSizeAsync(Guid cartId, int newSize, CartAccessContext access, CancellationToken cancellationToken = default)
        => RunAsync(cartId, access, requireOpen: true, touchCart: true, (ctx, ct) =>
        {
            ValidateSize(ctx.Plan, newSize);

            var units = TotalUnits(ctx.BoxLines);
            if (newSize < units)
            {
                // R2 — never silently drop lines; name the exact count to remove first.
                throw new StorefrontValidationException(
                    $"R2: the box holds {units} dish(es); remove {units - newSize} before shrinking to {newSize}.");
            }

            ctx.Cart.BoxSize = newSize;   // reprices the container only
            return Task.CompletedTask;
        }, cancellationToken);

    public Task<BoxCartDto> AddLineAsync(Guid cartId, AddBoxLineCommand command, CartAccessContext access, CancellationToken cancellationToken = default)
        => RunAsync(cartId, access, requireOpen: true, touchCart: true, async (ctx, ct) =>
        {
            await AddLineCoreAsync(ctx.TenantId, ctx.Cart, ctx.Plan, command, ct);
        }, cancellationToken);

    public Task<BoxCartDto> UpdateLineAsync(Guid cartId, Guid lineId, UpdateBoxLineCommand command, CartAccessContext access, CancellationToken cancellationToken = default)
        => RunAsync(cartId, access, requireOpen: true, touchCart: true, async (ctx, ct) =>
        {
            var line = ctx.BoxLines.FirstOrDefault(l => l.Id == lineId)
                ?? throw new NotFoundException($"Cart line '{lineId}' was not found.");

            if (command.Quantity is { } quantity)
            {
                if (quantity < 0)
                {
                    throw new StorefrontValidationException(
                        "R12: quantity cannot be negative — a negative line must never reach pricing or reservation.");
                }
                if (quantity == 0)
                {
                    _dbContext.CartItems.Remove(line);
                    return;
                }

                var delta = quantity - (int)line.Quantity;
                if (delta > 0)
                {
                    await EnsureLineIsAvailableForIncreaseAsync(ctx, line, ct);
                    EnsureCapacity(ctx, delta);
                    await EnsureAvailabilityAsync(ctx, line.ProductVariantId, delta, ct);
                }
                line.Quantity = quantity;
            }

            if (command.Personalisation is { } personalisation)
            {
                var applyTo = command.ApplyToUnits ?? (int)line.Quantity;
                if (applyTo < 1 || applyTo > (int)line.Quantity)
                {
                    throw new StorefrontValidationException(
                        $"R12: applyToUnits must be between 1 and the line quantity ({(int)line.Quantity}).");
                }

                var variant = ctx.Variants[line.ProductVariantId];
                var priced = await _selections.NormalizeAndPriceAsync(
                    variant.ProductId, personalisation, ctx.Cart.Currency, ct);

                if (string.Equals(priced.CanonicalSelectionJson, line.PersonalisationJson, StringComparison.Ordinal))
                {
                    return;   // the "new" selection is this line's own — nothing moves
                }

                var target = ctx.BoxLines.FirstOrDefault(l => l.Id != line.Id
                    && l.BoxBundleSlotId == line.BoxBundleSlotId
                    && l.ProductVariantId == line.ProductVariantId
                    && string.Equals(l.PersonalisationJson, priced.CanonicalSelectionJson, StringComparison.Ordinal));

                if (applyTo == (int)line.Quantity)
                {
                    // Full re-personalisation: merge into an identical line (R6) or restyle in place.
                    if (target is not null)
                    {
                        target.Quantity += line.Quantity;
                        _dbContext.CartItems.Remove(line);
                    }
                    else
                    {
                        ApplySelection(line, priced);
                    }
                }
                else
                {
                    // Split semantics (FR-10.5): remove n units from this line, merge-or-create a
                    // line with the new selection carrying them. One atomic operation — the
                    // two-line state is never client-assembled. Total units are unchanged, so no
                    // capacity or availability re-check applies.
                    line.Quantity -= applyTo;
                    if (target is not null)
                    {
                        target.Quantity += applyTo;
                    }
                    else
                    {
                        var split = NewBoxLine(ctx.TenantId, ctx.Cart, line.BoxBundleSlotId!.Value,
                            ctx.Variants[line.ProductVariantId], ctx.Products[variant.ProductId], applyTo, priced);
                        // Explicit Add: an entity with a pre-set key discovered via navigation
                        // fixup would be tracked as Modified, not Added. Fixup appends it to
                        // cart.Items, so only backfill the collections it does not own.
                        _dbContext.CartItems.Add(split);
                        if (!ctx.Cart.Items.Contains(split))
                        {
                            ctx.Cart.Items.Add(split);
                        }
                        ctx.BoxLines.Add(split);
                    }
                }
            }
        }, cancellationToken);

    public Task<BoxCartDto> RemoveLineAsync(Guid cartId, Guid lineId, CartAccessContext access, CancellationToken cancellationToken = default)
        => RunAsync(cartId, access, requireOpen: true, touchCart: true, (ctx, ct) =>
        {
            var line = ctx.BoxLines.FirstOrDefault(l => l.Id == lineId)
                ?? throw new NotFoundException($"Cart line '{lineId}' was not found.");
            _dbContext.CartItems.Remove(line);
            return Task.CompletedTask;
        }, cancellationToken);

    public Task<BoxCartDto> ContinueAsync(Guid cartId, CartAccessContext access, CancellationToken cancellationToken = default)
        => RunAsync(cartId, access, requireOpen: true, touchCart: false, (ctx, ct) =>
        {
            // R8 — the full-box gate. Advisory UX; checkout independently re-validates (A10).
            // The persisted size is revalidated too: an admin may have shrunk the plan since
            // this session chose its size (J2) — change the size before continuing.
            ValidateSize(ctx.Plan, ctx.Cart.BoxSize!.Value);
            var units = TotalUnits(ctx.BoxLines);
            if (units != ctx.Cart.BoxSize)
            {
                var shortfall = ctx.Cart.BoxSize!.Value - units;
                throw new StorefrontValidationException(shortfall > 0
                    ? $"R8: the box has {units}/{ctx.Cart.BoxSize} dishes — add {shortfall} more to continue."
                    : $"R8: the box holds {units} dishes for a size of {ctx.Cart.BoxSize} — remove {-shortfall}.");
            }
            if (ctx.UnavailableLineIds.Count > 0)
            {
                throw new StorefrontValidationException(
                    $"R8: {ctx.UnavailableLineIds.Count} line(s) are unavailable — remove them to continue.");
            }
            return Task.CompletedTask;
        }, cancellationToken);

    // ─── The serialized write core (A17) ─────────────────────────────────────

    private sealed class BoxContext
    {
        public required Guid TenantId { get; init; }
        public required Entities.Cart.Cart Cart { get; init; }
        public required BundleSizePlan Plan { get; init; }
        public required List<CartItem> BoxLines { get; init; }
        public required Dictionary<Guid, ProductVariant> Variants { get; init; }
        public required Dictionary<Guid, Product> Products { get; init; }
        public required List<BoxChangeDto> Changes { get; init; }
        public required HashSet<Guid> UnavailableLineIds { get; init; }
        public required Dictionary<Guid, decimal> Available { get; init; }

        /// <summary>Each line's freshly renormalized §12 result — the checkout envelopes.</summary>
        public required Dictionary<Guid, OptionSelectionResult> Priced { get; init; }
    }

    private async Task<BoxCartDto> RunAsync(
        Guid cartId,
        CartAccessContext access,
        bool requireOpen,
        bool touchCart,
        Func<BoxContext, CancellationToken, Task>? mutate,
        CancellationToken cancellationToken)
    {
        try
        {
            return await AttemptAsync(cartId, access, requireOpen, touchCart, mutate, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A17 — the loser revalidates against fresh state and retries once; a second conflict
            // surfaces as the mapped 409.
            return await AttemptAsync(cartId, access, requireOpen, touchCart, mutate, cancellationToken);
        }
    }

    private async Task<BoxCartDto> AttemptAsync(
        Guid cartId,
        CartAccessContext access,
        bool requireOpen,
        bool touchCart,
        Func<BoxContext, CancellationToken, Task>? mutate,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var strategy = _dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async ct =>
        {
            // A replayed attempt must not resubmit entities a failed attempt added or mutated —
            // and a detached instance must also leave its cart's Items collection, or the fresh
            // query would materialise a tracked TWIN of the same row beside the stale ghost and
            // the reload below would hit an identity-map conflict.
            foreach (var entry in _dbContext.ChangeTracker.Entries<CartItem>()
                         .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                         .ToList())
            {
                entry.State = EntityState.Detached;
            }
            foreach (var trackedCart in _dbContext.ChangeTracker.Entries<Entities.Cart.Cart>().ToList())
            {
                trackedCart.Entity.Items.RemoveAll(i => _dbContext.Entry(i).State == EntityState.Detached);
            }

            var cart = await _dbContext.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.Id == cartId && c.TenantId == tenantId, ct);
            if (cart is not null)
            {
                await _dbContext.Entry(cart).ReloadAsync(ct);
                // Snapshot first: reloading an item triggers relationship fixup, which can mutate
                // cart.Items mid-enumeration.
                foreach (var item in cart.Items.ToList())
                {
                    await _dbContext.Entry(item).ReloadAsync(ct);
                }
            }

            // R10 — an unknown cart and an unauthorized one are the same 404; no oracle.
            if (cart is null || cart.BoxBundleProductId is null || !CartAccess.IsAuthorized(cart, access))
            {
                throw new NotFoundException($"Cart '{cartId}' was not found.");
            }

            if (requireOpen && (cart.Status != CartStatuses.Open || cart.OrderId is not null))
            {
                // R9 — checkout deliberately leaves a cart Open (OrderId set) until payment
                // completes; its order, reservation and payment amount are already fixed.
                throw new StorefrontValidationException(
                    "R9: this box has been checked out and can no longer be edited.");
            }

            var plan = await LoadPlanAsync(tenantId, cart.BoxBundleProductId.Value, ct);

            // §8 — drift repair on every load, but only while the session is still editable: a
            // checked-out box is a record, not a session, and must not be rewritten under its order.
            var editable = cart.Status == CartStatuses.Open && cart.OrderId is null;

            // The live-plan currency guard protects EDITABLE sessions from quoting against a
            // repriced plan; a closed cart's figures are frozen in its charge summary, and an
            // allowed later currency change must not brick the historical view (J7).
            if (editable && !string.Equals(plan.Currency, cart.Currency, StringComparison.Ordinal))
            {
                throw new StorefrontValidationException(
                    $"This box session is denominated in {cart.Currency} but the plan now prices in {plan.Currency}; " +
                    "start a new box.");
            }

            var context = await BuildContextAsync(tenantId, cart, plan, ct);
            if (editable)
            {
                await ApplyDriftAsync(context, ct);
            }
            // Guards-only pass: increase guards consult the flags, but the authoritative flags
            // and change entries are computed AFTER the mutation — a quantity decrease can make
            // an over-demanded variant available again (J5).
            await FlagUnavailableAsync(context, emitChanges: false, ct);

            if (mutate is not null)
            {
                await mutate(context, ct);
            }

            // K1 — a line the mutation deleted must leave the working set before the
            // authoritative pass, or its quantity still counts toward cart-wide demand and can
            // wrongly flag the surviving lines.
            context.BoxLines.RemoveAll(l =>
                l.IsDeleted || _dbContext.Entry(l).State == EntityState.Deleted);
            context.UnavailableLineIds.Clear();
            context.Available.Clear();
            await FlagUnavailableAsync(context, emitChanges: true, ct);

            await using var transaction = _dbContext.Database.IsRelational()
                ? await _dbContext.Database.BeginTransactionAsync(ct)
                : null;

            if (touchCart)
            {
                // A17 — capacity-affecting writes contend on the cart row: two adds racing into
                // the last space touch different line rows, so without this both would commit.
                _dbContext.Entry(context.Cart).State = EntityState.Modified;
            }

            await _dbContext.SaveChangesAsync(ct);
            if (transaction is not null)
            {
                await transaction.CommitAsync(ct);
            }

            return await BuildDtoAsync(tenantId, context.Cart, plan, context.Changes, cartToken: null, ct);
        }, cancellationToken);
    }

    private async Task<BoxContext> BuildContextAsync(
        Guid tenantId, Entities.Cart.Cart cart, BundleSizePlan plan, CancellationToken ct)
    {
        var boxLines = cart.Items
            .Where(i => i.LineKind == CartLineKinds.BoxDish && i.BoxBundleSlotId is not null)
            .OrderBy(i => i.CreatedAt).ThenBy(i => i.Id)
            .ToList();

        var variantIds = boxLines.Select(l => l.ProductVariantId).Distinct().ToList();
        var variants = await _dbContext.ProductVariants
            .AsNoTracking()
            .Where(v => v.TenantId == tenantId && variantIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, ct);
        var productIds = variants.Values.Select(v => v.ProductId).Distinct().ToList();
        var products = await _dbContext.Products
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        return new BoxContext
        {
            TenantId = tenantId,
            Cart = cart,
            Plan = plan,
            BoxLines = boxLines,
            Variants = variants,
            Products = products,
            Changes = [],
            UnavailableLineIds = [],
            Available = [],
            Priced = [],
        };
    }

    // ─── §8 drift repair ─────────────────────────────────────────────────────

    private async Task ApplyDriftAsync(BoxContext context, CancellationToken ct)
    {
        foreach (var line in context.BoxLines.ToList())
        {
            if (!context.BoxLines.Contains(line))
            {
                continue;   // absorbed by an earlier remap-then-merge in this same pass
            }
            if (!context.Variants.TryGetValue(line.ProductVariantId, out var variant))
            {
                continue;   // variant row gone entirely — the availability pass flags the line
            }

            var stored = line.PersonalisationJson ?? "{}";
            var renormalized = await _selections.RenormalizeStoredAsync(
                variant.ProductId, stored, context.Cart.Currency, ct);
            var result = renormalized.Result;

            var priceDelta = result.Adjustment - (line.PersonalisationAdjustment ?? 0m);
            foreach (var drift in renormalized.Drift)
            {
                context.Changes.Add(new BoxChangeDto(
                    line.Id, drift.GroupKey, drift.FromChoiceKey, drift.ToChoiceKey, drift.Reason,
                    priceDelta == 0m ? null : priceDelta));
            }

            context.Priced[line.Id] = result;
            var selectionChanged = !string.Equals(result.CanonicalSelectionJson, line.PersonalisationJson, StringComparison.Ordinal);

            // R7 — snapshots are display cache, re-derived on every load from the live catalogue.
            ApplySelection(line, result);

            if (selectionChanged)
            {
                // The merge key changed with the selection — the remapped line may now equal
                // another line (R6); the change entry says so.
                var target = context.BoxLines.FirstOrDefault(l => l.Id != line.Id
                    && l.BoxBundleSlotId == line.BoxBundleSlotId
                    && l.ProductVariantId == line.ProductVariantId
                    && string.Equals(l.PersonalisationJson, result.CanonicalSelectionJson, StringComparison.Ordinal));
                if (target is not null)
                {
                    target.Quantity += line.Quantity;
                    _dbContext.CartItems.Remove(line);
                    context.BoxLines.Remove(line);
                    context.Changes.Add(new BoxChangeDto(
                        line.Id, null, null, null, BoxChangeReasons.LineMerged, null, target.Id));
                }
            }
        }
    }

    private async Task FlagUnavailableAsync(BoxContext context, bool emitChanges, CancellationToken ct)
    {
        foreach (var line in context.BoxLines.Where(l => !l.IsDeleted).ToList())
        {
            var variant = context.Variants.GetValueOrDefault(line.ProductVariantId);
            var product = variant is null ? null : context.Products.GetValueOrDefault(variant.ProductId);

            var unavailable = variant is null || !variant.IsActive
                || product is null || product.Status != ProductStatuses.Active;

            if (!unavailable)
            {
                var available = await GetAvailableAsync(context, line.ProductVariantId, ct);
                var cartDemand = context.BoxLines
                    .Where(l => l.ProductVariantId == line.ProductVariantId)
                    .Sum(l => l.Quantity);
                unavailable = cartDemand > available;
            }

            if (unavailable)
            {
                context.UnavailableLineIds.Add(line.Id);
                if (emitChanges)
                {
                    context.Changes.Add(new BoxChangeDto(
                        line.Id, null, null, null, BoxChangeReasons.Unavailable));
                }
            }
        }
    }

    // ─── Line construction (shared by add, split and create-with-first-line) ─

    private async Task AddLineCoreAsync(
        Guid tenantId,
        Entities.Cart.Cart cart,
        BundleSizePlan plan,
        AddBoxLineCommand command,
        CancellationToken ct)
    {
        if (command.Quantity < 1)
        {
            throw new StorefrontValidationException("R12: quantity must be at least 1.");
        }

        var variant = await _dbContext.ProductVariants
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == command.ProductVariantId && v.TenantId == tenantId, ct)
            ?? throw new NotFoundException($"Product variant '{command.ProductVariantId}' was not found.");
        var product = await _dbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == variant.ProductId && p.TenantId == tenantId, ct)
            ?? throw new NotFoundException($"Product '{variant.ProductId}' was not found.");

        // R5 — a retired SKU can never ride into a box.
        if (!variant.IsActive || product.Status != ProductStatuses.Active)
        {
            throw new StorefrontValidationException("R5: this dish is not currently available.");
        }

        var slotId = await ResolveSlotAsync(tenantId, cart.BoxBundleProductId!.Value, variant, product, command.BundleSlotId, ct);

        // R4 — Spec 066 owns selection validity and pricing; V10 rejects mis-denominated groups.
        var priced = await _selections.NormalizeAndPriceAsync(
            product.Id, command.Personalisation, cart.Currency, ct);

        var boxLines = cart.Items
            .Where(i => i.LineKind == CartLineKinds.BoxDish && i.BoxBundleSlotId is not null)
            .ToList();

        // R3 — no overflow path exists server-side; growing first is the only route.
        var units = (int)boxLines.Sum(l => l.Quantity);
        if (units + command.Quantity > cart.BoxSize)
        {
            throw new StorefrontValidationException(
                $"R3: adding {command.Quantity} would exceed the box size of {cart.BoxSize} " +
                $"({units} already selected) — grow the box first.");
        }

        // R5 — aggregate, non-reserving availability: the resulting cart-wide demand for the
        // variant must not exceed what is available; repeated merges cannot creep past it.
        var cartWide = (int)boxLines.Where(l => l.ProductVariantId == variant.Id).Sum(l => l.Quantity);
        var available = await _inventory.GetAvailableAsync(variant.Id, ct);
        if (cartWide + command.Quantity > available)
        {
            throw new StorefrontValidationException(
                $"R5: only {available:0.##} of this dish is available; the box already holds {cartWide}.");
        }

        // R6 — identical (kind, slot, variant, canonical selection) merges, never duplicates.
        var target = boxLines.FirstOrDefault(l => l.BoxBundleSlotId == slotId
            && l.ProductVariantId == variant.Id
            && string.Equals(l.PersonalisationJson, priced.CanonicalSelectionJson, StringComparison.Ordinal));
        if (target is not null)
        {
            target.Quantity += command.Quantity;
            ApplySelection(target, priced);
            return;
        }

        var newLine = NewBoxLine(tenantId, cart, slotId, variant, product, command.Quantity, priced);
        // Explicit Add unless the whole cart graph is itself being added (create-with-first-line):
        // a pre-set key discovered via fixup from an UNCHANGED parent tracks as Modified. The Add
        // fixes up cart.Items itself; the manual append covers only the Added-graph path.
        if (_dbContext.Entry(cart).State != EntityState.Added)
        {
            _dbContext.CartItems.Add(newLine);
        }
        if (!cart.Items.Contains(newLine))
        {
            cart.Items.Add(newLine);
        }
    }

    private async Task<Guid> ResolveSlotAsync(
        Guid tenantId, Guid bundleProductId, ProductVariant variant, Product product, Guid? named, CancellationToken ct)
    {
        var slots = await _dbContext.BundleSlots
            .AsNoTracking()
            .Include(s => s.Options)
            .Where(s => s.TenantId == tenantId && s.BundleProductId == bundleProductId)
            .ToListAsync(ct);

        bool Eligible(BundleSlot slot) => slot.Options.Count > 0
            ? slot.Options.Any(o => o.ProductVariantId == variant.Id)
            : slot.FromCategoryId is not { } category || product.CategoryId == category;

        if (named is { } explicitSlot)
        {
            var slot = slots.FirstOrDefault(s => s.Id == explicitSlot)
                ?? throw new NotFoundException($"Bundle slot '{explicitSlot}' was not found.");
            if (!Eligible(slot))
            {
                throw new StorefrontValidationException($"R5: this dish is not eligible for slot '{slot.Name}'.");
            }
            return slot.Id;
        }

        var eligible = slots.Where(Eligible).ToList();
        return eligible.Count switch
        {
            1 => eligible[0].Id,
            0 => throw new StorefrontValidationException("R5: this dish fits no slot of the box."),
            _ => throw new StorefrontValidationException(
                $"R5: this dish is eligible for {eligible.Count} slots — name the slot explicitly."),
        };
    }

    private static CartItem NewBoxLine(
        Guid tenantId,
        Entities.Cart.Cart cart,
        Guid slotId,
        ProductVariant variant,
        Product product,
        int quantity,
        OptionSelectionResult priced)
    {
        var line = new CartItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CartId = cart.Id,
            ProductVariantId = variant.Id,
            LineKind = CartLineKinds.BoxDish,
            BoxBundleSlotId = slotId,
            Quantity = quantity,
            // No standalone dish price exists in a box (§2) — the box price is the container's.
            UnitPriceSnapshot = 0m,
            Sku = variant.Sku,
            NameSnapshot = product.Name,
        };
        ApplySelection(line, priced);
        return line;
    }

    private static void ApplySelection(CartItem line, OptionSelectionResult priced)
    {
        line.PersonalisationJson = priced.CanonicalSelectionJson;
        line.PersonalisationSummary = TruncateSummary(priced.Summary);
        line.PersonalisationAdjustment = priced.Adjustment;
        line.UnitSurcharge = priced.UnitSurcharge ?? 0m;
    }

    /// <summary>The summary is a bounded display cache (512 per §13); selections themselves are
    /// unbounded, so a wide multi-select could otherwise fail the write only on SQL Server. The
    /// full-fidelity text lives in the §12 envelope's display entries.</summary>
    internal static string TruncateSummary(string summary)
        => summary.Length <= 512 ? summary : summary[..511] + "\u2026";

    // ─── Guards shared by quantity increases ─────────────────────────────────

    private Task EnsureLineIsAvailableForIncreaseAsync(BoxContext context, CartItem line, CancellationToken ct)
        => context.UnavailableLineIds.Contains(line.Id)
            ? throw new StorefrontValidationException(
                "R5: this line is unavailable — remove it or wait for it to return.")
            : Task.CompletedTask;

    private static void EnsureCapacity(BoxContext context, int addedUnits)
    {
        var units = TotalUnits(context.BoxLines);
        if (units + addedUnits > context.Cart.BoxSize)
        {
            throw new StorefrontValidationException(
                $"R3: the box size is {context.Cart.BoxSize} and it already holds {units} — grow the box first.");
        }
    }

    private async Task EnsureAvailabilityAsync(BoxContext context, Guid variantId, int addedUnits, CancellationToken ct)
    {
        var cartWide = context.BoxLines.Where(l => l.ProductVariantId == variantId).Sum(l => l.Quantity);
        var available = await GetAvailableAsync(context, variantId, ct);
        if (cartWide + addedUnits > available)
        {
            throw new StorefrontValidationException(
                $"R5: only {available:0.##} of this dish is available; the box already holds {cartWide:0.##}.");
        }
    }

    private async Task<decimal> GetAvailableAsync(BoxContext context, Guid variantId, CancellationToken ct)
    {
        if (!context.Available.TryGetValue(variantId, out var available))
        {
            available = await _inventory.GetAvailableAsync(variantId, ct);
            context.Available[variantId] = available;
        }
        return available;
    }

    private static int TotalUnits(IEnumerable<CartItem> boxLines) => (int)boxLines.Sum(l => l.Quantity);

    // ─── Checkout support (Spec 068 §9) ──────────────────────────────────────

    public async Task<BoxCheckoutShape> PrepareForCheckoutAsync(Entities.Cart.Cart cart, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        // The container itself must still be sellable — the per-line pass checks only dishes,
        // so a withdrawn box product would otherwise reserve stock and initiate payment (J6).
        var bundleProduct = await _dbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == cart.BoxBundleProductId && p.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException($"Product '{cart.BoxBundleProductId}' was not found.");
        if (bundleProduct.Kind != ProductKinds.Bundle
            || bundleProduct.Status != ProductStatuses.Active
            || bundleProduct.BundlePricingMode != BundlePricingModes.SizeTiered)
        {
            throw new StorefrontValidationException(
                "This box product is no longer available for checkout.");
        }

        var plan = await LoadPlanAsync(tenantId, cart.BoxBundleProductId!.Value, cancellationToken);

        // An admin may have shrunk the plan since this session chose its size; an out-of-range
        // size must never reach pricing or payment — below the new minimum the formula can even
        // quote zero or negative (J2).
        ValidateSize(plan, cart.BoxSize!.Value);

        var context = await BuildContextAsync(tenantId, cart, plan, cancellationToken);

        await ApplyDriftAsync(context, cancellationToken);
        await FlagUnavailableAsync(context, emitChanges: true, cancellationToken);

        // A18 — any drift or unavailable line stops checkout BEFORE anything is reserved or
        // created: persist the repair so the refreshed state is durable, then 409 with it. A
        // stale client that skipped continue must explicitly review a changed meal or price.
        if (context.Changes.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            throw new BoxCheckoutDriftException(
                await BuildDtoAsync(tenantId, cart, plan, context.Changes, cartToken: null, cancellationToken));
        }

        // R8 — checkout is the enforcement; the continue gate was advisory.
        var units = TotalUnits(context.BoxLines);
        if (units != cart.BoxSize)
        {
            throw new StorefrontValidationException(
                $"R8: the box has {units}/{cart.BoxSize} dishes; it must be full to check out.");
        }

        var boxPrice = BoxPricing.BoxPrice(plan, cart.BoxSize!.Value);
        var personalisation = context.BoxLines.Sum(l => (l.PersonalisationAdjustment ?? 0m) * l.Quantity);
        var surcharges = context.BoxLines.Sum(l => (l.UnitSurcharge ?? 0m) * l.Quantity);
        var deliveryCharged = await ReadAmountAsync(CommerceSettingNames.StorefrontDeliveryChargedAmount, tenantId, cancellationToken);

        // K5 — below-default choices are legitimate (A7) but their aggregate must never drive
        // the goods total to zero or below: Finance would reject the payment only after
        // reservation and order creation had already left durable partial state.
        if (boxPrice + personalisation + surcharges <= 0)
        {
            throw new StorefrontValidationException(
                "This box's personalisation reduces its total to zero or below; it cannot be checked out.");
        }

        var envelope = JsonSerializer.Serialize(new
        {
            box = new
            {
                bundleProductId = cart.BoxBundleProductId,
                size = cart.BoxSize!.Value,
                quote = new
                {
                    boxPrice,
                    personalisation,
                    unitSurcharges = surcharges,
                    currency = cart.Currency,
                },
            },
        });

        var lines = context.BoxLines
            .Select(l => (Line: l, Priced: context.Priced[l.Id]))
            .ToList();

        return new BoxCheckoutShape(
            boxPrice + personalisation + surcharges,
            boxPrice,
            personalisation,
            surcharges,
            deliveryCharged,
            bundleProduct.Slug,
            cart.BoxSize!.Value,
            envelope,
            lines);
    }

    // ─── Plan, quote and mapping ─────────────────────────────────────────────

    private async Task<BundleSizePlan> LoadPlanAsync(Guid tenantId, Guid bundleProductId, CancellationToken ct)
        => await _dbContext.BundleSizePlans
            .AsNoTracking()
            .Include(p => p.Presets)
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.BundleProductId == bundleProductId, ct)
            ?? throw new NotFoundException("This bundle has no size plan.");

    private static void ValidateSize(BundleSizePlan plan, int size)
    {
        if (!BoxPricing.IsValidSize(plan, size))
        {
            throw new StorefrontValidationException(
                $"R1: box sizes run from {plan.MinSize} to {plan.MaxSize}; {size} is outside the plan.");
        }
    }

    private async Task<BoxCartDto> BuildDtoAsync(
        Guid tenantId,
        Entities.Cart.Cart cart,
        BundleSizePlan plan,
        List<BoxChangeDto> changes,
        string? cartToken,
        CancellationToken ct)
    {
        var boxLines = cart.Items
            .Where(i => i.LineKind == CartLineKinds.BoxDish && i.BoxBundleSlotId is not null && !i.IsDeleted)
            .OrderBy(i => i.CreatedAt).ThenBy(i => i.Id)
            .ToList();

        var variantIds = boxLines.Select(l => l.ProductVariantId).Distinct().ToList();
        var variants = await _dbContext.ProductVariants
            .AsNoTracking()
            .Where(v => v.TenantId == tenantId && variantIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, ct);

        var unavailableIds = changes
            .Where(c => c.Reason == BoxChangeReasons.Unavailable && c.LineId is not null)
            .Select(c => c.LineId!.Value)
            .ToHashSet();

        var lines = boxLines.Select(l => new BoxLineDto(
            l.Id,
            variants.TryGetValue(l.ProductVariantId, out var v) ? v.ProductId : Guid.Empty,
            l.ProductVariantId,
            l.NameSnapshot,
            (int)l.Quantity,
            ParseSelection(l.PersonalisationJson),
            l.PersonalisationSummary ?? string.Empty,
            string.IsNullOrEmpty(l.PersonalisationSummary),
            l.PersonalisationAdjustment ?? 0m,
            l.UnitSurcharge ?? 0m,
            l.BoxBundleSlotId!.Value,
            unavailableIds.Contains(l.Id))).ToList();

        // A closed cart's quote pins to its durable charge summary — a later plan price edit
        // must not display a figure different from what was actually charged (J7).
        OrderChargeSummary? summary = null;
        if (cart.OrderId is { } orderId)
        {
            summary = await _dbContext.OrderChargeSummaries
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.OrderId == orderId, ct);
        }

        var quote = await BuildQuoteAsync(tenantId, cart, plan, boxLines, summary, ct);

        return new BoxCartDto(
            new BoxDto(cart.Id, cart.BoxBundleProductId!.Value, cart.BoxSize!.Value, cart.Currency, lines),
            quote,
            changes,
            cartToken);
    }

    private async Task<BoxQuoteDto> BuildQuoteAsync(
        Guid tenantId,
        Entities.Cart.Cart cart,
        BundleSizePlan plan,
        IReadOnlyList<CartItem> boxLines,
        OrderChargeSummary? summary,
        CancellationToken ct)
    {
        var size = cart.BoxSize!.Value;
        var personalisation = boxLines.Sum(l => (l.PersonalisationAdjustment ?? 0m) * l.Quantity);
        var surcharges = boxLines.Sum(l => (l.UnitSurcharge ?? 0m) * l.Quantity);
        var deliveryList = await ReadAmountAsync(CommerceSettingNames.StorefrontDeliveryListAmount, tenantId, ct);

        // Frozen view: line snapshots are no longer drift-repaired, so the goods split derives
        // from the recorded subtotal, and delivery is whatever the payment actually included.
        var boxPrice = summary is not null
            ? summary.Subtotal - personalisation - surcharges
            : BoxPricing.BoxPrice(plan, size);
        var deliveryCharged = summary is not null
            ? summary.Total - (summary.Subtotal - summary.DiscountTotal + summary.TaxTotal)
            : await ReadAmountAsync(CommerceSettingNames.StorefrontDeliveryChargedAmount, tenantId, ct);

        var components = new List<QuoteComponentDto>
        {
            new(QuoteComponentKeys.BoxPrice, boxPrice),
            new(QuoteComponentKeys.Personalisation, personalisation),
            new(QuoteComponentKeys.UnitSurcharges, surcharges),
            new(QuoteComponentKeys.DeliveryCharged, deliveryCharged),
        };

        // K3 — a frozen view must total exactly what was charged: the recorded discount and tax
        // join as components (zero-amount components may be omitted per §7, and clients iterate
        // rather than reconstruct, so these are additive).
        if (summary is not null && summary.DiscountTotal != 0)
        {
            components.Add(new QuoteComponentDto("discount", -summary.DiscountTotal));
        }
        if (summary is not null && summary.TaxTotal != 0)
        {
            components.Add(new QuoteComponentDto("tax", summary.TaxTotal));
        }

        var units = TotalUnits(boxLines);
        return new BoxQuoteDto(
            components,
            deliveryList,
            components.Sum(c => c.Amount),   // A24 — the total IS the component sum
            cart.Currency,
            units,
            size,
            Math.Max(0, size - units),
            units == size);
    }

    private static JsonElement? ParseSelection(string? canonicalJson)
    {
        if (string.IsNullOrWhiteSpace(canonicalJson))
        {
            return null;
        }
        using var document = JsonDocument.Parse(canonicalJson);
        return document.RootElement.Clone();
    }

    private async Task<decimal> ReadAmountAsync(string key, Guid tenantId, CancellationToken ct)
    {
        var raw = await _settingStore.GetTenantValueAsync(key, tenantId, ct)
            ?? await _settings.GetAsync(key, ct);
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) && amount >= 0
            ? amount
            : 0m;
    }
}
