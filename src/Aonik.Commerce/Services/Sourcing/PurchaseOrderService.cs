using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Sourcing;
using Aonik.Commerce.Entities.Inventory;
using Aonik.Commerce.Entities.Sourcing;
using Aonik.Commerce.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Ordering;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.Sourcing;

/// <summary>
/// Purchase orders over the shared Order spine (Spec 053 §10–§13). Commerce holds only the
/// supplier master data; the PO itself is an <c>Order</c> created/read/transitioned through
/// <see cref="IOrderService"/> — never a parallel Commerce order concept. Because the spine's
/// <c>TransitionAsync</c> accepts any status string (no state machine), the allowed PO
/// transitions are enforced HERE, over the existing <see cref="OrderStatusCodes"/>.
/// </summary>
internal sealed class PurchaseOrderService : IPurchaseOrderService
{
    private readonly CommerceDbContext _dbContext;
    private readonly IOrderService _orders;
    private readonly ITenantProvider _tenantProvider;

    public PurchaseOrderService(CommerceDbContext dbContext, IOrderService orders, ITenantProvider tenantProvider)
    {
        _dbContext = dbContext;
        _orders = orders;
        _tenantProvider = tenantProvider;
    }

    public async Task<OrderDto> CreateAsync(CreatePurchaseOrderCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        if (command.Lines is null || command.Lines.Count == 0)
        {
            throw new ArgumentException("A purchase order requires at least one line.", nameof(command));
        }
        if (command.Lines.Any(l => l.Quantity <= 0))
        {
            throw new ArgumentException("Every purchase-order line quantity must be positive (in the ingredient's base unit).", nameof(command));
        }
        if (command.Lines.Any(l => l.UnitPrice is <= 0))
        {
            throw new ArgumentException("An explicit unit price must be positive.", nameof(command));
        }

        var supplier = await GetActiveSupplierAsync(tenantId, command.SupplierId, cancellationToken);
        var currency = NormalizeCurrency(command.Currency ?? supplier.Currency);

        var ingredientIds = command.Lines.Select(l => l.IngredientId).Distinct().ToList();
        var ingredients = await GetActiveIngredientsAsync(tenantId, ingredientIds, cancellationToken);
        var catalog = await GetCatalogRowsAsync(tenantId, supplier.Id, ingredientIds, cancellationToken);

        // Pass 1 — resolve every line's price up-front (all-or-nothing): an explicit UnitPrice
        // wins; else the catalog's pack economics (PackPrice / PackSize, per base unit); a line
        // with neither is rejected naming the ingredient (§10 — no Spec 051 cost fallback, §12).
        var unpriceable = new List<string>();
        var resolved = new List<(PurchaseOrderLineCommand Line, Ingredient Ingredient, decimal UnitPrice, string? Sku)>();
        foreach (var line in command.Lines)
        {
            var ingredient = ingredients[line.IngredientId];
            catalog.TryGetValue(line.IngredientId, out var catalogRow);

            decimal? unitPrice = line.UnitPrice;
            if (unitPrice is null && catalogRow is not null)
            {
                if (!string.Equals(catalogRow.Currency, currency, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"The catalog price for ingredient '{ingredient.Name}' is in {catalogRow.Currency}, but this " +
                        $"purchase order is in {currency}. Pass an explicit unit price for the line, or order in the catalog currency.");
                }
                unitPrice = DeriveUnitPrice(catalogRow);
            }

            if (unitPrice is null)
            {
                unpriceable.Add(ingredient.Name);
                continue;
            }

            resolved.Add((line, ingredient, unitPrice.Value, catalogRow?.Sku));
        }
        if (unpriceable.Count > 0)
        {
            throw new InvalidOperationException(
                "No unit price could be resolved for: " +
                $"{string.Join(", ", unpriceable.Select(n => $"'{n}'"))}. Pass an explicit unit price, or add the " +
                $"ingredient to supplier '{supplier.Name}'s catalog.");
        }

        var items = resolved
            .Select((r, index) => BuildLine(index, r.Ingredient, r.Line.Quantity, r.UnitPrice, r.Sku, currency))
            .ToList();

        return await _orders.CreateAsync(new CreateOrderCommand(
            OrderType: OrderTypeCodes.PurchaseOrder,
            // The payer is the tenant itself, which has no Party row — PayerPartyId stays null;
            // the outbound direction is carried by the order type, the Supplier role, and the
            // provenance (§11).
            PayerPartyId: null,
            CurrencyIn: currency,
            Items: items,
            IdempotencyKey: command.IdempotencyKey,
            ProvenanceJson: BuildProvenance(supplier, command.Notes, alertIds: null),
            PartyRoles: BuildSupplierRole(supplier)), cancellationToken);
    }

