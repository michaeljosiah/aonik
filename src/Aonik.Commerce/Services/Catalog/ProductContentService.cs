using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.Catalog;

/// <summary>
/// Spec 067 — option-dependent product content. The safety stance is enforced here (§2):
/// authored, or absent — never derived, never substituted. Every content write for a product runs
/// inside one execution-strategy attempt that reloads the <see cref="ProductContent"/> row,
/// validates against that snapshot, and bumps <see cref="ProductContent.ContentVersion"/> under
/// the row's concurrency token — the cross-row invariants (V-C2/V-C6) are serialized, not hoped
/// for (§9), and the version bump the cache needs doubles as the serialization point.
/// </summary>
internal sealed class ProductContentService : IProductContentService
{
    private const decimal FigureBound = 9_999_999.99m; // decimal(9,2)

    private readonly CommerceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IOptionSelectionService _selections;
    private readonly IProductOptionService _options;

    public ProductContentService(
        CommerceDbContext dbContext,
        ITenantProvider tenantProvider,
        IOptionSelectionService selections,
        IProductOptionService options)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _selections = selections;
        _options = options;
    }

    // ─── Resolution (§5) ─────────────────────────────────────────────────────

    public async Task<ResolvedContentDto?> ResolveAsync(
        Guid productId, JsonElement? selection, CancellationToken ct = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        // Step 1 — no default block: a defined state, never an empty panel presented as fact.
        var content = await _dbContext.ProductContents
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.ProductId == productId, ct);
        if (content is null)
        {
            return null;
        }

        // Step 2 — normalise via Spec 066: complete, canonical, validated (V1–V5 fail there,
        // currency-free per the 066 §10 amendment).
        var canonical = (await _selections.NormalizeAsync(productId, selection, ct)).CanonicalSelectionJson;

        // Step 3 — exact variant match by the complete canonical selection. Identity survives
        // default moves, so after chicken → salmon the salmon variant serves the all-defaults
        // selection BEFORE any default-block fallback (§6).
        var hash = HashSelection(canonical);
        var variant = await _dbContext.ProductContentVariants
            .AsNoTracking()
            .FirstOrDefaultAsync(
                v => v.TenantId == tenantId && v.ProductId == productId && v.SelectionHash == hash && v.IsActive, ct);

        if (variant is not null && string.Equals(variant.SelectionJson, canonical, StringComparison.Ordinal))
        {
            var heating = ParseHeatingLenient(variant.HeatingJson);
            return new ResolvedContentDto(
                variant.ServingLabel,
                NutritionOf(variant),
                variant.Ingredients,
                variant.Allergens,
                // ANY missing declaration is withheld: a half-published pair (ingredients
                // authored, allergens not) must still show the not-yet-published state — the
                // absent ALLERGEN line is the dangerous half.
                DeclarationsWithheld: variant.Ingredients is null || variant.Allergens is null,
                heating ?? [],
                HeatingWithheld: heating is null,
                IsStandardPreparation: false,
                IsStale: false,
                canonical,
                variant.SelectionJson,
                content.ContentVersion);
        }

        // Steps 4–6 — no variant: diff against the CURRENT all-defaults selection. Canonical
        // forms make the key-based diff a string comparison — prices never participate, so two
        // £0 proteins are still a diff (a diff is exactly what can change an allergen list).
        var allDefaults = (await _selections.NormalizeAsync(productId, null, ct)).CanonicalSelectionJson;
        var isStandardPreparation = !string.Equals(canonical, allDefaults, StringComparison.Ordinal);

        // Belt and braces (§6): the block records which all-defaults combination it describes; a
        // mismatch behaves exactly like RequiresReview even if the flag write was missed — a
        // default shifted by a path that never fired a hook can still never serve old
        // standard-prep content as the new standard preparation.
        var isStale = content.RequiresReview
            || !string.Equals(content.DescribesSelectionJson, allDefaults, StringComparison.Ordinal);

        // Declarations and heating are exact-authored or withheld: for a non-default combination
        // the caption qualifies FIGURES — it does not make a substituted shellfish declaration or
        // light-portion timings safe. A stale block withholds them even for the standard
        // preparation.
        var withhold = isStandardPreparation || isStale;
        // Null = the stored JSON failed to parse (legacy damage): corrupted heating is WITHHELD,
        // never presented as an explicitly authored "no heating required".
        var blockHeating = ParseHeatingLenient(content.HeatingJson);

        return new ResolvedContentDto(
            content.ServingLabel,
            NutritionOf(content),
            withhold ? null : content.Ingredients,
            withhold ? null : content.Allergens,
            DeclarationsWithheld: withhold || content.Ingredients is null || content.Allergens is null,
            withhold || blockHeating is null ? [] : blockHeating,
            HeatingWithheld: withhold || blockHeating is null,
            isStandardPreparation,
            isStale,
            canonical,
            MatchedVariantSelectionJson: null,
            content.ContentVersion);
    }

    // ─── Authoring (§7/§9) ───────────────────────────────────────────────────

    public async Task<ProductContentDto> UpsertContentAsync(
        Guid productId, UpsertProductContentCommand command, CancellationToken ct = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        await RequireProductAsync(tenantId, productId, ct);

        ValidateServingLabel(command.ServingLabel);
        ValidateFigures(FiguresOf(command));
        var heatingJson = NormalizeHeatingJson(command.HeatingJson) ?? "[]";
        var ingredients = NormalizeDeclaration(command.Ingredients);
        var allergens = NormalizeDeclaration(command.Allergens);

        // Captured OUTSIDE the write: the all-defaults binding the block will describe.
        return await RunContentWriteAsync(tenantId, productId, requireExisting: false, async (content, ct2) =>
        {
            // The all-defaults binding is captured INSIDE the serialized attempt: an option
            // write committing between an outside capture and this write would store a binding
            // that was stale at birth (M4-class staleness).
            var allDefaults = (await _selections.NormalizeAsync(productId, null, ct2)).CanonicalSelectionJson;

            // V-C6 — a figure the default newly publishes must be published by every ACTIVE
            // variant, or a resolved panel could mix default and variant figures by the back
            // door. Validated INSIDE the serialized write so a racing variant-add cannot slip a
            // now-incomplete variant past it (§9 / A21). Removing a figure is always allowed.
            var newFigures = FiguresOf(command);
            // AsNoTracking on purpose: a RETRY reuses this context, and a tracking query would
            // hand back variant instances loaded by the failed attempt — validating V-C6 against
            // values the winning writer already changed. No-tracking reads the database truth
            // every attempt; this path only reads.
            var activeVariants = await _dbContext.ProductContentVariants
                .AsNoTracking()
                .Where(v => v.TenantId == tenantId && v.ProductId == productId && v.IsActive)
                .ToListAsync(ct2);

            var offenders = new List<string>();
            foreach (var variant in activeVariants)
            {
                var missing = PublishedNames(newFigures)
                    .Where(name => !PublishedNames(FiguresOf(variant)).Contains(name, StringComparer.Ordinal))
                    .ToList();
                if (missing.Count > 0)
                {
                    offenders.Add($"{variant.SelectionJson} (missing {string.Join(", ", missing)})");
                }
            }
            if (offenders.Count > 0)
            {
                throw new StorefrontValidationException(
                    $"V-C6: the default block would publish figure(s) not published by active variant(s): {string.Join("; ", offenders)}. Update them first, or in the same batch.");
            }

            if (content is null)
            {
                content = new ProductContent { Id = Guid.NewGuid(), TenantId = tenantId, ProductId = productId };
                _dbContext.ProductContents.Add(content);
            }

            content.ServingLabel = command.ServingLabel.Trim();
            content.Kcal = command.Kcal;
            content.ProteinGrams = command.ProteinGrams;
            content.CarbsGrams = command.CarbsGrams;
            content.FatGrams = command.FatGrams;
            content.FibreGrams = command.FibreGrams;
            content.SugarsGrams = command.SugarsGrams;
            content.SaltGrams = command.SaltGrams;
            content.Ingredients = ingredients;
            content.Allergens = allergens;
            content.HeatingJson = heatingJson;
            content.DescribesSelectionJson = allDefaults;   // re-captures the binding (§6)
            content.RequiresReview = false;

            return content;
        }, ct, MapContent);
    }

    public async Task<ProductContentDto> ConfirmContentReviewAsync(Guid productId, CancellationToken ct = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        return await RunContentWriteAsync(tenantId, productId, requireExisting: true, async (content, ct2) =>
        {
            // "Reviewed, still correct": the operator asserts the existing text describes the
            // CURRENT standard preparation — captured inside the attempt so "current" means
            // current at commit, not at request parse.
            var allDefaults = (await _selections.NormalizeAsync(productId, null, ct2)).CanonicalSelectionJson;
            content!.DescribesSelectionJson = allDefaults;
            content.RequiresReview = false;
            return content;
        }, ct, MapContent);
    }

    public async Task<ProductContentVariantDto> AddVariantAsync(
        Guid productId, UpsertContentVariantCommand command, CancellationToken ct = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        await RequireProductAsync(tenantId, productId, ct);

        ValidateServingLabel(command.ServingLabel);
        ValidateFigures(FiguresOf(command));
        var heatingJson = NormalizeHeatingJson(command.HeatingJson);

        ProductContentVariant? result = null;
        await RunContentWriteAsync(tenantId, productId, requireExisting: true, async (content, ct2) =>
        {
            // V-C1 — normalised INSIDE the serialized attempt: an option write (narrowing,
            // default move) committing after an outside normalisation would let this store a
            // variant for a now-invalid combination, or one that now shadows the default block.
            // 066's V1–V5 throw here exactly as they do for customer input.
            var canonical = (await NormalizeAuthoringSelectionAsync(productId, command.SelectionJson, ct2)).CanonicalSelectionJson;
            var allDefaults = (await _selections.NormalizeAsync(productId, null, ct2)).CanonicalSelectionJson;
            if (string.Equals(canonical, allDefaults, StringComparison.Ordinal))
            {
                throw new StorefrontValidationException(
                    "V-C1: this selection is the current standard preparation — author it on the default block, not as a variant.");
            }

            // V-C8 enforced by requireExisting; V-C2 against the block inside the same write.
            ValidateVariantFigureCompleteness(content!, FiguresOf(command));

            var hash = HashSelection(canonical);
            var existing = await _dbContext.ProductContentVariants
                .FirstOrDefaultAsync(
                    v => v.TenantId == tenantId && v.ProductId == productId && v.SelectionHash == hash, ct2);
            if (existing is not null)
            {
                // Tracked because the revive path mutates it — so a retry must reload it, or it
                // replays flags a failed attempt already flipped in memory.
                await _dbContext.Entry(existing).ReloadAsync(ct2);
            }

            if (existing is not null && !string.Equals(existing.SelectionJson, canonical, StringComparison.Ordinal))
            {
                // Astronomically unlikely, but the index key is the hash — compare the JSON
                // before trusting it (V-C4's service-side check).
                throw new StorefrontValidationException(
                    "Selection hash collision with a different stored combination; contact support.");
            }

            if (existing is { IsActive: true })
            {
                throw new StorefrontValidationException(
                    "V-C4: an active variant already exists for this combination — update it instead.");
            }

            if (existing is not null)
            {
                // Re-authoring a retired combination revives its row (the unique index spans
                // retired rows precisely so this path exists).
                existing.IsActive = true;
                Apply(existing, command, heatingJson);
                result = existing;
            }
            else
            {
                var variant = new ProductContentVariant
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProductId = productId,
                    SelectionJson = canonical,
                    SelectionHash = hash,
                };
                Apply(variant, command, heatingJson);
                _dbContext.ProductContentVariants.Add(variant);
                result = variant;
            }

            return content!;
        }, ct, _ => true);

        return MapVariant(result!);
    }

    public async Task<ProductContentVariantDto> UpdateVariantAsync(
        Guid variantId, UpsertContentVariantCommand command, CancellationToken ct = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var variant = await _dbContext.ProductContentVariants
            .FirstOrDefaultAsync(v => v.Id == variantId && v.TenantId == tenantId, ct)
            ?? throw new NotFoundException($"Content variant '{variantId}' was not found.");

        ValidateServingLabel(command.ServingLabel);
        ValidateFigures(FiguresOf(command));
        var heatingJson = NormalizeHeatingJson(command.HeatingJson);

        await RunContentWriteAsync(tenantId, variant.ProductId, requireExisting: true, async (content, ct2) =>
        {
            // Reload the tracked variant per attempt (a failed attempt mutated it in memory),
            // then normalise inside the same serialized snapshot as the write — see AddVariant.
            await _dbContext.Entry(variant).ReloadAsync(ct2);

            // A retired variant is history (V-C5): its authored combination must survive for
            // audit and revival, and coverage still lists its id. Rewriting it in place — worse,
            // moving it to another hash — destroys that record while staying unservable.
            if (!variant.IsActive)
            {
                throw new StorefrontValidationException(
                    "V-C5: this variant is retired — re-author its combination to revive it; retired rows are not editable.");
            }

            var canonical = (await NormalizeAuthoringSelectionAsync(variant.ProductId, command.SelectionJson, ct2)).CanonicalSelectionJson;
            var hash = HashSelection(canonical);
            var selectionChanges = !string.Equals(hash, variant.SelectionHash, StringComparison.Ordinal);

            // V-C1 guards MOVES onto the standard preparation. A variant whose UNCHANGED
            // combination became the default (the default moved under it) is deliberately served
            // ahead of the block, so its facts must remain correctable in place — blocking the
            // update would freeze exactly the content customers are being served.
            var allDefaults = (await _selections.NormalizeAsync(variant.ProductId, null, ct2)).CanonicalSelectionJson;
            if (selectionChanges && string.Equals(canonical, allDefaults, StringComparison.Ordinal))
            {
                throw new StorefrontValidationException(
                    "V-C1: this selection is the current standard preparation — author it on the default block, not as a variant.");
            }

            ValidateVariantFigureCompleteness(content!, FiguresOf(command));

            if (selectionChanges)
            {
                var occupant = await _dbContext.ProductContentVariants.AnyAsync(
                    v => v.TenantId == tenantId && v.ProductId == variant.ProductId
                        && v.SelectionHash == hash && v.Id != variant.Id, ct2);
                if (occupant)
                {
                    throw new StorefrontValidationException(
                        "V-C4: another variant (active or retired) already holds this combination — author that one instead.");
                }
                variant.SelectionJson = canonical;
                variant.SelectionHash = hash;
            }

            Apply(variant, command, heatingJson);
            return content!;
        }, ct, _ => true);

        return MapVariant(variant);
    }

    public async Task DeactivateVariantAsync(Guid variantId, CancellationToken ct = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var variant = await _dbContext.ProductContentVariants
            .FirstOrDefaultAsync(v => v.Id == variantId && v.TenantId == tenantId, ct)
            ?? throw new NotFoundException($"Content variant '{variantId}' was not found.");

        // Soft-retire only (V-C5) — and still a content write: resolution changes, so the
        // version bumps and cached responses become unreachable.
        await RunContentWriteAsync(tenantId, variant.ProductId, requireExisting: true, async (content, ct2) =>
        {
            // Reload per attempt: a retry must submit the CURRENT rowversion, not the token a
            // failed attempt already burned — see UpdateVariantAsync.
            await _dbContext.Entry(variant).ReloadAsync(ct2);
            variant.IsActive = false;
            return content!;
        }, ct, _ => true);
    }

    public async Task<ContentCoverageDto> GetCoverageAsync(Guid productId, CancellationToken ct = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        await RequireProductAsync(tenantId, productId, ct);

        var variants = await _dbContext.ProductContentVariants
            .AsNoTracking()
            .Where(v => v.TenantId == tenantId && v.ProductId == productId)
            .OrderBy(v => v.SelectionJson)
            .ToListAsync(ct);
        var activeHashes = variants.Where(v => v.IsActive).Select(v => v.SelectionHash).ToHashSet(StringComparer.Ordinal);

        // Single-choice deviations only: for each offered group, each offered non-default choice
        // substituted ALONE into the standard selection. Bounded by Σ|offered choices| — a large
        // multi-select group must never turn an admin read into a 2^N enumeration (§8).
        var groups = await _options.GetEffectiveOptionsAsync(productId, ct);
        var gaps = new List<ContentCoverageGapDto>();

        foreach (var group in groups)
        {
            foreach (var choice in group.Choices)
            {
                if (string.Equals(choice.Key, group.DefaultChoiceKey, StringComparison.Ordinal))
                {
                    continue;
                }

                var probe = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    [group.Key] = group.SelectionMode == OptionSelectionModes.Multi
                        ? new[] { choice.Key }
                        : choice.Key,
                };
                using var document = JsonSerializer.SerializeToDocument(probe);
                var canonical = (await _selections.NormalizeAsync(productId, document.RootElement, ct)).CanonicalSelectionJson;

                if (!activeHashes.Contains(HashSelection(canonical)))
                {
                    gaps.Add(new ContentCoverageGapDto(group.Key, choice.Key, canonical));
                }
            }
        }

        return new ContentCoverageDto(
            productId,
            variants.Select(v => new ContentCoverageEntryDto(v.Id, v.SelectionJson, v.IsActive)).ToList(),
            gaps);
    }

    // ─── The serialized content write (§9) ───────────────────────────────────

    /// <summary>Every content write runs here: one execution-strategy attempt that reloads the
    /// content row, runs <paramref name="mutate"/> against that snapshot, bumps
    /// <see cref="ProductContent.ContentVersion"/>, and commits — concurrent content writes for
    /// the product conflict on the row's token instead of validating past each other.</summary>
    private async Task<TResult> RunContentWriteAsync<TResult>(
        Guid tenantId,
        Guid productId,
        bool requireExisting,
        Func<ProductContent?, CancellationToken, Task<ProductContent>> mutate,
        CancellationToken ct,
        Func<ProductContent, TResult> map)
    {
        // The loser of a token conflict revalidates against fresh state and retries ONCE, then
        // fails cleanly (§9): the per-attempt reload below makes the retry see the winner's
        // committed state, so a variant missing a now-published figure is re-REJECTED rather
        // than re-conflicted, and a compatible write simply lands.
        try
        {
            return await RunContentWriteAttemptAsync(tenantId, productId, requireExisting, mutate, ct, map);
        }
        catch (DbUpdateException)
        {
            // Concurrency loser OR the first-upsert race: two creators both observe no row, and
            // the unique (TenantId, ProductId) index rejects one with a plain DbUpdateException —
            // there was no rowversion to contend on yet. Either way the retry's reload sees the
            // winner's committed state and revalidates against it; a second failure is genuine.
            return await RunContentWriteAttemptAsync(tenantId, productId, requireExisting, mutate, ct, map);
        }
    }

    private async Task<TResult> RunContentWriteAttemptAsync<TResult>(
        Guid tenantId,
        Guid productId,
        bool requireExisting,
        Func<ProductContent?, CancellationToken, Task<ProductContent>> mutate,
        CancellationToken ct,
        Func<ProductContent, TResult> map)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async ct2 =>
        {
            // A replay must not re-stage entities a failed attempt left tracked as Added — a
            // second AddVariant would double-insert into the unique selection index, and a
            // second first-upsert would double-insert the content row itself.
            foreach (var entry in _dbContext.ChangeTracker.Entries<ProductContentVariant>()
                .Where(e => e.State == EntityState.Added && e.Entity.ProductId == productId)
                .ToList())
            {
                entry.State = EntityState.Detached;
            }
            foreach (var entry in _dbContext.ChangeTracker.Entries<ProductContent>()
                .Where(e => e.State == EntityState.Added && e.Entity.ProductId == productId)
                .ToList())
            {
                entry.State = EntityState.Detached;
            }

            // Fresh snapshot per attempt — a replay must not trust state a failed attempt
            // mutated in memory (the Spec 066 retry lessons).
            var content = await _dbContext.ProductContents
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.ProductId == productId, ct2);
            if (content is not null)
            {
                await _dbContext.Entry(content).ReloadAsync(ct2);
            }
            if (content is null && requireExisting)
            {
                throw new StorefrontValidationException(
                    "V-C8: this product has no default content block — author it first; it is the baseline variants are validated against.");
            }

            await using var transaction = _dbContext.Database.IsRelational()
                ? await _dbContext.Database.BeginTransactionAsync(ct2)
                : null;

            // The mutate delegate returns the content row to bump — on the upsert-create path
            // that is the freshly Added (not yet saved) entity, which no store query could find.
            var written = await mutate(content, ct2);
            written.ContentVersion++;

            await _dbContext.SaveChangesAsync(ct2);

            if (transaction is not null)
            {
                await transaction.CommitAsync(ct2);
            }

            return map(written);
        }, ct);
    }

    // ─── Internals ───────────────────────────────────────────────────────────

    internal static string HashSelection(string canonicalJson)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson))).ToLowerInvariant();

    private async Task<OptionSelectionResult> NormalizeAuthoringSelectionAsync(
        Guid productId, string selectionJson, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(selectionJson))
        {
            throw new StorefrontValidationException("V-C1: a selection is required; send {} for no deviations.");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(selectionJson);
        }
        catch (JsonException)
        {
            throw new StorefrontValidationException("V-C1: the selection is not valid JSON.");
        }

        using (document)
        {
            return await _selections.NormalizeAsync(productId, document.RootElement, ct);
        }
    }

    private async Task RequireProductAsync(Guid tenantId, Guid productId, CancellationToken ct)
    {
        var exists = await _dbContext.Products.AnyAsync(p => p.Id == productId && p.TenantId == tenantId, ct);
        if (!exists)
        {
            throw new NotFoundException($"Product '{productId}' was not found.");
        }
    }

    private static void ValidateServingLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            throw new StorefrontValidationException("V-C3: a serving label is required — every block must caption which preparation it describes.");
        }
        if (label.Trim().Length > 128)
        {
            throw new StorefrontValidationException("A serving label is at most 128 characters.");
        }
    }

    /// <summary>V-C7 — SQL would happily store −500 kcal; the service must not. decimal is finite
    /// by type; negatives and column-bound overflow are the representable lies.</summary>
    private static void ValidateFigures(IReadOnlyList<(string Name, decimal? Value)> figures)
    {
        foreach (var (name, value) in figures)
        {
            if (value is < 0)
            {
                throw new StorefrontValidationException($"V-C7: {name} cannot be negative.");
            }
            if (value is > FigureBound)
            {
                throw new StorefrontValidationException($"V-C7: {name} exceeds the storable bound ({FigureBound}).");
            }
        }
    }

    /// <summary>V-C2 — for every figure the default block publishes, the variant must publish it
    /// too (more is allowed, fewer is not): a panel mixing default kcal with variant protein
    /// would be a derived panel by the back door.</summary>
    private static void ValidateVariantFigureCompleteness(
        ProductContent content, IReadOnlyList<(string Name, decimal? Value)> variantFigures)
    {
        var variantPublished = PublishedNames(variantFigures);
        var missing = PublishedNames(FiguresOf(content))
            .Where(name => !variantPublished.Contains(name, StringComparer.Ordinal))
            .ToList();
        if (missing.Count > 0)
        {
            throw new StorefrontValidationException(
                $"V-C2: the variant must publish every figure the default block publishes; missing {string.Join(", ", missing)}.");
        }
    }

    private static List<string> PublishedNames(IReadOnlyList<(string Name, decimal? Value)> figures)
        => figures.Where(f => f.Value is not null).Select(f => f.Name).ToList();

    private static List<(string, decimal?)> FiguresOf(ProductContent c) =>
        [("kcal", c.Kcal), ("proteinGrams", c.ProteinGrams), ("carbsGrams", c.CarbsGrams), ("fatGrams", c.FatGrams), ("fibreGrams", c.FibreGrams), ("sugarsGrams", c.SugarsGrams), ("saltGrams", c.SaltGrams)];

    private static List<(string, decimal?)> FiguresOf(ProductContentVariant v) =>
        [("kcal", v.Kcal), ("proteinGrams", v.ProteinGrams), ("carbsGrams", v.CarbsGrams), ("fatGrams", v.FatGrams), ("fibreGrams", v.FibreGrams), ("sugarsGrams", v.SugarsGrams), ("saltGrams", v.SaltGrams)];

    private static List<(string, decimal?)> FiguresOf(UpsertProductContentCommand c) =>
        [("kcal", c.Kcal), ("proteinGrams", c.ProteinGrams), ("carbsGrams", c.CarbsGrams), ("fatGrams", c.FatGrams), ("fibreGrams", c.FibreGrams), ("sugarsGrams", c.SugarsGrams), ("saltGrams", c.SaltGrams)];

    private static List<(string, decimal?)> FiguresOf(UpsertContentVariantCommand c) =>
        [("kcal", c.Kcal), ("proteinGrams", c.ProteinGrams), ("carbsGrams", c.CarbsGrams), ("fatGrams", c.FatGrams), ("fibreGrams", c.FibreGrams), ("sugarsGrams", c.SugarsGrams), ("saltGrams", c.SaltGrams)];

    private static NutritionDto NutritionOf(ProductContent c) => new(
        c.Kcal, c.ProteinGrams, c.CarbsGrams, c.FatGrams, c.FibreGrams, c.SugarsGrams, c.SaltGrams);

    private static NutritionDto NutritionOf(ProductContentVariant v) => new(
        v.Kcal, v.ProteinGrams, v.CarbsGrams, v.FatGrams, v.FibreGrams, v.SugarsGrams, v.SaltGrams);

    private static void Apply(ProductContentVariant variant, UpsertContentVariantCommand command, string? heatingJson)
    {
        variant.ServingLabel = command.ServingLabel.Trim();
        variant.Kcal = command.Kcal;
        variant.ProteinGrams = command.ProteinGrams;
        variant.CarbsGrams = command.CarbsGrams;
        variant.FatGrams = command.FatGrams;
        variant.FibreGrams = command.FibreGrams;
        variant.SugarsGrams = command.SugarsGrams;
        variant.SaltGrams = command.SaltGrams;
        variant.Ingredients = NormalizeDeclaration(command.Ingredients);
        variant.Allergens = NormalizeDeclaration(command.Allergens);
        variant.HeatingJson = heatingJson;
    }

    /// <summary>Strict for authoring: a JSON array of { method, body } with non-empty strings.
    /// Null input stays null — on a VARIANT that means withheld (§4); the default block maps null
    /// to "[]" at its call site.</summary>
    /// <summary>Declarations are authored or ABSENT: a blank string is absence wearing quotes,
    /// and storing it would make resolution report DeclarationsWithheld: false over no usable
    /// allergen information — the storefront would suppress its unpublished warning.</summary>
    private static string? NormalizeDeclaration(string? text)
        => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static string? NormalizeHeatingJson(string? heatingJson)
    {
        if (heatingJson is null)
        {
            return null;
        }
        if (string.IsNullOrWhiteSpace(heatingJson))
        {
            throw new StorefrontValidationException("heatingJson must be a JSON array of { method, body } steps; send [] for none.");
        }

        try
        {
            var steps = ParseHeatingStrict(heatingJson);
            return JsonSerializer.Serialize(steps.Select(s => new { method = s.Method, body = s.Body }));
        }
        catch (JsonException)
        {
            throw new StorefrontValidationException("heatingJson is not valid JSON.");
        }
    }

    private static List<HeatingStepDto> ParseHeatingStrict(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new StorefrontValidationException("heatingJson must be a JSON array of { method, body } steps.");
        }

        var steps = new List<HeatingStepDto>();
        foreach (var entry in document.RootElement.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object
                || !entry.TryGetProperty("method", out var method) || method.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(method.GetString())
                || !entry.TryGetProperty("body", out var body) || body.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(body.GetString()))
            {
                throw new StorefrontValidationException(
                    "Every heating step requires non-empty string 'method' and 'body'.");
            }
            steps.Add(new HeatingStepDto(method.GetString()!.Trim(), body.GetString()!.Trim()));
        }
        return steps;
    }

    /// <summary>Lenient for serving: authored strictly, so malformed storage is legacy damage —
    /// treated as withheld rather than 500ing an anonymous read (the §11-style posture).</summary>
    private static List<HeatingStepDto>? ParseHeatingLenient(string? json)
    {
        if (json is null)
        {
            return null;
        }

        try
        {
            return ParseHeatingStrict(json);
        }
        catch (Exception e) when (e is JsonException or StorefrontValidationException)
        {
            return null;
        }
    }

    private static ProductContentDto MapContent(ProductContent c) => new(
        c.ProductId,
        c.ServingLabel,
        NutritionOf(c),
        c.Ingredients,
        c.Allergens,
        ParseHeatingLenient(c.HeatingJson) ?? [],
        c.DescribesSelectionJson,
        c.RequiresReview,
        c.ContentVersion);

    private static ProductContentVariantDto MapVariant(ProductContentVariant v) => new(
        v.Id,
        v.ProductId,
        v.SelectionJson,
        v.ServingLabel,
        NutritionOf(v),
        v.Ingredients,
        v.Allergens,
        ParseHeatingLenient(v.HeatingJson),
        v.IsActive);
}
