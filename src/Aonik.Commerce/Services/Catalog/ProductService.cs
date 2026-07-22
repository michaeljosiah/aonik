using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;

// Targeted alias: importing the whole SharedKernel.Abstractions namespace would make PagedResult<>
// ambiguous with the Commerce-local contract of the same name.
using NotFoundException = Aonik.SharedKernel.Abstractions.NotFoundException;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aonik.Commerce.Services.Catalog;

/// <summary>Catalog management over <see cref="CommerceDbContext"/> (Spec 042 §8/§12), including
/// the Spec 070 merchandised browse (facets, collections, hidden-keyword search).</summary>
internal sealed partial class ProductService : IProductService
{
    private readonly CommerceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IProductOptionService _options;
    private readonly ILogger<ProductService> _logger;

    public ProductService(
        CommerceDbContext dbContext,
        ITenantProvider tenantProvider,
        IProductOptionService options,
        ILogger<ProductService> logger)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _options = options;
        _logger = logger;
    }

    public async Task<ProductDto> CreateProductAsync(CreateProductCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        if (await _dbContext.Products.AnyAsync(p => p.TenantId == tenantId && p.Slug == command.Slug, cancellationToken))
        {
            throw new InvalidOperationException($"A product with slug '{command.Slug}' already exists.");
        }

        var product = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Slug = command.Slug,
            Name = command.Name,
            Description = command.Description,
            Status = command.Status,
            Kind = command.Kind,
            CategoryId = command.CategoryId,
            // JSON hygiene applies at create too (§11) — the browse path reads these defensively,
            // but a 400 at authoring beats a warning-logged half-rendered row later.
            TagsJson = command.TagsJson is null ? "[]" : ValidateStringArrayJson(command.TagsJson, "tagsJson"),
            AttributesJson = command.AttributesJson is null ? "{}" : ValidateObjectJson(command.AttributesJson, "attributesJson"),
            BundlePricingMode = command.BundlePricingMode,
            BundleFixedAmount = command.BundleFixedAmount,
            BundlePremium = command.BundlePremium,
            BundleCurrency = command.BundleCurrency,
        };

        foreach (var line in command.Variants ?? Array.Empty<CreateVariantLine>())
        {
            product.Variants.Add(new ProductVariant
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProductId = product.Id,
                Sku = line.Sku,
                Name = line.Name,
                OptionsJson = line.OptionsJson ?? "{}",
                WeightGrams = line.WeightGrams,
                IsActive = true,
            });
        }

        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return (await GetProductAsync(product.Id, cancellationToken))!;
    }

    public async Task<ProductDto?> GetProductAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var product = await QueryWithGraph()
            .FirstOrDefaultAsync(p => p.Id == productId && p.TenantId == tenantId, cancellationToken);
        if (product is null) return null;
        return Map(product, await _options.GetEffectiveOptionsAsync(product.Id, cancellationToken));
    }

    public async Task<ProductDto?> GetProductBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var product = await QueryWithGraph()
            .FirstOrDefaultAsync(p => p.Slug == slug && p.TenantId == tenantId, cancellationToken);
        if (product is null) return null;
        return Map(product, await _options.GetEffectiveOptionsAsync(product.Id, cancellationToken));
    }

    public async Task<PagedResult<ProductSummaryDto>> ListProductsAsync(ListProductsQuery query, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var sort = ResolveSort(query);

        var q = _dbContext.Products.AsNoTracking().Where(p => p.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(query.Kind)) q = q.Where(p => p.Kind == query.Kind);
        if (query.CategoryId is { } cat) q = q.Where(p => p.CategoryId == cat);
        if (!string.IsNullOrWhiteSpace(query.Status)) q = q.Where(p => p.Status == query.Status);

        // Facet evaluation and keyword search are deliberately in-memory over the SQL-prefiltered
        // candidate set (Spec 070 §3): at catalogue scale (tens of products) this is exact,
        // provider-independent — the InMemory tests exercise the REAL matching logic, not a
        // translation's approximation of it — and JSON-aware, where a LIKE over the raw column
        // would match escaped text or JSON syntax (§7). A search index is a different spec when a
        // tenant has thousands of products.
        var candidates = await q
            .Include(p => p.Media)
            .Include(p => p.Variants)
            .ToListAsync(cancellationToken);

        var rows = candidates.Select(ParseRow).ToList();

        // Collection membership filter + the rank order it carries (§6).
        Dictionary<Guid, int>? ranks = null;
        if (!string.IsNullOrWhiteSpace(query.Collection))
        {
            ranks = await CollectionRanksAsync(tenantId, query.Collection.Trim(), cancellationToken);
            rows = rows.Where(r => ranks.ContainsKey(r.Product.Id)).ToList();
        }

        // Facets: OR within a group, AND across groups (§6). Every submitted key and value is
        // validated first — a storefront bug should be loud, never silently unfiltered.
        if (query.Facets is { Count: > 0 } facets)
        {
            foreach (var predicate in await BuildFacetPredicatesAsync(tenantId, facets, cancellationToken))
            {
                rows = rows.Where(predicate).ToList();
            }
        }

        // Search ANDs with everything else: name, slug (dropping it would regress slug-based
        // discovery), description, and every hidden keyword — matched as logical array entries.
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(r =>
                    r.Product.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || r.Product.Slug.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || r.Product.Description.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || r.SearchKeywords.Any(k => k.Contains(term, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        var ordered = sort switch
        {
            ProductSortOrders.Rank => rows.OrderBy(r => ranks![r.Product.Id]).ThenBy(r => r.Product.Name, StringComparer.OrdinalIgnoreCase),
            ProductSortOrders.Newest => rows.OrderByDescending(r => r.Product.CreatedAt).ThenBy(r => r.Product.Id),
            _ => rows.OrderBy(r => r.Product.Name, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.Product.Slug, StringComparer.Ordinal),
        };

        var total = rows.Count;
        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, 200);

        var items = ordered
            .Skip((page - 1) * size)
            .Take(size)
            .Select(r => ProductSummaryMapper.Map(r.Product, r.Tags))
            .ToList();

        return new PagedResult<ProductSummaryDto>(items, total, page, size);
    }

    // ─── Spec 070 §6 — browse internals ──────────────────────────────────────

    /// <summary>A candidate row with its JSON parsed once, defensively: a malformed legacy row
    /// renders with empty tags, drops out of facet matching, and logs a warning — it never 500s
    /// the public browse (§11 / A13).</summary>
    private sealed record BrowseRow(
        Product Product,
        IReadOnlyList<string> Tags,
        JsonElement? Attributes,
        IReadOnlyList<string> SearchKeywords);

    private BrowseRow ParseRow(Product product)
    {
        var tags = StorefrontJson.ParseStringArray(product.TagsJson, out var tagsMalformed);
        var attributes = StorefrontJson.ParseObject(product.AttributesJson, out var attributesMalformed);
        var keywords = StorefrontJson.ParseStringArray(product.SearchKeywordsJson, out var keywordsMalformed);

        if (tagsMalformed || attributesMalformed || keywordsMalformed)
        {
            LogMalformedProductJson(_logger, product.Slug, product.Id);
        }

        return new BrowseRow(product, tags, attributes, keywords);
    }

    private static string ResolveSort(ListProductsQuery query)
    {
        var sort = string.IsNullOrWhiteSpace(query.Sort)
            ? (string.IsNullOrWhiteSpace(query.Collection) ? ProductSortOrders.Name : ProductSortOrders.Rank)
            : query.Sort.Trim().ToLowerInvariant();

        if (sort is not (ProductSortOrders.Name or ProductSortOrders.Newest or ProductSortOrders.Rank))
        {
            throw new StorefrontValidationException(
                $"Unknown sort '{query.Sort}'; expected {ProductSortOrders.Name}, {ProductSortOrders.Newest} or {ProductSortOrders.Rank}.");
        }

        // Rank is curated order WITHIN one collection; outside one it has no meaning (§6).
        if (sort == ProductSortOrders.Rank && string.IsNullOrWhiteSpace(query.Collection))
        {
            throw new StorefrontValidationException("sort=rank requires a collection filter.");
        }

        return sort;
    }

    private async Task<Dictionary<Guid, int>> CollectionRanksAsync(
        Guid tenantId, string slug, CancellationToken cancellationToken)
    {
        var collection = await _dbContext.Collections
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Slug == slug && c.IsActive, cancellationToken)
            ?? throw new StorefrontValidationException($"Unknown collection '{slug}'.");

        return await _dbContext.CollectionItems
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && i.CollectionId == collection.Id)
            .ToDictionaryAsync(i => i.ProductId, i => i.Rank, cancellationToken);
    }

    /// <summary>One predicate per requested facet group — AND across groups; each predicate ORs
    /// its selected options (§6). Throws for unknown keys, retired groups, and values that are
    /// not among the group's declared option tokens (labels included: submitting a label where a
    /// value belongs is exactly the storefront bug that must be loud, A15).</summary>
    private async Task<List<Func<BrowseRow, bool>>> BuildFacetPredicatesAsync(
        Guid tenantId,
        IReadOnlyDictionary<string, IReadOnlyList<string>> facets,
        CancellationToken cancellationToken)
    {
        var requestedKeys = facets.Keys.ToList();
        var groups = await _dbContext.FacetGroups
            .AsNoTracking()
            .Where(g => g.TenantId == tenantId && g.IsActive && requestedKeys.Contains(g.Key))
            .ToListAsync(cancellationToken);

        var unknown = requestedKeys
            .Where(k => groups.All(g => !string.Equals(g.Key, k, StringComparison.Ordinal)))
            .ToList();
        if (unknown.Count > 0)
        {
            throw new StorefrontValidationException($"Unknown facet key(s): {string.Join(", ", unknown)}.");
        }

        var predicates = new List<Func<BrowseRow, bool>>();

        foreach (var group in groups)
        {
            var options = FacetDefinitions.ParseLenient(group.OptionsJson);
            var selectedValues = facets[group.Key]
                .Select(v => v.Trim())
                .Where(v => v.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (selectedValues.Count == 0)
            {
                // An unselected group restricts nothing (§6); a key submitted with no values is
                // treated the same rather than filtering everything out.
                continue;
            }

            var declared = options.Select(o => o.Value).ToHashSet(StringComparer.Ordinal);
            var invalid = selectedValues.Where(v => !declared.Contains(v)).ToList();
            if (invalid.Count > 0)
            {
                throw new StorefrontValidationException(
                    $"Facet '{group.Key}' has no option value(s) {string.Join(", ", invalid)}; submit option values, not labels.");
            }

            var selected = options.Where(o => selectedValues.Contains(o.Value, StringComparer.Ordinal)).ToList();

            predicates.Add(group.MatchKind switch
            {
                FacetMatchKinds.Tag => row => row.Tags.Any(t => selectedValues.Contains(t, StringComparer.OrdinalIgnoreCase)),
                FacetMatchKinds.Attribute => BuildAttributePredicate(group, selectedValues),
                FacetMatchKinds.Range => BuildRangePredicate(group, selected),
                FacetMatchKinds.Category => await BuildCategoryPredicateAsync(tenantId, selectedValues, cancellationToken),
                _ => _ => false, // Unknown stored kind: matches nothing rather than everything.
            });
        }

        return predicates;
    }

    private static Func<BrowseRow, bool> BuildAttributePredicate(FacetGroup group, List<string> selectedValues)
        => row =>
        {
            var value = StorefrontJson.ReadString(row.Attributes, group.SourcePath ?? string.Empty);
            return value is not null && selectedValues.Contains(value, StringComparer.OrdinalIgnoreCase);
        };

    private static Func<BrowseRow, bool> BuildRangePredicate(FacetGroup group, List<FacetOption> selectedBands)
        => row =>
        {
            // Half-open [min, max): min inclusive, max exclusive. A product missing the value
            // matches no band (§6) — absence of data is never a match.
            var value = StorefrontJson.ReadNumber(row.Attributes, group.SourcePath ?? string.Empty);
            return value is { } number && selectedBands.Any(b =>
                (b.Min is not { } min || number >= min) && (b.Max is not { } max || number < max));
        };

    /// <summary>Category matching walks the ACTIVE tree only: selecting "Mains" matches products
    /// in Mains and any active descendant. A deactivated category — or one under a deactivated
    /// ancestor — drops out of the closure, so its products stop matching (A17).</summary>
    private async Task<Func<BrowseRow, bool>> BuildCategoryPredicateAsync(
        Guid tenantId, List<string> selectedSlugs, CancellationToken cancellationToken)
    {
        var categories = await _dbContext.ProductCategories
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.IsActive)
            .Select(c => new { c.Id, c.Slug, c.ParentCategoryId })
            .ToListAsync(cancellationToken);

        var childrenByParent = categories
            .Where(c => c.ParentCategoryId is not null)
            .ToLookup(c => c.ParentCategoryId!.Value, c => c.Id);

        var closure = new HashSet<Guid>();
        var frontier = new Queue<Guid>(categories
            .Where(c => selectedSlugs.Contains(c.Slug, StringComparer.OrdinalIgnoreCase))
            .Select(c => c.Id));

        while (frontier.Count > 0)
        {
            var id = frontier.Dequeue();
            if (!closure.Add(id))
            {
                continue;
            }
            foreach (var child in childrenByParent[id])
            {
                frontier.Enqueue(child);
            }
        }

        return row => row.Product.CategoryId is { } categoryId && closure.Contains(categoryId);
    }

    [LoggerMessage(
        EventId = 7001,
        Level = LogLevel.Warning,
        Message = "Product {Slug} ({ProductId}) has malformed catalogue JSON; rendering with empty tags and excluding it from facet matching.")]
    private static partial void LogMalformedProductJson(ILogger logger, string slug, Guid productId);

    public async Task<AdminProductDetailDto?> GetAdminProductAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var product = await QueryWithGraph()
            .FirstOrDefaultAsync(p => p.Id == productId && p.TenantId == tenantId, cancellationToken);
        if (product is null)
        {
            return null;
        }

        return MapAdmin(product, await _options.GetEffectiveOptionsAsync(product.Id, cancellationToken));
    }

    public async Task<AdminProductDetailDto> UpdateProductAsync(
        Guid productId, UpdateProductCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var product = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == productId && p.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException($"Product '{productId}' was not found.");

        // Validate EVERYTHING before assigning ANYTHING — a rejected request must change nothing.
        string? status = null;
        if (command.Status is not null)
        {
            status = command.Status.Trim();
            if (status is not (ProductStatuses.Draft or ProductStatuses.Active or ProductStatuses.Archived))
            {
                throw new StorefrontValidationException(
                    $"'{command.Status}' is not a valid status; expected {ProductStatuses.Draft}, {ProductStatuses.Active} or {ProductStatuses.Archived}.");
            }
        }

        if (command.CategoryId is { } categoryId && !command.ClearCategory)
        {
            var categoryExists = await _dbContext.ProductCategories
                .AnyAsync(c => c.Id == categoryId && c.TenantId == tenantId, cancellationToken);
            if (!categoryExists)
            {
                throw new NotFoundException($"Category '{categoryId}' was not found.");
            }
        }

        var tags = command.TagsJson is not null ? ValidateStringArrayJson(command.TagsJson, "tagsJson") : null;
        var keywords = command.SearchKeywordsJson is not null
            ? ValidateStringArrayJson(command.SearchKeywordsJson, "searchKeywordsJson")
            : null;

        if (keywords is not null && keywords.Length > 1024)
        {
            // The column bound is 1024 (§7 — an authoring bound, not a search engine). Rejecting
            // here beats the DbUpdateException-turned-500 the database would otherwise produce.
            throw new StorefrontValidationException("searchKeywordsJson exceeds the 1024-character bound.");
        }

        var attributes = command.AttributesJson is not null
            ? ValidateObjectJson(command.AttributesJson, "attributesJson")
            : null;

        if (command.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(command.Name))
            {
                throw new StorefrontValidationException("A product name cannot be blank.");
            }
            product.Name = command.Name.Trim();
        }

        if (command.Description is not null)
        {
            product.Description = command.Description;
        }

        if (status is not null)
        {
            product.Status = status;
        }

        if (command.ClearCategory)
        {
            product.CategoryId = null;
        }
        else if (command.CategoryId is { } newCategoryId)
        {
            product.CategoryId = newCategoryId;
        }

        if (tags is not null)
        {
            product.TagsJson = tags;
        }

        if (attributes is not null)
        {
            product.AttributesJson = attributes;
        }

        if (keywords is not null)
        {
            product.SearchKeywordsJson = keywords;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return (await GetAdminProductAsync(productId, cancellationToken))!;
    }

    public async Task<IReadOnlyList<ProductMediaDto>> ReplaceProductMediaAsync(
        Guid productId, ReplaceProductMediaCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var product = await _dbContext.Products
            .Include(p => p.Media)
            .FirstOrDefaultAsync(p => p.Id == productId && p.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException($"Product '{productId}' was not found.");

        var lines = command.Items ?? throw new StorefrontValidationException(
            "An 'items' array is required. To remove all media, send an explicit empty array.");

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line.Url) || line.Url.Length > 2048)
            {
                throw new StorefrontValidationException("Every media item requires a URL of at most 2048 characters.");
            }
            if (line.Kind is not (null or "image" or "doc"))
            {
                throw new StorefrontValidationException($"'{line.Kind}' is not a valid media kind; expected image or doc.");
            }
        }

        _dbContext.ProductMedia.RemoveRange(product.Media);

        var replacements = lines
            .Select((line, index) => new ProductMedia
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProductId = productId,
                Url = line.Url.Trim(),
                Kind = line.Kind ?? "image",
                SortOrder = index,
            })
            .ToList();
        _dbContext.ProductMedia.AddRange(replacements);

        // Serialize concurrent full-replaces on the product row — two disjoint replaces share no
        // media row, so without this both would commit and the sets would merge.
        _dbContext.Entry(product).State = EntityState.Modified;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return replacements
            .OrderBy(m => m.SortOrder)
            .Select(m => new ProductMediaDto(m.Id, m.Url, m.Kind, m.SortOrder))
            .ToList();
    }

    /// <summary>Strict JSON hygiene for writes (§11): a JSON array of strings, returned in its
    /// canonical serialized form so equivalent submissions store identically.</summary>
    private static string ValidateStringArrayJson(string json, string field)
    {
        var values = StorefrontJson.ParseStringArray(json, out var malformed);
        if (malformed)
        {
            throw new StorefrontValidationException($"{field} must be a JSON array of strings.");
        }
        return JsonSerializer.Serialize(values);
    }

    private static string ValidateObjectJson(string json, string field)
    {
        var element = StorefrontJson.ParseObject(json, out var malformed);
        if (malformed || element is null)
        {
            throw new StorefrontValidationException($"{field} must be a JSON object.");
        }
        return json;
    }

    public async Task<ProductVariantDto> AddVariantAsync(AddVariantCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var productExists = await _dbContext.Products
            .AnyAsync(p => p.Id == command.ProductId && p.TenantId == tenantId, cancellationToken);
        if (!productExists)
        {
            throw new InvalidOperationException($"Product '{command.ProductId}' was not found.");
        }

        var variant = new ProductVariant
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = command.ProductId,
            Sku = command.Sku,
            Name = command.Name,
            OptionsJson = command.OptionsJson ?? "{}",
            WeightGrams = command.WeightGrams,
            IsActive = true,
        };
        _dbContext.ProductVariants.Add(variant);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapVariant(variant, Array.Empty<ProductPrice>());
    }

    public async Task<ProductCategoryDto> CreateCategoryAsync(CreateCategoryCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var category = new ProductCategory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Slug = command.Slug,
            Name = command.Name,
            ParentCategoryId = command.ParentCategoryId,
            SortOrder = command.SortOrder,
        };
        _dbContext.ProductCategories.Add(category);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ProductCategoryDto(category.Id, category.Slug, category.Name, category.ParentCategoryId, category.SortOrder);
    }

    public async Task<IReadOnlyList<CategoryTreeNodeDto>> GetCategoryTreeAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var categories = await _dbContext.ProductCategories
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.IsActive)
            .ToListAsync(cancellationToken);

        var childrenByParent = categories.ToLookup(c => c.ParentCategoryId);

        // Built by walking from the roots: a child whose parent is inactive (and therefore absent
        // from the active set) is never reached, so a deactivated node hides its whole subtree —
        // the same closure rule the category facet uses (A17).
        List<CategoryTreeNodeDto> BuildLevel(Guid? parentId) => childrenByParent[parentId]
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select(c => new CategoryTreeNodeDto(c.Id, c.Slug, c.Name, c.SortOrder, BuildLevel(c.Id)))
            .ToList();

        return BuildLevel(null);
    }

    public async Task<ProductCategoryDto> UpdateCategoryAsync(
        Guid categoryId, UpdateCategoryCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var category = await _dbContext.ProductCategories
            .FirstOrDefaultAsync(c => c.Id == categoryId && c.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException($"Category '{categoryId}' was not found.");

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            throw new StorefrontValidationException("A category name is required.");
        }
        category.Name = command.Name.Trim();

        // Re-parenting: ClearParent moves to the root (a nullable Guid alone cannot say "clear" —
        // null means unchanged). The new parent must exist here and must not create a cycle: a
        // category under its own descendant would orbit forever in every tree walk.
        if (command.ClearParent)
        {
            category.ParentCategoryId = null;
        }
        else if (command.ParentCategoryId is { } parentId)
        {
            if (parentId == categoryId)
            {
                throw new StorefrontValidationException("A category cannot be its own parent.");
            }

            var all = await _dbContext.ProductCategories
                .AsNoTracking()
                .Where(c => c.TenantId == tenantId)
                .Select(c => new { c.Id, c.ParentCategoryId })
                .ToListAsync(cancellationToken);
            var parentById = all.ToDictionary(c => c.Id, c => c.ParentCategoryId);

            if (!parentById.ContainsKey(parentId))
            {
                throw new NotFoundException($"Parent category '{parentId}' was not found.");
            }

            // Walk UP from the proposed parent; reaching this category means the proposed parent
            // is a descendant. Bounded by the node count so corrupt data cannot loop forever.
            var cursor = (Guid?)parentId;
            for (var hops = 0; cursor is { } current && hops <= all.Count; hops++)
            {
                if (current == categoryId)
                {
                    throw new StorefrontValidationException(
                        "That parent is a descendant of this category; the move would create a cycle.");
                }
                cursor = parentById.GetValueOrDefault(current);
            }

            category.ParentCategoryId = parentId;
        }

        if (command.SortOrder is { } sortOrder)
        {
            category.SortOrder = sortOrder;
        }

        if (command.IsActive is { } isActive)
        {
            category.IsActive = isActive;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ProductCategoryDto(category.Id, category.Slug, category.Name, category.ParentCategoryId, category.SortOrder);
    }

    public async Task<BundleSlotDto> AddBundleSlotAsync(AddBundleSlotCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var product = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == command.BundleProductId && p.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Bundle product '{command.BundleProductId}' was not found.");

        if (product.Kind != ProductKinds.Bundle)
        {
            throw new InvalidOperationException("Selection slots can only be added to a Bundle product.");
        }
        if (command.MinItems < 0 || command.MaxItems < command.MinItems || command.MaxItems == 0)
        {
            throw new ArgumentException("A bundle slot requires 0 <= MinItems <= MaxItems and MaxItems > 0.");
        }

        var slot = new BundleSlot
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BundleProductId = command.BundleProductId,
            Name = command.Name,
            MinItems = command.MinItems,
            MaxItems = command.MaxItems,
            FromCategoryId = command.FromCategoryId,
            AllowDuplicates = command.AllowDuplicates,
            SortOrder = command.SortOrder,
        };

        foreach (var opt in command.Options ?? Array.Empty<AddBundleSlotOptionLine>())
        {
            slot.Options.Add(new BundleSlotOption
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                BundleSlotId = slot.Id,
                ProductVariantId = opt.ProductVariantId,
                PriceDelta = opt.PriceDelta,
            });
        }

        _dbContext.BundleSlots.Add(slot);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapSlot(slot);
    }

    private IQueryable<Product> QueryWithGraph()
        => _dbContext.Products
            .AsNoTracking()
            .Include(p => p.Variants).ThenInclude(v => v.Prices)
            .Include(p => p.Media)
            .Include(p => p.BundleSlots).ThenInclude(s => s.Options);

    private AdminProductDetailDto MapAdmin(Product p, IReadOnlyList<EffectiveOptionGroupDto> effectiveOptions)
    {
        var baseline = Map(p, effectiveOptions);
        var keywords = StorefrontJson.ParseStringArray(p.SearchKeywordsJson, out var malformed);
        if (malformed)
        {
            LogMalformedProductJson(_logger, p.Slug, p.Id);
        }

        return new AdminProductDetailDto(
            baseline.Id, baseline.Slug, baseline.Name, baseline.Description, baseline.Status, baseline.Kind,
            baseline.CategoryId, baseline.TagsJson, baseline.AttributesJson,
            baseline.BundlePricingMode, baseline.BundleFixedAmount, baseline.BundlePremium, baseline.BundleCurrency,
            baseline.TargetMarginPct, baseline.Variants, baseline.Media, baseline.BundleSlots,
            baseline.EffectiveOptionGroups, baseline.UnitSurcharge, baseline.UnitSurchargeCurrency,
            keywords);
    }

    private static ProductDto Map(Product p, IReadOnlyList<EffectiveOptionGroupDto> effectiveOptions) => new(
        p.Id, p.Slug, p.Name, p.Description, p.Status, p.Kind, p.CategoryId, p.TagsJson, p.AttributesJson,
        p.BundlePricingMode, p.BundleFixedAmount, p.BundlePremium, p.BundleCurrency, p.TargetMarginPct,
        p.Variants.OrderBy(v => v.Name).Select(v => MapVariant(v, v.Prices)).ToList(),
        p.Media.OrderBy(m => m.SortOrder).Select(m => new ProductMediaDto(m.Id, m.Url, m.Kind, m.SortOrder)).ToList(),
        p.BundleSlots.OrderBy(s => s.SortOrder).Select(MapSlot).ToList(),
        effectiveOptions,
        p.UnitSurcharge,
        p.UnitSurchargeCurrency);

    private static ProductVariantDto MapVariant(ProductVariant v, IEnumerable<ProductPrice> prices) => new(
        v.Id, v.ProductId, v.Sku, v.Name, v.OptionsJson, v.WeightGrams, v.IsActive,
        prices.Select(pr => new ProductPriceDto(pr.Id, pr.ProductVariantId, pr.Currency, pr.Amount, pr.EffectiveFrom, pr.EffectiveTo, pr.IsActive)).ToList());

    private static BundleSlotDto MapSlot(BundleSlot s) => new(
        s.Id, s.Name, s.MinItems, s.MaxItems, s.FromCategoryId, s.AllowDuplicates, s.SortOrder,
        s.Options.Select(o => new BundleSlotOptionDto(o.Id, o.ProductVariantId, o.PriceDelta)).ToList());
}