    public async Task<OrderDto> CreateFromShortfallAsync(CreateFromShortfallCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var supplier = await GetActiveSupplierAsync(tenantId, command.SupplierId, cancellationToken);
        var currency = NormalizeCurrency(supplier.Currency);

        // The full catalog for this supplier — both the auto-selection filter and the price source.
        var supplierCatalog = await _dbContext.SupplierIngredients
            .Where(si => si.TenantId == tenantId && si.SupplierId == supplier.Id)
            .ToDictionaryAsync(si => si.IngredientId, cancellationToken);

        // Resolve the source alerts: named ids, or (auto) every ACTIVE (Open/Acknowledged) alert
        // for an ingredient this supplier has a catalog row for (§12). Alerts are loaded TRACKED —
        // they flip to Ordered in this same operation.
        List<LowStockAlert> alerts;
        if (command.AlertIds is { Count: > 0 })
        {
            var alertIds = command.AlertIds.Distinct().ToList();
            alerts = await _dbContext.LowStockAlerts
                .Where(a => a.TenantId == tenantId && alertIds.Contains(a.Id))
                .ToListAsync(cancellationToken);

            var missing = alertIds.Except(alerts.Select(a => a.Id)).ToList();
            if (missing.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Low-stock alert(s) not found: {string.Join(", ", missing.Select(id => $"'{id}'"))}.");
            }
            var inactive = alerts
                .Where(a => a.Status is not (LowStockAlertStatuses.Open or LowStockAlertStatuses.Acknowledged))
                .ToList();
            if (inactive.Count > 0)
            {
                throw new InvalidOperationException(
                    "Only Open or Acknowledged low-stock alerts can seed a purchase order; " +
                    $"{string.Join(", ", inactive.Select(a => $"'{a.Id}' is {a.Status}"))}.");
            }
        }
        else
        {
            var suppliableIngredientIds = supplierCatalog.Keys.ToList();
            alerts = await _dbContext.LowStockAlerts
                .Where(a => a.TenantId == tenantId
                    && suppliableIngredientIds.Contains(a.IngredientId)
                    && (a.Status == LowStockAlertStatuses.Open || a.Status == LowStockAlertStatuses.Acknowledged))
                .ToListAsync(cancellationToken);
            if (alerts.Count == 0)
            {
                throw new InvalidOperationException(
                    $"No active (Open/Acknowledged) low-stock alerts exist for ingredients supplier '{supplier.Name}' can supply.");
            }
        }

        var ingredientIds = alerts.Select(a => a.IngredientId).Distinct().ToList();
        var ingredients = await GetActiveIngredientsAsync(tenantId, ingredientIds, cancellationToken);

        // Pack rounding needs PackSize and the buy price is PackPrice / PackSize — a catalog row
        // is REQUIRED on this path (no Spec 051 cost fallback; the explicit-lines path covers the
        // no-catalog case with an explicit unit price — §12).
        var uncatalogued = ingredientIds
            .Where(id => !supplierCatalog.ContainsKey(id))
            .Select(id => ingredients[id].Name)
            .ToList();
        if (uncatalogued.Count > 0)
        {
            throw new InvalidOperationException(
                $"Supplier '{supplier.Name}' has no catalog row for: " +
                $"{string.Join(", ", uncatalogued.Select(n => $"'{n}'"))}. Add the ingredient(s) to the supplier's " +
                "catalog, or create the purchase order from explicit lines with an explicit unit price.");
        }
        var mispriced = ingredientIds
            .Where(id => !string.Equals(supplierCatalog[id].Currency, currency, StringComparison.Ordinal))
            .Select(id => ingredients[id].Name)
            .ToList();
        if (mispriced.Count > 0)
        {
            throw new InvalidOperationException(
                $"The catalog price is not in the supplier currency ({currency}) for: " +
                $"{string.Join(", ", mispriced.Select(n => $"'{n}'"))}. Align the catalog currency, or create the " +
                "purchase order from explicit lines.");
        }

        // Quantity per alert (base units): the level's ReorderQuantity when set (the operator's
        // explicit suggestion, taken as-is), else the alert-snapshot shortfall
        // (ReorderPoint − AvailableAtRaise — the figures the operator is acting on) rounded UP to
        // whole packs, minimum one pack (§12).
        var reorderQuantities = await GetReorderQuantitiesAsync(tenantId, ingredientIds, cancellationToken);

        var items = new List<OrderItemCommand>();
        var index = 0;
        foreach (var alert in alerts.OrderBy(a => ingredients[a.IngredientId].Name))
        {
            var ingredient = ingredients[alert.IngredientId];
            var catalogRow = supplierCatalog[alert.IngredientId];

            decimal quantity;
            if (reorderQuantities.TryGetValue(alert.IngredientId, out var reorderQuantity))
            {
                quantity = reorderQuantity;
            }
            else
            {
                var shortfall = Math.Max(0m, alert.ReorderPoint - alert.AvailableAtRaise);
                var packs = Math.Max(1m, Math.Ceiling(shortfall / catalogRow.PackSize));
                quantity = packs * catalogRow.PackSize;
            }

            items.Add(BuildLine(index++, ingredient, quantity, DeriveUnitPrice(catalogRow), catalogRow.Sku, currency));
        }

        var order = await _orders.CreateAsync(new CreateOrderCommand(
            OrderType: OrderTypeCodes.PurchaseOrder,
            PayerPartyId: null,
            CurrencyIn: currency,
            Items: items,
            IdempotencyKey: command.IdempotencyKey,
            ProvenanceJson: BuildProvenance(supplier, command.Notes, alerts.Select(a => a.Id).ToList()),
            PartyRoles: BuildSupplierRole(supplier)), cancellationToken);

        // Flip the source alerts to Ordered (Spec 052's constant) — ends their active cycle so the
        // scan stops refreshing them. Done after the order exists: if this save failed, the alerts
        // would simply stay active and re-flag, which is the safe direction.
        foreach (var alert in alerts)
        {
            alert.Status = LowStockAlertStatuses.Ordered;
        }
        await _dbContext.SaveChangesAsync(cancellationToken);

        return order;
    }

