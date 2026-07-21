using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.Catalog;

/// <summary>Selection validation, canonicalisation and pricing (Spec 066 §7–§9).</summary>
internal sealed class OptionSelectionService : IOptionSelectionService
{
    private readonly CommerceDbContext _dbContext;
    private readonly IProductOptionService _optionService;
    private readonly ITenantProvider _tenantProvider;

    public OptionSelectionService(
        CommerceDbContext dbContext,
        IProductOptionService optionService,
        ITenantProvider tenantProvider)
    {
        _dbContext = dbContext;
        _optionService = optionService;
        _tenantProvider = tenantProvider;
    }

    public Task<OptionSelectionResult> NormalizeAndPriceAsync(
        Guid productId, JsonElement? selection, string currency, CancellationToken cancellationToken = default)
        => ResolveAsync(productId, selection, currency, cancellationToken);

    public Task<OptionSelectionResult> NormalizeAsync(
        Guid productId, JsonElement? selection, CancellationToken cancellationToken = default)
        => ResolveAsync(productId, selection, currency: null, cancellationToken);

    public async Task<StoredSelectionResult> RenormalizeStoredAsync(
        Guid productId, string canonicalSelectionJson, string currency, CancellationToken cancellationToken = default)
    {
        var stored = CanonicalSelection.ParseStored(canonicalSelectionJson);
        var groups = await _optionService.GetEffectiveOptionsAsync(productId, cancellationToken);
        var drift = new List<SelectionDrift>();

        var resolved = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        // Groups the product no longer offers are dropped, not remapped: there is no effective
        // default to remap to, so remapping would invent one (Spec 066 §7).
        foreach (var storedGroup in stored.Keys)
        {
            if (!groups.Any(g => g.Key == storedGroup))
            {
                drift.Add(new SelectionDrift(storedGroup, null, null, SelectionDriftReasons.GroupRemoved));
            }
        }

        foreach (var group in groups)
        {
            var isMulti = group.SelectionMode == OptionSelectionModes.Multi;
            var offered = group.Choices.Select(c => c.Key).ToHashSet(StringComparer.Ordinal);

            // A stored canonical selection names every group the product offered when it was saved,
            // so a group missing from it is one the product has gained since. Filling the default is
            // right, but it changes the preparation the customer actually chose — report it rather
            // than mutating the selection silently.
            if (!stored.TryGetValue(group.Key, out var storedValues) || storedValues.Count == 0)
            {
                drift.Add(new SelectionDrift(group.Key, null, group.DefaultChoiceKey, SelectionDriftReasons.GroupAdded));
                resolved[group.Key] = [group.DefaultChoiceKey];
                continue;
            }

            var storedDistinct = storedValues.Distinct(StringComparer.Ordinal).ToList();
            var kept = storedDistinct.Where(v => offered.Contains(v)).ToList();
            var lost = storedDistinct.Where(v => !offered.Contains(v)).ToList();

            foreach (var gone in lost)
            {
                drift.Add(new SelectionDrift(group.Key, gone, group.DefaultChoiceKey, SelectionDriftReasons.OptionRetired));
            }

            // A group tightened Multi → One keeps a single stored choice silently, but cannot keep
            // several. Decide that on what was STORED, not on what survived retirement: filtering
            // first makes ["salmon","retired"] look single-valued, so the mode change goes unnoticed
            // and salmon is kept — while the retirement drift above has already promised a remap to
            // the default. The selection and its own change report would then disagree.
            if (!isMulti && storedDistinct.Count > 1)
            {
                drift.Add(new SelectionDrift(group.Key, string.Join(",", storedDistinct), group.DefaultChoiceKey, SelectionDriftReasons.SelectionModeChanged));
                resolved[group.Key] = [group.DefaultChoiceKey];
                continue;
            }

            if (kept.Count == 0)
            {
                resolved[group.Key] = [group.DefaultChoiceKey];
                continue;
            }

            // A partially retired multi-selection must actually receive the replacement the drift
            // report promises. Without this, ["salmon","retired"] would silently become ["salmon"]
            // while the report claimed a remap to the default — the cart would then be repriced
            // against a selection the customer was never shown.
            if (isMulti && lost.Count > 0 && !kept.Contains(group.DefaultChoiceKey, StringComparer.Ordinal))
            {
                kept.Add(group.DefaultChoiceKey);
            }

            resolved[group.Key] = isMulti ? kept : [kept[0]];
        }

        var result = await BuildResultAsync(productId, groups, resolved, currency, cancellationToken);
        return new StoredSelectionResult(result, drift);
    }

    // ─── Internals ───────────────────────────────────────────────────────────

