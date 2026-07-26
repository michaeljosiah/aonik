using System.Text.RegularExpressions;

using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aonik.Commerce.Services.Catalog;

/// <summary>Curated collection authoring and reads (Spec 070 §5/§10/§11).</summary>
internal sealed partial class CollectionService : ICollectionService
{
    private readonly CommerceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<CollectionService> _logger;

    private readonly IExtrasCatalogService _extras;

    public CollectionService(
        CommerceDbContext dbContext,
        ITenantProvider tenantProvider,
        ILogger<CollectionService> logger,
        IExtrasCatalogService extras)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _logger = logger;
        _extras = extras;
    }

    // ─── Public reads ────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<PublicCollectionDto>> ListPublicAsync(
        string? kind = null, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        // The kind filter is matched case-insensitively and validated against the known values:
        // an exact-compare would silently return nothing under case-sensitive collations, and an
        // unknown kind is a storefront bug that should be loud (§10).
        string? normalizedKind = null;
        if (!string.IsNullOrWhiteSpace(kind))
        {
            normalizedKind = NormalizeKind(kind);
        }

        var collections = await _dbContext.Collections
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.IsActive)
            .Where(c => normalizedKind == null || c.Kind == normalizedKind)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Title)
            .ToListAsync(cancellationToken);

        if (collections.Count == 0)
        {
            return [];
        }

        var members = await LoadActiveMembersAsync(tenantId, collections.Select(c => c.Id).ToList(), cancellationToken);

        return collections
            .Select(c => MapPublic(c, members.TryGetValue(c.Id, out var list) ? list : []))
            .ToList();
    }

    public async Task<PublicCollectionDto?> GetPublicBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var collection = await _dbContext.Collections
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Slug == slug && c.IsActive, cancellationToken);
        if (collection is null)
        {
            return null;
        }

        var members = await LoadActiveMembersAsync(tenantId, [collection.Id], cancellationToken);
        return MapPublic(collection, members.TryGetValue(collection.Id, out var list) ? list : []);
    }

    // ─── Admin ───────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<AdminCollectionSummaryDto>> ListAdminAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        return await _dbContext.Collections
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Title)
            .Select(c => new AdminCollectionSummaryDto(
                c.Id, c.Slug, c.Title, c.Subtitle, c.Kind, c.SortOrder, c.IsActive, c.Items.Count))
            .ToListAsync(cancellationToken);
    }

    public async Task<AdminCollectionDto> GetAdminAsync(Guid collectionId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var collection = await _dbContext.Collections
            .AsNoTracking()
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == collectionId && c.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException($"Collection '{collectionId}' was not found.");

        var dto = await MapAdminAsync(tenantId, collection, cancellationToken);

        // Spec 078 dependency — the extras collection is the one place members
        // carry retail pricing state, so the admin can see WHICH retained member
        // the public rail skips as unpriceable (it omits and counts; the admin
        // keeps and marks). Sourced from the REAL public read, never simulated.
        var extrasSlug = await _extras.GetConfiguredSlugAsync(cancellationToken);
        if (string.Equals(collection.Slug, extrasSlug, StringComparison.OrdinalIgnoreCase))
        {
            var rail = await _extras.GetExtrasAsync(cancellationToken);
            var byProduct = rail.Rows.ToDictionary(r => r.ProductId);

            // IsPriceable = false is a PRICING verdict — reserve it for members the
            // rail would serve but for a missing price. An Active member can also be
            // missing from the rail because it is structurally ineligible (not a
            // Simple product, or no active variant); those are null, or the operator
            // would be told to repair pricing when the problem is the product itself.
            var absentActiveIds = dto.Items
                .Where(i => i.Status == ProductStatuses.Active && !byProduct.ContainsKey(i.ProductId))
                .Select(i => i.ProductId)
                .ToList();
            var eligibleKinds = await _dbContext.Products.AsNoTracking()
                .Where(p => p.TenantId == tenantId && absentActiveIds.Contains(p.Id) && p.Kind == ProductKinds.Simple)
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);
            var withActiveVariant = await _dbContext.ProductVariants.AsNoTracking()
                .Where(v => v.TenantId == tenantId && absentActiveIds.Contains(v.ProductId) && v.IsActive)
                .Select(v => v.ProductId)
                .Distinct()
                .ToListAsync(cancellationToken);
            var unpriceable = eligibleKinds.Intersect(withActiveVariant).ToHashSet();

            dto = dto with
            {
                Items = dto.Items
                    .Select(i => byProduct.TryGetValue(i.ProductId, out var row)
                        ? i with { UnitPrice = row.UnitPrice, Currency = row.Currency, IsPriceable = true }
                        : i with { IsPriceable = unpriceable.Contains(i.ProductId) ? false : null })
                    .ToList(),
            };
        }

        return dto;
    }

    public async Task<AdminCollectionDto> CreateAsync(CreateCollectionCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var slug = NormalizeSlug(command.Slug);
        if (await _dbContext.Collections.AnyAsync(c => c.TenantId == tenantId && c.Slug == slug, cancellationToken))
        {
            throw new StorefrontValidationException($"A collection with slug '{slug}' already exists.");
        }

        var collection = new Collection
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Slug = slug,
            Title = RequireTitle(command.Title),
            Subtitle = BoundSubtitle(command.Subtitle),
            Kind = NormalizeKind(command.Kind),
            SortOrder = command.SortOrder,
            IsActive = true,
        };

        _dbContext.Collections.Add(collection);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await MapAdminAsync(tenantId, collection, cancellationToken);
    }

    public async Task<AdminCollectionDto> UpdateAsync(
        Guid collectionId, UpdateCollectionCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var collection = await _dbContext.Collections
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == collectionId && c.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException($"Collection '{collectionId}' was not found.");

        collection.Title = RequireTitle(command.Title);

        // Omitted means unchanged: a rename must never be able to deactivate a collection,
        // reorder the homepage, re-kind a rail, or erase a subtitle as a side effect. Clearing
        // the subtitle is said explicitly — a nullable string cannot carry both meanings.
        if (command.ClearSubtitle)
        {
            collection.Subtitle = null;
        }
        else if (command.Subtitle is not null)
        {
            collection.Subtitle = BoundSubtitle(command.Subtitle);
        }

        if (command.Kind is not null)
        {
            collection.Kind = NormalizeKind(command.Kind);
        }
        if (command.SortOrder is { } sortOrder)
        {
            collection.SortOrder = sortOrder;
        }
        if (command.IsActive is { } isActive)
        {
            collection.IsActive = isActive;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await MapAdminAsync(tenantId, collection, cancellationToken);
    }

    public async Task<AdminCollectionDto> ReplaceItemsAsync(
        Guid collectionId, ReplaceCollectionItemsCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var collection = await _dbContext.Collections
            .FirstOrDefaultAsync(c => c.Id == collectionId && c.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException($"Collection '{collectionId}' was not found.");

        var lines = command.Items ?? throw new StorefrontValidationException(
            "An 'items' array is required. To empty the collection, send an explicit empty array.");

        // Duplicate ranks would make curated order nondeterministic (A12); duplicate products
        // would collide with the membership unique index. Both are authoring mistakes — name them.
        var duplicateProducts = lines.GroupBy(l => l.ProductId).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicateProducts.Count > 0)
        {
            throw new StorefrontValidationException(
                $"Product(s) {string.Join(", ", duplicateProducts)} appear more than once.");
        }

        var duplicateRanks = lines.GroupBy(l => l.Rank).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicateRanks.Count > 0)
        {
            throw new StorefrontValidationException(
                $"Rank(s) {string.Join(", ", duplicateRanks)} appear more than once; ranks must be unique so curated order is deterministic.");
        }

        // Ranks are ordinal positions and must be non-negative — which also reserves the negative
        // space for the index-safe reorder below: phase 1 parks surviving rows on negative ranks
        // no request can ever collide with.
        if (lines.Any(l => l.Rank < 0))
        {
            throw new StorefrontValidationException("Ranks must be non-negative.");
        }

        // Members must exist in the tenant — any status: Active is enforced at read time, not
        // membership time, so a draft product can be staged before launch (A9).
        var productIds = lines.Select(l => l.ProductId).ToList();
        var known = await _dbContext.Products
            .Where(p => p.TenantId == tenantId && productIds.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);
        var missing = productIds.Except(known).ToList();
        if (missing.Count > 0)
        {
            throw new StorefrontValidationException(
                $"Product(s) {string.Join(", ", missing)} do not exist in this tenant.");
        }

        // The replacement runs in two index-safe phases inside one transaction, because the
        // filtered unique index on (TenantId, CollectionId, Rank) is evaluated PER STATEMENT: a
        // routine swap (A:1,B:2 → A:2,B:1) would otherwise violate it mid-flight when the first
        // UPDATE lands on a rank its neighbour has not yet vacated — the same per-statement
        // reality the recommended-default demote-before-promote handles. Phase 1 parks every
        // surviving row on a negative rank (unreachable by requests, which are validated
        // non-negative) and soft-deletes removals, emptying the live rank space; phase 2 assigns
        // final ranks, revives re-added members, and inserts new ones into a vacated index.
        //
        // It runs through the execution strategy (EnableRetryOnFailure rejects bare user
        // transactions), and reloads its working set INSIDE each attempt: a replayed delegate
        // must re-stage from what the database actually holds, not from flags and ranks a failed
        // attempt already mutated in memory.
        var requested = productIds.ToHashSet();
        var strategy = _dbContext.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async ct =>
        {
            await _dbContext.Entry(collection).ReloadAsync(ct);

            // A replayed attempt must not re-stage inserts a failed attempt left tracked as
            // Added — they would double-insert against the membership unique index.
            foreach (var entry in _dbContext.ChangeTracker.Entries<CollectionItem>()
                .Where(e => e.State == EntityState.Added && e.Entity.CollectionId == collectionId)
                .ToList())
            {
                entry.State = EntityState.Detached;
            }

            // Full replace over EVERY row including soft-deleted ones: a previously removed
            // member must be revived rather than re-inserted — an insert would collide with the
            // soft-deleted row's (collection, product) key. IncludeSoftDeleted keeps tenant
            // scoping intact; only the soft-delete filter lifts.
            var existing = await _dbContext.CollectionItems
                .IncludeSoftDeleted()
                .Where(i => i.TenantId == tenantId && i.CollectionId == collectionId)
                .ToListAsync(ct);

            foreach (var row in existing)
            {
                await _dbContext.Entry(row).ReloadAsync(ct);
            }

            await using var transaction = _dbContext.Database.IsRelational()
                ? await _dbContext.Database.BeginTransactionAsync(ct)
                : null;

            // Serialize concurrent full-replaces on the collection row: two replaces with
            // disjoint member sets share no item row, so without this both would commit and the
            // memberships would merge into a union that is neither caller's "full" replacement.
            _dbContext.Entry(collection).State = EntityState.Modified;

            var byProduct = existing.ToDictionary(i => i.ProductId);

            // Phase 1 — vacate the live rank space.
            var temp = -1;
            foreach (var row in existing.Where(i => !i.IsDeleted))
            {
                if (requested.Contains(row.ProductId))
                {
                    row.Rank = temp--;
                }
                else
                {
                    _dbContext.CollectionItems.Remove(row);
                }
            }
            await _dbContext.SaveChangesAsync(ct);

            // Phase 2 — final ranks into an emptied index.
            foreach (var line in lines)
            {
                if (byProduct.TryGetValue(line.ProductId, out var row))
                {
                    row.Rank = line.Rank;
                    row.IsDeleted = false;
                    row.DeletedAt = null;
                }
                else
                {
                    _dbContext.CollectionItems.Add(new CollectionItem
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        CollectionId = collectionId,
                        ProductId = line.ProductId,
                        Rank = line.Rank,
                    });
                }
            }
            await _dbContext.SaveChangesAsync(ct);

            if (transaction is not null)
            {
                await transaction.CommitAsync(ct);
            }
        }, cancellationToken);

        return await MapAdminAsync(tenantId, collection, cancellationToken);
    }

    // ─── Internals ───────────────────────────────────────────────────────────

    /// <summary>Ranked, ACTIVE member products per collection — the public shape. A staged draft
    /// stays invisible here and surfaces the moment the product itself activates (A9).</summary>
    private async Task<Dictionary<Guid, List<ProductSummaryDto>>> LoadActiveMembersAsync(
        Guid tenantId, List<Guid> collectionIds, CancellationToken cancellationToken)
    {
        var items = await _dbContext.CollectionItems
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && collectionIds.Contains(i.CollectionId))
            .ToListAsync(cancellationToken);

        var productIds = items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _dbContext.Products
            .AsNoTracking()
            .Include(p => p.Media)
            .Include(p => p.Variants)
            .Where(p => p.TenantId == tenantId && productIds.Contains(p.Id) && p.Status == ProductStatuses.Active)
            .ToListAsync(cancellationToken);
        var productById = products.ToDictionary(p => p.Id);

        return items
            .GroupBy(i => i.CollectionId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(i => i.Rank)
                    .Where(i => productById.ContainsKey(i.ProductId))
                    .Select(i => ProductSummaryMapper.Map(productById[i.ProductId], _logger))
                    .ToList());
    }

    private static PublicCollectionDto MapPublic(Collection collection, List<ProductSummaryDto> members) => new(
        collection.Id, collection.Slug, collection.Title, collection.Subtitle, collection.Kind,
        collection.SortOrder, members);

    private async Task<AdminCollectionDto> MapAdminAsync(
        Guid tenantId, Collection collection, CancellationToken cancellationToken)
    {
        var items = await _dbContext.CollectionItems
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && i.CollectionId == collection.Id)
            .ToListAsync(cancellationToken);

        var productIds = items.Select(i => i.ProductId).ToList();
        var names = await _dbContext.Products
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Slug, p.Name, p.Status })
            .ToListAsync(cancellationToken);
        var nameById = names.ToDictionary(p => p.Id);

        return new AdminCollectionDto(
            collection.Id, collection.Slug, collection.Title, collection.Subtitle, collection.Kind,
            collection.SortOrder, collection.IsActive,
            items
                .OrderBy(i => i.Rank)
                .Select(i => nameById.TryGetValue(i.ProductId, out var p)
                    ? new AdminCollectionItemDto(i.ProductId, p.Slug, p.Name, p.Status, i.Rank)
                    : new AdminCollectionItemDto(i.ProductId, string.Empty, string.Empty, "Missing", i.Rank))
                .ToList());
    }

    private static string NormalizeSlug(string? value)
    {
        var slug = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (!SlugPattern().IsMatch(slug))
        {
            throw new StorefrontValidationException(
                $"'{value}' is not a valid collection slug; use 1–64 characters of a-z, 0-9 or '-'.");
        }
        return slug;
    }

    private static string NormalizeKind(string? value)
    {
        var kind = (value ?? string.Empty).Trim();
        kind = kind.Length == 0 ? CollectionKinds.Curated : char.ToUpperInvariant(kind[0]) + kind[1..].ToLowerInvariant();

        if (!CollectionKinds.IsKnown(kind))
        {
            throw new StorefrontValidationException(
                $"'{value}' is not a valid collection kind; expected {CollectionKinds.Featured}, {CollectionKinds.Curated} or {CollectionKinds.Custom}.");
        }
        return kind;
    }

    /// <summary>Non-blank AND within the mapped column bound — an oversized value would pass
    /// here and then fail SaveChanges as a 500 on SQL Server, exactly the class the media-URL
    /// bound fix addressed.</summary>
    private static string RequireTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new StorefrontValidationException("A title is required.");
        }

        var trimmed = title.Trim();
        return trimmed.Length <= 128
            ? trimmed
            : throw new StorefrontValidationException("A title is at most 128 characters.");
    }

    private static string? BoundSubtitle(string? subtitle)
        => subtitle is { Length: > 256 }
            ? throw new StorefrontValidationException("A subtitle is at most 256 characters.")
            : subtitle;

    [GeneratedRegex("^[a-z0-9-]{1,64}$")]
    private static partial Regex SlugPattern();
}