    public async Task<OrderDto> SubmitAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await GetPurchaseOrderAsync(orderId, cancellationToken);
        if (!string.Equals(order.Status, OrderStatusCodes.Draft, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Purchase order '{orderId}' is {order.Status}; only a Draft purchase order can be submitted.");
        }

        // The spine records the transition + history + OrderStatusChangedEvent; the guard above is
        // the PO state machine (the spine itself enforces none — §13).
        return await _orders.TransitionAsync(orderId, OrderStatusCodes.Pending, "Submitted to supplier", cancellationToken);
    }

    public async Task<OrderDto> CancelAsync(Guid orderId, string? reason = null, CancellationToken cancellationToken = default)
    {
        var order = await GetPurchaseOrderAsync(orderId, cancellationToken);
        if (order.Status is not (OrderStatusCodes.Draft or OrderStatusCodes.Pending))
        {
            throw new InvalidOperationException(
                $"Purchase order '{orderId}' is {order.Status}; only a Draft or Pending purchase order can be cancelled.");
        }

        return await _orders.TransitionAsync(
            orderId, OrderStatusCodes.Cancelled, reason ?? "Cancelled before receipt", cancellationToken);
    }

    public Task<PagedResult<OrderSummary>> ListAsync(string? status = null, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
        => _orders.ListAsync(
            new ListOrdersQuery(OrderType: OrderTypeCodes.PurchaseOrder, Status: status, PageNumber: pageNumber, PageSize: pageSize),
            cancellationToken);

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────

    private async Task<OrderDto> GetPurchaseOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await _orders.GetAsync(orderId, cancellationToken)
            ?? throw new KeyNotFoundException($"Order '{orderId}' was not found.");
        if (!string.Equals(order.OrderType, OrderTypeCodes.PurchaseOrder, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Order '{orderId}' is a {order.OrderType} order, not a purchase order.");
        }
        return order;
    }

    private async Task<Supplier> GetActiveSupplierAsync(Guid tenantId, Guid supplierId, CancellationToken cancellationToken)
    {
        var supplier = await _dbContext.Suppliers
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == supplierId && s.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Supplier '{supplierId}' was not found.");
        if (!supplier.IsActive)
        {
            throw new InvalidOperationException($"Supplier '{supplier.Name}' is inactive; purchase orders require an active supplier.");
        }
        return supplier;
    }

    private async Task<Dictionary<Guid, Ingredient>> GetActiveIngredientsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> ingredientIds, CancellationToken cancellationToken)
    {
        var ingredients = await _dbContext.Ingredients
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && ingredientIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, cancellationToken);