    private async Task<OptionSelectionResult> ResolveAsync(
        Guid productId, JsonElement? selection, string? currency, CancellationToken cancellationToken)
    {
        var groups = await _optionService.GetEffectiveOptionsAsync(productId, cancellationToken);
        var input = selection is { } element
            ? CanonicalSelection.Parse(element, "V5")
            : new Dictionary<string, List<string>>(StringComparer.Ordinal);

        // V1/V3 — a group the product does not offer is a client error. Distinguish a group that
        // exists in the catalogue but has been retired (V3) from one this product never offered
        // (V1); the caller needs to tell "we withdrew it" from "you made that up".
        foreach (var groupKey in input.Keys)
        {
            if (!groups.Any(g => g.Key == groupKey))
            {
                var existsButRetired = await GroupWithdrawnAsync(groupKey, cancellationToken);
                throw existsButRetired
                    ? new OptionValidationException("V3", $"Option group '{groupKey}' is no longer available.")
                    : new OptionValidationException("V1", $"This product does not offer option group '{groupKey}'.");
            }
        }

        var resolved = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        foreach (var group in groups)
        {
            var isMulti = group.SelectionMode == OptionSelectionModes.Multi;
            var offered = group.Choices.Select(c => c.Key).ToHashSet(StringComparer.Ordinal);

            // Omitted groups fill with the recommended default — a default quick-add legitimately
            // sends no selection at all, so absence is never an error (Spec 066 §7).
            if (!input.TryGetValue(group.Key, out var chosen))
            {
                resolved[group.Key] = [group.DefaultChoiceKey];
                continue;
            }

            // V4 is specifically the multi-select minimum-one rule. For a single-select group an
            // empty array is a shape error — the same family as supplying two — so it is V5.
            // Clients switch on RuleId to decide what to say, so the distinction has to be right.
            if (chosen.Count == 0)
            {
                throw isMulti
                    ? new OptionValidationException(
                        "V4",
                        $"Group '{group.Key}' was supplied with no choice; at least one selection is required.")
                    : new OptionValidationException(
                        "V5",
                        $"Group '{group.Key}' allows one choice but an empty selection was supplied.");
            }

            // V5 — a single-select group accepts a bare string or a one-element array (unwrapped);
            // anything wider is a shape error, never silently truncated.
            if (!isMulti && chosen.Count > 1)
            {
                throw new OptionValidationException(
                    "V5",
                    $"Group '{group.Key}' allows one choice but {chosen.Count} were supplied.");
            }

            foreach (var key in chosen)
            {
                if (!offered.Contains(key))
                {
                    // V2 vs V3: a key that exists in the catalogue but has been deactivated is a
                    // retirement (V3); a key this product simply never offered — even one that is
                    // perfectly valid elsewhere in the tenant catalogue — is V2.
                    var retired = await ChoiceWithdrawnAsync(group.Key, key, cancellationToken);
                    throw retired
                        ? new OptionValidationException("V3", $"Choice '{key}' in group '{group.Key}' is no longer available.")
                        : new OptionValidationException("V2", $"This product does not offer choice '{key}' in group '{group.Key}'.");
                }
            }

            resolved[group.Key] = chosen.Distinct(StringComparer.Ordinal).ToList();
        }

        return await BuildResultAsync(productId, groups, resolved, currency, cancellationToken);
    }

    /// <summary>
    /// True only when the group exists in the tenant catalogue AND has been withdrawn (inactive,
    /// or left without an active choice / recommended default). A group that is alive and well but
    /// simply not offered by this product is not a retirement — that is V1.
    /// </summary>
    private async Task<bool> GroupWithdrawnAsync(string groupKey, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var group = await _dbContext.OptionGroups
            .AsNoTracking()
            .Include(g => g.Choices)
            .FirstOrDefaultAsync(g => g.TenantId == tenantId && g.Key == groupKey, cancellationToken);

        if (group is null)
        {
            return false;
        }

        var active = group.Choices.Where(c => c.IsActive && !c.IsDeleted).ToList();
        var servable = group.IsActive && !group.IsDeleted && active.Count > 0 && active.Count(c => c.IsRecommendedDefault) == 1;
        return !servable;
    }

    /// <summary>
    /// True only when the choice exists in the group AND is inactive — a retirement. A choice that
    /// is active but outside this product's narrowing is V2, however valid it is elsewhere.
    /// </summary>
    private async Task<bool> ChoiceWithdrawnAsync(string groupKey, string choiceKey, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        return await _dbContext.OptionChoices
            .AsNoTracking()
            .Join(
                _dbContext.OptionGroups.AsNoTracking().Where(g => g.TenantId == tenantId && g.Key == groupKey),
                c => c.OptionGroupId,
                g => g.Id,
                (c, _) => c)
            .AnyAsync(c => c.TenantId == tenantId && c.Key == choiceKey && !c.IsActive, cancellationToken);
    }

