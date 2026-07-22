using System.Text.Json;
using System.Text.RegularExpressions;

using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aonik.Commerce.Services.Catalog;

/// <summary>Option catalogue authoring and per-product resolution (Spec 066 §6, §9, §10).</summary>
internal sealed partial class ProductOptionService : IProductOptionService
{
    private readonly CommerceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IProductContentReviewFlagger _contentReview;
    private readonly ILogger<ProductOptionService> _logger;

    public ProductOptionService(
        CommerceDbContext dbContext,
        ITenantProvider tenantProvider,
        IProductContentReviewFlagger contentReview,
        ILogger<ProductOptionService> logger)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _contentReview = contentReview;
        _logger = logger;
    }

    // ─── Authoring ───────────────────────────────────────────────────────────

    public async Task<OptionGroupDto> CreateGroupAsync(CreateOptionGroupCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var key = NormalizeKey(command.Key, "group");
        var mode = NormalizeSelectionMode(command.SelectionMode);

        if (await _dbContext.OptionGroups.AnyAsync(g => g.TenantId == tenantId && g.Key == key, cancellationToken))
        {
            throw new OptionValidationException("V6", $"An option group with key '{key}' already exists.");
        }

        var group = new OptionGroup
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = key,
            Label = RequireLabel(command.Label),
            HelpText = command.HelpText,
            SelectionMode = mode,
            Currency = NormalizeCurrency(command.Currency),
            SortOrder = command.SortOrder,
            IsActive = true,
        };

        _dbContext.OptionGroups.Add(group);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Map(group, []);
    }

    public async Task<OptionGroupDto> UpdateGroupAsync(Guid groupId, UpdateOptionGroupCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var group = await _dbContext.OptionGroups
            .Include(g => g.Choices)
            .FirstOrDefaultAsync(g => g.Id == groupId && g.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException($"Option group '{groupId}' was not found.");

        // Spec 067 §6 — group-level edits that change every inheriting product's all-defaults
        // selection: an activation flip adds/removes the group from it, and a selection-mode
        // change alters its canonical SHAPE (key vs [key]) for narrowings without an override.
        // Label/currency/sort edits change no selection and stage nothing.
        var activationFlips = command.IsActive is { } newActive && newActive != group.IsActive;
        var modeChanges = command.SelectionMode is not null
            && !string.Equals(NormalizeSelectionMode(command.SelectionMode), group.SelectionMode, StringComparison.Ordinal);

        // Only edits that actually change all-defaults selections stage a review: an activation
        // flip matters only when it changes SERVABILITY (an active-but-unservable group is
        // already absent from every selection), and a mode change matters only while the group
        // is servable and only for narrowings without their own override.
        var wasServable = IsServable(group);
        var willBeServable = command.IsActive is { } active
            ? active && !group.IsDeleted && HasServableChoiceSet(group)
            : wasServable;
        var servabilityChanges = activationFlips && wasServable != willBeServable;
        var modeMatters = modeChanges && wasServable;

        if (servabilityChanges || modeMatters)
        {
            var affected = await _dbContext.ProductOptionGroups
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.OptionGroupId == group.Id)
                .Where(x => servabilityChanges || x.SelectionModeOverride == null)
                .Select(x => x.ProductId)
                .Distinct()
                .ToListAsync(cancellationToken);
            await _contentReview.StageAsync(affected, cancellationToken);
        }

        group.Label = RequireLabel(command.Label);
        group.HelpText = command.HelpText;

        // Omitted means unchanged, for every value-typed member. Currency in particular denominates
        // the group's absolute choice prices, so silently defaulting it would reinterpret every one
        // of them without editing a single amount; defaulting IsActive would deactivate the group on
        // a rename. A label edit must never be able to redenominate money or hide a group.
        if (command.SortOrder is { } sortOrder)
        {
            group.SortOrder = sortOrder;
        }

        if (command.IsActive is { } isActive)
        {
            group.IsActive = isActive;
        }

        if (command.SelectionMode is not null)
        {
            group.SelectionMode = NormalizeSelectionMode(command.SelectionMode);
        }

        if (command.Currency is not null)
        {
            group.Currency = NormalizeCurrency(command.Currency);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(group, group.Choices);
    }

    public async Task<OptionChoiceDto> AddChoiceAsync(Guid groupId, AddOptionChoiceCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var group = await _dbContext.OptionGroups
            .Include(g => g.Choices)
            .FirstOrDefaultAsync(g => g.Id == groupId && g.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException($"Option group '{groupId}' was not found.");

        var key = NormalizeKey(command.Key, "choice");

        if (group.Choices.Any(c => c.Key == key))
        {
            throw new OptionValidationException("V6", $"Group '{group.Key}' already has a choice with key '{key}'.");
        }

        // V7 — a direct default flag is allowed only for the 0→1 transition (a group's first
        // default). Moving an existing default goes through SetRecommendedDefaultAsync so the
        // demote and promote commit together.
        if (command.IsRecommendedDefault && group.Choices.Any(c => c.IsRecommendedDefault && c.IsActive && !c.IsDeleted))
        {
            throw new OptionValidationException(
                "V7",
                $"Group '{group.Key}' already has a recommended default; move it with the recommended-default operation instead.");
        }

        var choice = new OptionChoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OptionGroupId = group.Id,
            Key = key,
            Label = RequireLabel(command.Label),
            Note = command.Note,
            Price = command.Price,
            IsRecommendedDefault = command.IsRecommendedDefault,
            SortOrder = command.SortOrder,
            IsActive = command.IsActive,
        };

        _dbContext.OptionChoices.Add(choice);

        // A new choice widens the offered set and can turn a half-authored group servable — both
        // things a concurrent narrowing validates against, so it contends on the group's token.
        TouchGroups([group]);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapChoice(choice);
    }

    public async Task<OptionChoiceDto> UpdateChoiceAsync(Guid choiceId, UpdateOptionChoiceCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var choice = await _dbContext.OptionChoices
            .FirstOrDefaultAsync(c => c.Id == choiceId && c.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException($"Option choice '{choiceId}' was not found.");

        var group = await _dbContext.OptionGroups
            .FirstOrDefaultAsync(g => g.Id == choice.OptionGroupId && g.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException($"Option group '{choice.OptionGroupId}' was not found.");

        // V7 — deactivating the group's own recommended default leaves §6's servability rule with
        // zero active defaults, so the group vanishes from the public catalogue and from EVERY
        // product's effective options. The V9 check below cannot catch this: products carrying
        // their own resolvable default each look safe in isolation, yet they lose the group along
        // with everyone else. Move the group default first.
        if (choice.IsActive && command.IsActive == false && choice.IsRecommendedDefault && group.IsActive && !group.IsDeleted)
        {
            throw new OptionValidationException(
                "V7",
                $"Choice '{choice.Key}' is the recommended default for group '{group.Key}'; " +
                "move the default to another choice before deactivating it.");
        }

        // V9 — deactivating the last resolvable default for some product's narrowing would leave
        // that product with an unresolvable effective default, and §6's fail-safe would silently
        // drop the whole group from its storefront. Name the products instead.
        if (choice.IsActive && command.IsActive == false)
        {
            var blocked = await ProductsLosingTheirDefaultAsync(tenantId, choice, cancellationToken);
            if (blocked.Count > 0)
            {
                throw new OptionValidationException(
                    "V9",
                    $"Choice '{choice.Key}' is the only resolvable default for product(s) {string.Join(", ", blocked)}; " +
                    "give them an explicit default or widen their allowed choices first.");
            }
        }

        // V7 (inverse transition) — a deactivated choice keeps its IsRecommendedDefault flag, so
        // reactivating it after another choice took over would leave two active defaults: a raw
        // unique-index error on SQL Server, and a silently non-servable group on InMemory.
        if (!choice.IsActive && command.IsActive == true && choice.IsRecommendedDefault)
        {
            var hasOtherDefault = await _dbContext.OptionChoices.AnyAsync(
                c => c.TenantId == tenantId
                    && c.OptionGroupId == choice.OptionGroupId
                    && c.Id != choice.Id
                    && c.IsRecommendedDefault
                    && c.IsActive,
                cancellationToken);

            if (hasOtherDefault)
            {
                throw new OptionValidationException(
                    "V7",
                    $"Choice '{choice.Key}' still carries the recommended-default flag and the group already has an active default; " +
                    "move the default explicitly instead of reactivating a second one.");
            }
        }

        // An activation flip changes which choices are offered and which defaults resolve, so a
        // concurrent narrowing must contend on the group's token rather than validating against
        // state this write is about to remove. A pure label or price edit carries no invariant and
        // deliberately does not contend.
        if (command.IsActive is { } newActive && choice.IsActive != newActive)
        {
            TouchGroups([group]);

            // Spec 067 §6, two distinct blast radii:
            //
            // (a) Group SERVABILITY flips — reactivating the sole recommended default of an
            //     active group turns it servable, adding it to EVERY offering product's
            //     all-defaults selection (the symmetric deactivation is V7-blocked for active
            //     groups, but the reactivation path is real). Every offering product is staged.
            //
            // (b) Otherwise, a product whose narrowing pins THIS choice as its explicit default
            //     changes standard preparation on either flip: deactivation falls it back to the
            //     group default (V9 allows that when one remains), reactivation restores it.
            var groupChoices = await _dbContext.OptionChoices
                .AsNoTracking()
                .Where(c => c.TenantId == tenantId && c.OptionGroupId == choice.OptionGroupId && c.Id != choice.Id)
                .ToListAsync(cancellationToken);

            bool ServableWith(bool thisChoiceActive)
            {
                var active = groupChoices.Where(c => c.IsActive && !c.IsDeleted).ToList();
                var activeCount = active.Count + (thisChoiceActive && !choice.IsDeleted ? 1 : 0);
                var defaultCount = active.Count(c => c.IsRecommendedDefault)
                    + (thisChoiceActive && !choice.IsDeleted && choice.IsRecommendedDefault ? 1 : 0);
                return group.IsActive && !group.IsDeleted && activeCount > 0 && defaultCount == 1;
            }

            var servabilityChanges = ServableWith(choice.IsActive) != ServableWith(newActive);

            var affectedQuery = _dbContext.ProductOptionGroups
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.OptionGroupId == choice.OptionGroupId);
            if (!servabilityChanges)
            {
                affectedQuery = affectedQuery.Where(x => x.DefaultChoiceKey == choice.Key);
            }

            var affected = await affectedQuery
                .Select(x => x.ProductId)
                .Distinct()
                .ToListAsync(cancellationToken);
            await _contentReview.StageAsync(affected, cancellationToken);
        }

        choice.Label = RequireLabel(command.Label);
        choice.Note = command.Note;

        // Omitted means unchanged — the same rule as the group update, and for the same reason:
        // the old defaults let a rename silently reprice a choice to zero or deactivate it.
        if (command.Price is { } price)
        {
            choice.Price = price;
        }

        if (command.SortOrder is { } sortOrder)
        {
            choice.SortOrder = sortOrder;
        }

        if (command.IsActive is { } isActive)
        {
            choice.IsActive = isActive;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapChoice(choice);
    }

    public async Task<RecommendedDefaultChangeResult> SetRecommendedDefaultAsync(Guid groupId, string choiceKey, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var group = await _dbContext.OptionGroups
            .Include(g => g.Choices)
            .FirstOrDefaultAsync(g => g.Id == groupId && g.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException($"Option group '{groupId}' was not found.");

        var key = NormalizeKey(choiceKey, "choice");

        // Demote FIRST, in its own round trip, then promote. A single SaveChanges is transactional
        // but does not guarantee EF emits the demote before the promote — and the filtered unique
        // index is evaluated per statement, so a promote-first ordering collides with the existing
        // default and fails the supported path. Two ordered writes inside one transaction avoid
        // both that and any window where the group has zero or two defaults.
        //
        // The sequence runs through the configured execution strategy because CommerceDbContext
        // enables EnableRetryOnFailure, and EF rejects a user-initiated transaction under a
        // retrying strategy unless the whole delegate is replayable. Without this the method throws
        // on every SQL Server call while passing every InMemory test, which opens no transaction.
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async ct =>
        {
            // Validation and commit must share ONE snapshot, and the reload defines it — so
            // everything from target resolution to the V11 check runs after it, inside the attempt.
            // Validating before the reload opened a window: a narrowing could commit in between,
            // the reload would refresh the group's concurrency token to the narrowing writer's new
            // one, and the touch below would then COMMIT rather than conflict — moving the default
            // to a choice that narrowing excludes, exactly what V11 exists to prevent. A narrowing
            // that commits after the reload instead trips the token and surfaces as a conflict.
            //
            // The reload also serves retries: a replayed attempt must not trust flags a failed
            // attempt already mutated in memory — it would find nothing to demote, stage no writes,
            // and report success against a database the rollback left on the OLD default. If an
            // ambiguous commit in fact landed, the replay finds the move already done and succeeds
            // idempotently.
            await _dbContext.Entry(group).ReloadAsync(ct);
            foreach (var choice in group.Choices)
            {
                await _dbContext.Entry(choice).ReloadAsync(ct);
            }

            var target = group.Choices.FirstOrDefault(c => c.Key == key && !c.IsDeleted)
                ?? throw new OptionValidationException("V8", $"Group '{group.Key}' has no choice with key '{key}'.");

            if (!target.IsActive)
            {
                throw new OptionValidationException("V7", $"Choice '{key}' is inactive and cannot be the recommended default.");
            }

            // Already the default: nothing moves, so nothing downstream should be told it did.
            // Reporting affected products here would have Spec 067 re-review unchanged content.
            if (target.IsRecommendedDefault)
            {
                return new RecommendedDefaultChangeResult(Map(group, group.Choices), []);
            }

            // V11 — a product that excludes the incoming default and has no override of its own
            // would be left with an unresolvable effective default. Reject naming those products
            // rather than committing and letting §6 drop the group from their storefront.
            var blocked = await ProductsExcludingChoiceWithoutOverrideAsync(tenantId, group, key, ct);
            if (blocked.Count > 0)
            {
                throw new OptionValidationException(
                    "V11",
                    $"Product(s) {string.Join(", ", blocked)} do not offer choice '{key}' and have no default of their own; " +
                    "give them an explicit default or widen their allowed choices before moving the group default.");
            }

            // Captured BEFORE the move, while the outgoing default is still the resolvable one, and
            // narrowed to products that actually inherit it — a product pinned to a still-valid
            // explicit default keeps exactly the preparation it had, so reporting it as changed
            // would send Spec 067 content review chasing products nothing happened to.
            var inheritingIds = await InheritingProductIdsAsync(tenantId, group, ct);
            var affectedProducts = await SlugsForAsync(tenantId, inheritingIds, ct);

            // Spec 067 §6 — the products whose STANDARD PREPARATION this move changes get their
            // default content block flagged for review, in the SAME transaction as the move: the
            // flag changes what resolution returns, so its ContentVersion bump must invalidate
            // cached content atomically with the default change (067 A18).
            await _contentReview.StageAsync(inheritingIds, ct);

            await using var transaction = _dbContext.Database.IsRelational()
                ? await _dbContext.Database.BeginTransactionAsync(ct)
                : null;

            // Bump the group's own concurrency token. A concurrent SetProductOptionGroupsAsync
            // validates a narrowing against the very default this move is about to change; without
            // contending on a shared row both writes pass their own checks and commit, leaving a
            // product with no resolvable default.
            TouchGroups([group]);

            foreach (var choice in group.Choices.Where(c => c.IsRecommendedDefault && c.Id != target.Id))
            {
                choice.IsRecommendedDefault = false;
            }
            await _dbContext.SaveChangesAsync(ct);

            target.IsRecommendedDefault = true;
            await _dbContext.SaveChangesAsync(ct);

            if (transaction is not null)
            {
                await transaction.CommitAsync(ct);
            }

            return new RecommendedDefaultChangeResult(Map(group, group.Choices), affectedProducts);
        }, cancellationToken);
    }

    public async Task SetProductOptionGroupsAsync(Guid productId, SetProductOptionGroupsCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        // Fetched tracked, not existence-checked: the full replacement below must contend on this
        // row (see the touch before SaveChanges), so the entity itself is needed.
        var product = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == productId && p.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException($"Product '{productId}' was not found.");

        var groups = await _dbContext.OptionGroups
            .Include(g => g.Choices)
            .Where(g => g.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        // Duplicate keys would create two live rows for the same (product, group): a raw
        // unique-index error on SQL Server, and on InMemory a group emitted twice with its
        // adjustment double-counted. Reject it as the authoring mistake it is.
        var duplicates = command.Groups
            .Select(line => NormalizeKey(line.GroupKey, "group"))
            .GroupBy(key => key, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Count > 0)
        {
            throw new OptionValidationException(
                "V6",
                $"Option group(s) {string.Join(", ", duplicates)} appear more than once in this narrowing.");
        }

        var replacements = new List<ProductOptionGroup>();

        foreach (var line in command.Groups)
        {
            var groupKey = NormalizeKey(line.GroupKey, "group");
            var group = groups.FirstOrDefault(g => g.Key == groupKey)
                ?? throw new OptionValidationException("V8", $"Unknown option group '{groupKey}'.");

            var groupChoiceKeys = group.Choices.Where(c => !c.IsDeleted).Select(c => c.Key).ToHashSet(StringComparer.Ordinal);

            // An explicitly supplied empty list is NOT the same as null. Null means "every active
            // choice"; an empty list means the operator selected nothing, and silently widening it
            // to the whole catalogue would expose choices they deliberately did not pick. Let it
            // fall through to the zero-active-choices rejection below.
            List<string>? allowed = null;
            if (line.AllowedChoiceKeys is not null)
            {
                allowed = line.AllowedChoiceKeys.Select(k => NormalizeKey(k, "choice")).Distinct(StringComparer.Ordinal).ToList();
                var unknown = allowed.Where(k => !groupChoiceKeys.Contains(k)).ToList();
                if (unknown.Count > 0)
                {
                    throw new OptionValidationException(
                        "V8",
                        $"Group '{groupKey}' has no choice(s) {string.Join(", ", unknown)}.");
                }
            }

            string? defaultKey = null;
            if (!string.IsNullOrWhiteSpace(line.DefaultChoiceKey))
            {
                defaultKey = NormalizeKey(line.DefaultChoiceKey, "choice");
                if (!groupChoiceKeys.Contains(defaultKey))
                {
                    throw new OptionValidationException("V8", $"Group '{groupKey}' has no choice '{defaultKey}'.");
                }
                if (allowed is not null && !allowed.Contains(defaultKey, StringComparer.Ordinal))
                {
                    throw new OptionValidationException(
                        "V8",
                        $"Default '{defaultKey}' is not among the allowed choices for group '{groupKey}'.");
                }
            }

            string? modeOverride = null;
            if (!string.IsNullOrWhiteSpace(line.SelectionModeOverride))
            {
                modeOverride = NormalizeSelectionMode(line.SelectionModeOverride);
            }

            // V8 — the narrowing must leave a resolvable effective default, or §6 would drop the
            // group at render time. Catch it here, at authoring, where the operator can fix it.
            var offered = allowed ?? group.Choices.Where(c => c.IsActive && !c.IsDeleted).Select(c => c.Key).ToList();
            var activeOffered = group.Choices
                .Where(c => c.IsActive && !c.IsDeleted && offered.Contains(c.Key, StringComparer.Ordinal))
                .ToList();

            if (activeOffered.Count == 0)
            {
                throw new OptionValidationException("V8", $"Group '{groupKey}' would offer no active choices for this product.");
            }

            var resolvesTo = defaultKey is not null && activeOffered.Any(c => c.Key == defaultKey)
                ? defaultKey
                : activeOffered.FirstOrDefault(c => c.IsRecommendedDefault)?.Key;

            if (resolvesTo is null)
            {
                throw new OptionValidationException(
                    "V8",
                    $"Group '{groupKey}' has no resolvable default for this product — the group's recommended default " +
                    "is not among the allowed choices, so set an explicit default for the product.");
            }

            replacements.Add(new ProductOptionGroup
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProductId = productId,
                OptionGroupId = group.Id,
                AllowedChoiceKeysJson = allowed is null ? null : JsonSerializer.Serialize(allowed),
                DefaultChoiceKey = defaultKey,
                SelectionModeOverride = modeOverride,
                SortOrder = line.SortOrder,
            });
        }

        var existing = await _dbContext.ProductOptionGroups
            .Where(x => x.TenantId == tenantId && x.ProductId == productId)
            .ToListAsync(cancellationToken);

        _dbContext.ProductOptionGroups.RemoveRange(existing);
        _dbContext.ProductOptionGroups.AddRange(replacements);

        // Serialize against a concurrent default move. Both operations validate against state the
        // other is about to change — narrowing a product to {A} while the group default moves
        // A→B passes both checks independently and commits a product with no resolvable default.
        // Touching the shared group rows makes the two writes contend on the group's concurrency
        // token, so the loser fails with a concurrency conflict instead of silently disagreeing.
        TouchGroups(groups.Where(g => replacements.Any(r => r.OptionGroupId == g.Id)));

        // Spec 067 §6 — a narrowing write can shift this product's effective default
        // combination (offered groups, explicit default, mode override). Flag its content block
        // in the same SaveChanges — but ONLY when the combination actually changes: an idempotent
        // save or a sort/allowed-set edit that leaves every effective default intact must not
        // withhold allergen declarations until an admin performs a pointless review.
        var signatureBefore = EffectiveDefaultSignature(existing, groups);
        var signatureAfter = EffectiveDefaultSignature(replacements, groups);
        if (!string.Equals(signatureBefore, signatureAfter, StringComparison.Ordinal))
        {
            await _contentReview.StageAsync([productId], cancellationToken);
        }

        // Serialize against a concurrent FULL REPLACE of the same product. The group rows cannot
        // do this: two replaces with disjoint group sets — or a replace racing an explicit clear —
        // share no group row, so both would commit and the survivors would merge into a union that
        // is neither caller's "full" replacement. The product row is the one thing every full
        // replacement of this product has in common, so every replacement contends on it.
        _dbContext.Entry(product).State = EntityState.Modified;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetUnitSurchargeAsync(Guid productId, SetUnitSurchargeCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var product = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == productId && p.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException($"Product '{productId}' was not found.");

        if (command.Amount is { } amount)
        {
            if (amount < 0)
            {
                throw new OptionValidationException("V6", "A unit surcharge cannot be negative.");
            }
            if (string.IsNullOrWhiteSpace(command.Currency))
            {
                throw new OptionValidationException(
                    "V10",
                    "A unit surcharge requires a currency — an undenominated amount would be reinterpreted if the storefront currency changed.");
            }

            product.UnitSurcharge = amount;
            product.UnitSurchargeCurrency = NormalizeCurrency(command.Currency);
        }
        else
        {
            product.UnitSurcharge = null;
            product.UnitSurchargeCurrency = null;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    // ─── Reads ───────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<OptionGroupDto>> GetCatalogueAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var groups = await _dbContext.OptionGroups
            .AsNoTracking()
            .Include(g => g.Choices)
            .Where(g => g.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        return groups
            .Where(g => includeInactive || IsServable(g))
            .OrderBy(g => g.SortOrder).ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => Map(g, includeInactive ? g.Choices : g.Choices.Where(c => c.IsActive)))
            .ToList();
    }

    public async Task<IReadOnlyList<EffectiveOptionGroupDto>> GetEffectiveOptionsAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var narrowings = await _dbContext.ProductOptionGroups
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.ProductId == productId)
            .ToListAsync(cancellationToken);

        if (narrowings.Count == 0)
        {
            return [];
        }

        var groupIds = narrowings.Select(n => n.OptionGroupId).ToList();
        var groups = await _dbContext.OptionGroups
            .AsNoTracking()
            .Include(g => g.Choices)
            .Where(g => g.TenantId == tenantId && groupIds.Contains(g.Id))
            .ToListAsync(cancellationToken);

        var result = new List<EffectiveOptionGroupDto>();

        foreach (var narrowing in narrowings.OrderBy(n => n.SortOrder))
        {
            var group = groups.FirstOrDefault(g => g.Id == narrowing.OptionGroupId);
            if (group is null || !IsServable(group))
            {
                continue;
            }

            var allowed = DeserializeAllowedKeys(narrowing.AllowedChoiceKeysJson);
            var choices = group.Choices
                .Where(c => c.IsActive && !c.IsDeleted)
                .Where(c => allowed is null || allowed.Contains(c.Key))
                .OrderBy(c => c.SortOrder).ThenBy(c => c.Key, StringComparer.Ordinal)
                .ToList();

            if (choices.Count == 0)
            {
                continue;
            }

            var defaultKey = ResolveDefaultKey(narrowing, choices);
            if (defaultKey is null)
            {
                // Authoring validation (V8/V9/V11) prevents this; if data drift ever produces it,
                // fail safe — the product loses one axis rather than rendering a default-less group.
                LogUnresolvableDefault(_logger, group.Key, productId);
                continue;
            }

            result.Add(new EffectiveOptionGroupDto(
                group.Key,
                group.Label,
                group.HelpText,
                narrowing.SelectionModeOverride ?? group.SelectionMode,
                group.Currency,
                narrowing.SortOrder,
                defaultKey,
                choices.Select(c => new EffectiveOptionChoiceDto(c.Key, c.Label, c.Note, c.Price, c.SortOrder)).ToList()));
        }

        return result;
    }

    // ─── Internals ───────────────────────────────────────────────────────────

    /// <summary>The choice-set half of servability: at least one active choice and exactly one
    /// active recommended default. Combined with the group flags by callers previewing a flip.</summary>
    private static bool HasServableChoiceSet(OptionGroup group)
    {
        var active = group.Choices.Where(c => c.IsActive && !c.IsDeleted).ToList();
        return active.Count > 0 && active.Count(c => c.IsRecommendedDefault) == 1;
    }

    /// <summary>The product's effective default combination as a comparable string: for each
    /// SERVABLE offered group (same rules as <see cref="GetEffectiveOptionsAsync"/>), its key,
    /// effective selection mode and resolved default key. Two narrowing states with equal
    /// signatures canonicalise the same all-defaults selection, so content review is only staged
    /// when the signatures differ (Spec 067 §6, precise form).</summary>
    private static string EffectiveDefaultSignature(
        IEnumerable<ProductOptionGroup> narrowings, IReadOnlyList<OptionGroup> groups)
    {
        var parts = new List<string>();
        foreach (var narrowing in narrowings)
        {
            var group = groups.FirstOrDefault(g => g.Id == narrowing.OptionGroupId);
            if (group is null || !IsServable(group))
            {
                continue;
            }

            var allowed = DeserializeAllowedKeys(narrowing.AllowedChoiceKeysJson);
            var choices = group.Choices
                .Where(c => c.IsActive && !c.IsDeleted)
                .Where(c => allowed is null || allowed.Contains(c.Key))
                .ToList();
            if (choices.Count == 0)
            {
                continue;
            }

            var defaultKey = ResolveDefaultKey(narrowing, choices);
            if (defaultKey is null)
            {
                continue;
            }

            var mode = narrowing.SelectionModeOverride ?? group.SelectionMode;
            parts.Add($"{group.Key}:{mode}:{defaultKey}");
        }

        parts.Sort(StringComparer.Ordinal);
        return string.Join(";", parts);
    }

    /// <summary>A group is servable only when it is active, has at least one active choice, and has
    /// exactly one active recommended default. Half-authored groups simply do not appear.</summary>
    private static bool IsServable(OptionGroup group)
    {
        if (!group.IsActive || group.IsDeleted)
        {
            return false;
        }

        var active = group.Choices.Where(c => c.IsActive && !c.IsDeleted).ToList();
        return active.Count > 0 && active.Count(c => c.IsRecommendedDefault) == 1;
    }

    private static string? ResolveDefaultKey(ProductOptionGroup narrowing, IReadOnlyList<OptionChoice> offeredChoices)
    {
        if (narrowing.DefaultChoiceKey is { Length: > 0 } explicitKey &&
            offeredChoices.Any(c => c.Key == explicitKey))
        {
            return explicitKey;
        }

        return offeredChoices.FirstOrDefault(c => c.IsRecommendedDefault)?.Key;
    }

    private static HashSet<string>? DeserializeAllowedKeys(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        var keys = JsonSerializer.Deserialize<List<string>>(json);
        return keys is null or { Count: 0 } ? null : keys.ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Products whose narrowing excludes <paramref name="choiceKey"/> and that have no
    /// default of their own — they would be left unresolvable by a default move (V11).</summary>
    private async Task<List<string>> ProductsExcludingChoiceWithoutOverrideAsync(
        Guid tenantId, OptionGroup group, string choiceKey, CancellationToken cancellationToken)
    {
        var narrowings = await _dbContext.ProductOptionGroups
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.OptionGroupId == group.Id)
            .ToListAsync(cancellationToken);

        var activeKeys = ActiveChoiceKeys(group);

        var affected = new List<Guid>();
        foreach (var narrowing in narrowings)
        {
            if (OverrideResolves(narrowing, activeKeys))
            {
                continue;
            }

            var allowed = DeserializeAllowedKeys(narrowing.AllowedChoiceKeysJson);
            if (allowed is not null && !allowed.Contains(choiceKey))
            {
                affected.Add(narrowing.ProductId);
            }
        }

        return await SlugsForAsync(tenantId, affected, cancellationToken);
    }

    private static HashSet<string> ActiveChoiceKeys(OptionGroup group) => group.Choices
        .Where(c => c.IsActive && !c.IsDeleted)
        .Select(c => c.Key)
        .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Whether a product's own default still stands on its own. A stored override only counts if it
    /// STILL resolves — the choice must be active and within the narrowing. A product that overrode
    /// to a since-deactivated choice is silently relying on the group default, so it moves when the
    /// group default moves, exactly like a product with no override at all.
    /// </summary>
    private static bool OverrideResolves(ProductOptionGroup narrowing, HashSet<string> activeKeys)
    {
        if (narrowing.DefaultChoiceKey is not { Length: > 0 } key || !activeKeys.Contains(key))
        {
            return false;
        }

        var allowed = DeserializeAllowedKeys(narrowing.AllowedChoiceKeysJson);
        return allowed is null || allowed.Contains(key);
    }

    /// <summary>Products that would lose their only resolvable default if this choice were
    /// deactivated (V9).</summary>
    private async Task<List<string>> ProductsLosingTheirDefaultAsync(
        Guid tenantId, OptionChoice choice, CancellationToken cancellationToken)
    {
        var narrowings = await _dbContext.ProductOptionGroups
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.OptionGroupId == choice.OptionGroupId)
            .ToListAsync(cancellationToken);

        if (narrowings.Count == 0)
        {
            return [];
        }

        var group = await _dbContext.OptionGroups
            .AsNoTracking()
            .Include(g => g.Choices)
            .FirstAsync(g => g.Id == choice.OptionGroupId, cancellationToken);

        var affected = new List<Guid>();
        foreach (var narrowing in narrowings)
        {
            var allowed = DeserializeAllowedKeys(narrowing.AllowedChoiceKeysJson);
            var remaining = group.Choices
                .Where(c => c.IsActive && !c.IsDeleted && c.Id != choice.Id)
                .Where(c => allowed is null || allowed.Contains(c.Key))
                .ToList();

            var stillResolves =
                (narrowing.DefaultChoiceKey is { Length: > 0 } key && remaining.Any(c => c.Key == key)) ||
                remaining.Any(c => c.IsRecommendedDefault);

            if (!stillResolves)
            {
                affected.Add(narrowing.ProductId);
            }
        }

        return await SlugsForAsync(tenantId, affected, cancellationToken);
    }

    /// <summary>Marks the shared option-group rows modified so concurrent writers that validate
    /// against each other's state contend on the group's rowversion instead of both committing.</summary>
    private void TouchGroups(IEnumerable<OptionGroup> groups)
    {
        foreach (var group in groups)
        {
            _dbContext.Entry(group).State = EntityState.Modified;
        }
    }

    /// <summary>Slugs of the products whose effective default actually follows this group's
    /// recommended default — returned by a default move so dependent capabilities (Spec 067 content
    /// review) know what genuinely changed. Products pinned to a still-resolvable default of their
    /// own keep the preparation they had and are deliberately excluded.</summary>
    private async Task<List<Guid>> InheritingProductIdsAsync(
        Guid tenantId, OptionGroup group, CancellationToken cancellationToken)
    {
        var narrowings = await _dbContext.ProductOptionGroups
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.OptionGroupId == group.Id)
            .ToListAsync(cancellationToken);

        var activeKeys = ActiveChoiceKeys(group);

        return narrowings
            .Where(n => !OverrideResolves(n, activeKeys))
            .Select(n => n.ProductId)
            .Distinct()
            .ToList();
    }

    private async Task<List<string>> SlugsForAsync(Guid tenantId, List<Guid> productIds, CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.Products
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && productIds.Contains(p.Id))
            .Select(p => p.Slug)
            .ToListAsync(cancellationToken);
    }

    private static string NormalizeKey(string? value, string what)
    {
        var key = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (!KeyPattern().IsMatch(key))
        {
            throw new OptionValidationException(
                "V6",
                $"'{value}' is not a valid {what} key; use 1–64 characters of a-z, 0-9 or '-'.");
        }
        return key;
    }

    private static string NormalizeSelectionMode(string? value)
    {
        var mode = (value ?? string.Empty).Trim();
        mode = mode.Length == 0
            ? OptionSelectionModes.One
            : char.ToUpperInvariant(mode[0]) + mode[1..].ToLowerInvariant();

        if (!OptionSelectionModes.IsKnown(mode))
        {
            throw new OptionValidationException(
                "V12",
                $"'{value}' is not a valid selection mode; expected '{OptionSelectionModes.One}' or '{OptionSelectionModes.Multi}'.");
        }
        return mode;
    }

    private static string NormalizeCurrency(string? value)
    {
        var currency = (value ?? string.Empty).Trim().ToUpperInvariant();
        if (currency.Length != 3)
        {
            throw new OptionValidationException("V6", $"'{value}' is not a valid ISO currency code.");
        }
        return currency;
    }

    private static string RequireLabel(string? label)
        => string.IsNullOrWhiteSpace(label)
            ? throw new OptionValidationException("V6", "A label is required.")
            : label.Trim();

    private static OptionGroupDto Map(OptionGroup g, IEnumerable<OptionChoice> choices) => new(
        g.Id, g.Key, g.Label, g.HelpText, g.SelectionMode, g.Currency, g.SortOrder, g.IsActive,
        choices
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Key, StringComparer.Ordinal)
            .Select(MapChoice)
            .ToList());

    private static OptionChoiceDto MapChoice(OptionChoice c) => new(
        c.Id, c.Key, c.Label, c.Note, c.Price, c.IsRecommendedDefault, c.SortOrder, c.IsActive);

    [GeneratedRegex("^[a-z0-9-]{1,64}$")]
    private static partial Regex KeyPattern();

    [LoggerMessage(
        EventId = 6601,
        Level = LogLevel.Warning,
        Message = "Option group {GroupKey} has no resolvable default for product {ProductId}; omitting it from the product's effective options.")]
    private static partial void LogUnresolvableDefault(ILogger logger, string groupKey, Guid productId);
}