        var missing = ingredientIds.Where(id => !ingredients.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Ingredient(s) not found: {string.Join(", ", missing.Select(id => $"'{id}'"))}.");
        }
        var inactive = ingredients.Values.Where(i => !i.IsActive).Select(i => i.Name).ToList();
        if (inactive.Count > 0)
        {
            throw new InvalidOperationException(
                $"Inactive ingredient(s) cannot be ordered: {string.Join(", ", inactive.Select(n => $"'{n}'"))}.");
        }

        return ingredients;
    }

    private async Task<Dictionary<Guid, SupplierIngredient>> GetCatalogRowsAsync(
        Guid tenantId, Guid supplierId, IReadOnlyCollection<Guid> ingredientIds, CancellationToken cancellationToken)
        => await _dbContext.SupplierIngredients
            .AsNoTracking()
            .Where(si => si.TenantId == tenantId && si.SupplierId == supplierId && ingredientIds.Contains(si.IngredientId))
            .ToDictionaryAsync(si => si.IngredientId, cancellationToken);

    private async Task<Dictionary<Guid, decimal>> GetReorderQuantitiesAsync(
        Guid tenantId, IReadOnlyCollection<Guid> ingredientIds, CancellationToken cancellationToken)
    {
        var levels = await _dbContext.InventoryLevels
            .AsNoTracking()
            .Where(l => l.TenantId == tenantId
                && l.StockItemKind == StockItemKinds.Ingredient
                && l.IngredientId != null
                && ingredientIds.Contains(l.IngredientId!.Value))
            .ToListAsync(cancellationToken);

        return levels
            .Where(l => l.ReorderQuantity is > 0)
            .GroupBy(l => l.IngredientId!.Value)
            .ToDictionary(g => g.Key, g => g.Max(l => l.ReorderQuantity!.Value));
    }

    /// <summary>The per-base-unit price from a catalog row's pack economics, rounded to the 4
    /// decimal places the money columns store — so the persisted UnitPrice × Quantity stays
    /// consistent with the persisted AmountIn.</summary>
    private static decimal DeriveUnitPrice(SupplierIngredient catalogRow)
        => decimal.Round(catalogRow.PackPrice / catalogRow.PackSize, 4);

    /// <summary>The §10 line mapping: PO line → <c>OrderItem</c> columns. <c>ProductId</c> is the
    /// documented soft-ref reinterpretation carrying the <c>IngredientId</c> (opaque Guid, no FK);
    /// the <c>DetailsJson</c> discriminator makes the reuse self-describing; the FX/remittance
    /// columns are simply left unset.</summary>
    private static OrderItemCommand BuildLine(
        int index, Ingredient ingredient, decimal quantity, decimal unitPrice, string? sku, string currency)
        => new(
            ItemType: OrderTypeCodes.PurchaseOrder,
            ItemIndex: index,
            AmountIn: quantity * unitPrice,
            CurrencyIn: currency,
            Quantity: quantity,
            UnitPrice: unitPrice,
            ProductId: ingredient.Id,
            Sku: sku,
            DetailsJson: JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["kind"] = "purchase-order-line",
                ["ingredientId"] = ingredient.Id,
                ["unit"] = ingredient.BaseUnit,
            }));

    /// <summary>Supplier identity always travels in the order's provenance (§11), so the PO is
    /// self-describing whether or not the supplier is party-linked.</summary>
    private static string BuildProvenance(Supplier supplier, string? notes, IReadOnlyList<Guid>? alertIds)
    {
        var provenance = new Dictionary<string, object?>
        {
            ["kind"] = "purchase-order",
            ["supplierId"] = supplier.Id,
            ["supplierName"] = supplier.Name,
        };
        if (!string.IsNullOrWhiteSpace(notes))
        {
            provenance["notes"] = notes.Trim();
        }
        if (alertIds is { Count: > 0 })
        {
            provenance["alertIds"] = alertIds;
        }
        return JsonSerializer.Serialize(provenance);
    }

    /// <summary>The <c>Supplier</c> party role — persisted only when the supplier is soft-linked
    /// to a platform Party (§11); the provenance carries the identity either way.</summary>
    private static IReadOnlyList<OrderPartyRoleCommand>? BuildSupplierRole(Supplier supplier)
        => supplier.PartyId is { } partyId
            ? new[] { new OrderPartyRoleCommand(partyId, OrderPartyRoleCodes.Supplier) }
            : null;

    private static string NormalizeCurrency(string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("Currency is required (ISO 4217, e.g. NGN).");
        }
        return currency.Trim().ToUpperInvariant();
    }
}