    private async Task<OptionSelectionResult> BuildResultAsync(
        Guid productId,
        IReadOnlyList<EffectiveOptionGroupDto> groups,
        Dictionary<string, IReadOnlyList<string>> resolved,
        string? currency,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var product = await _dbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == productId && p.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Product '{productId}' was not found.");

        var target = string.IsNullOrWhiteSpace(currency) ? null : currency.Trim().ToUpperInvariant();

        // V10 — every involved amount must already be in the requested currency. Nothing is
        // converted and nothing is assumed; a mis-denominated group is a hard error.
        if (target is not null)
        {
            foreach (var group in groups.Where(g => resolved.ContainsKey(g.Key)))
            {
                if (!string.Equals(group.Currency, target, StringComparison.Ordinal))
                {
                    throw new OptionValidationException(
                        "V10",
                        $"Option group '{group.Key}' is priced in {group.Currency}, but the quote currency is {target}.");
                }
            }

            if (product.UnitSurcharge is not null &&
                !string.Equals(product.UnitSurchargeCurrency, target, StringComparison.Ordinal))
            {
                throw new OptionValidationException(
                    "V10",
                    $"The unit surcharge is denominated in {product.UnitSurchargeCurrency ?? "an unknown currency"}, but the quote currency is {target}.");
            }
        }

        var multiSelectGroups = groups
            .Where(g => g.SelectionMode == OptionSelectionModes.Multi)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.Ordinal);

        var breakdown = new List<OptionGroupAdjustment>();
        var display = new List<OptionDisplayEntry>();
        var summaryParts = new List<string>();
        var adjustment = 0m;
        var isDefault = true;

        foreach (var group in groups.OrderBy(g => g.SortOrder).ThenBy(g => g.Key, StringComparer.Ordinal))
        {
            if (!resolved.TryGetValue(group.Key, out var chosenKeys))
            {
                continue;
            }

            var defaultChoice = group.Choices.First(c => c.Key == group.DefaultChoiceKey);
            var chosen = chosenKeys
                .Select(k => group.Choices.First(c => c.Key == k))
                .OrderBy(c => c.SortOrder).ThenBy(c => c.Key, StringComparer.Ordinal)
                .ToList();

            // Multi-select sums every chosen price, then subtracts the default's price ONCE.
            var groupAdjustment = chosen.Sum(c => c.Price) - defaultChoice.Price;
            adjustment += groupAdjustment;

            if (groupAdjustment != 0m || chosen.Count != 1 || chosen[0].Key != group.DefaultChoiceKey)
            {
                breakdown.Add(new OptionGroupAdjustment(group.Key, chosen.Select(c => c.Key).ToList(), groupAdjustment));
            }

            // Labels are snapshotted for EVERY group, defaults included: labels are deliberately
            // mutable and an all-defaults order has an empty summary, so without this the kitchen
            // could not render the preparation without the live catalogue.
            foreach (var choice in chosen)
            {
                display.Add(new OptionDisplayEntry(group.Label, choice.Label));
            }

            var differs = chosen.Count != 1 || chosen[0].Key != group.DefaultChoiceKey;
            if (differs)
            {
                isDefault = false;
                summaryParts.AddRange(chosen.Select(c => c.Label));
            }
        }

        var canonical = CanonicalSelection.Serialize(resolved, multiSelectGroups);

        // Without a target currency nothing has been checked against V10, so any total would be a
        // sum of possibly-different denominations wearing one label. Return the canonical form and
        // the structural facts, and zero the money rather than publish a number we cannot stand
        // behind — this is the contract content resolution (Spec 067) consumes.
        if (target is null)
        {
            return new OptionSelectionResult(
                canonical, isDefault, Adjustment: 0m, Currency: string.Empty,
                UnitSurcharge: null, UnitSurchargeCurrency: null,
                string.Join(" · ", summaryParts), display, Breakdown: []);
        }

        return new OptionSelectionResult(
            canonical,
            isDefault,
            adjustment,
            target,
            product.UnitSurcharge,
            product.UnitSurchargeCurrency,
            // Differs-from-default only (Step 2 FR-11.4). Presentation convenience — the canonical
            // selection is the truth and Display is the structured form consumers should prefer.
            string.Join(" · ", summaryParts),
            display,
            breakdown);
    }
}
