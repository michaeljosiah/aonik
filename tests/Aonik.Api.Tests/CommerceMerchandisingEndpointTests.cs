using System.Net;
using System.Net.Http.Json;

using Aonik.Commerce.Entities.Catalog;
using Aonik.Infrastructure.Persistence;
using Aonik.Platform.Entities.Identity;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Api.Tests;

/// <summary>
/// Spec 070 endpoints over the real DI container: the config document (A7/A14), the keyword
/// serialization contract walked over raw JSON (A5/A11), collections (A1), facet-driven browse
/// (A4), the category tree (A17), and the Operations-writes-storefront-config policy split (§9).
/// </summary>
public class CommerceMerchandisingEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public CommerceMerchandisingEndpointTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task StorefrontConfig_Should_ServeDefaults_Anonymously_WithTenantPartitionedCaching()
    {
        // A7 — never 404s; A14 — Vary: X-Tenant-Id so a shared cache cannot cross-serve tenants.
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId);

        var response = await AnonymousClient(tenantId).GetAsync("/commerce/config/storefront");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Vary.Should().Contain("X-Tenant-Id");

        var doc = await response.Content.ReadFromJsonAsync<ConfigResponse>();
        doc!.Currency.Should().Be("GBP");
        doc.RecommendedChoiceLabel.Should().Be("Recommended");
        doc.ResultsPageSize.Should().Be(8);
        doc.Box.Should().BeNull();
    }

    [Fact]
    public async Task StorefrontConfig_Should_BeWritableByOperations_WhoHoldNoPlatformSettingsPermission()
    {
        // §9's whole point: the platform settings surface would lock Operations out; the
        // Commerce endpoint must not. The Operations role has no Settings.Write.
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId);
        var admin = await AdminClient(tenantId);

        var put = await admin.PutAsJsonAsync("/commerce/admin/storefront-config", new
        {
            recommendedChoiceLabel = "Abby's choice",
            resultsPageSize = 12,
            deliveryListAmount = 10.0m,
        });

        put.StatusCode.Should().Be(HttpStatusCode.OK);

        var doc = await AnonymousClient(tenantId).GetFromJsonAsync<ConfigResponse>("/commerce/config/storefront");
        doc!.RecommendedChoiceLabel.Should().Be("Abby's choice");
        doc.ResultsPageSize.Should().Be(12);
        doc.Delivery!.ListAmount.Should().Be(10.0m);
    }

    [Fact]
    public async Task StorefrontConfig_Should_RejectInvalidWrites_With400()
    {
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId);
        var admin = await AdminClient(tenantId);

        var response = await admin.PutAsJsonAsync("/commerce/admin/storefront-config", new { resultsPageSize = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SearchKeywords_Should_MatchInSearch_AndAppearInNoPublicResponse()
    {
        // A5/A11 — THE serialization contract, asserted against the actual serialized JSON of
        // every public surface, not the DTO types: the customer who searches "owambe" finds the
        // dish and never sees the word; the admin editor sees it (or a full update would erase it).
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId);
        var productId = await SeedProductAsync(tenantId, "jollof", keywords: """["owambe"]""", tags: """["vegan"]""");
        await SeedCollectionAsync(tenantId, "featured", (productId, 1));
        var anonymous = AnonymousClient(tenantId);

        var search = await anonymous.GetStringAsync("/commerce/catalog/products?search=owambe");
        search.Should().Contain("jollof", "the hidden keyword must match");

        foreach (var url in new[]
        {
            "/commerce/catalog/products",
            "/commerce/catalog/products/jollof",
            "/commerce/catalog/collections",
            "/commerce/catalog/collections/featured",
        })
        {
            (await anonymous.GetStringAsync(url)).Should().NotContain("owambe", $"{url} must never serialize keywords");
        }

        var adminJson = await (await AdminClient(tenantId)).GetStringAsync($"/commerce/admin/products/{productId}");
        adminJson.Should().Contain("owambe").And.Contain("searchKeywords");
    }

    [Fact]
    public async Task BrowseRow_Should_CarryHeroImageAndTags_AndNoPriceOrKeywordFields()
    {
        // A6 — the enriched grid row, and what it deliberately does NOT carry.
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId);
        await SeedProductAsync(tenantId, "jollof", tags: """["vegan"]""", heroUrl: "https://cdn.example/hero.jpg");

        var raw = await AnonymousClient(tenantId).GetStringAsync("/commerce/catalog/products");

        raw.Should().Contain("https://cdn.example/hero.jpg").And.Contain("heroImageUrl").And.Contain("vegan");
        raw.Should().NotContain("\"prices\"", "no variant price belongs on a list row");
        raw.Should().NotContain("searchKeywords");
    }

    [Fact]
    public async Task Collections_Should_RoundTripThroughAdmin_AndServeRankOrderPublicly()
    {
        // A1 — curated in the back office, reordered without a release.
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId);
        var first = await SeedProductAsync(tenantId, "dish-a");
        var second = await SeedProductAsync(tenantId, "dish-b");
        var admin = await AdminClient(tenantId);

        var created = await admin.PostAsJsonAsync("/commerce/admin/collections", new
        {
            slug = "featured",
            title = "Featured",
            kind = "Featured",
        });
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        var collectionId = (await created.Content.ReadFromJsonAsync<AdminCollectionResponse>())!.Id;

        var items = await admin.PutAsJsonAsync($"/commerce/admin/collections/{collectionId}/items", new
        {
            items = new[] { new { productId = second, rank = 1 }, new { productId = first, rank = 2 } },
        });
        items.StatusCode.Should().Be(HttpStatusCode.OK);

        var pub = await AnonymousClient(tenantId)
            .GetFromJsonAsync<List<PublicCollectionResponse>>("/commerce/catalog/collections?kind=featured");
        pub.Should().ContainSingle().Which.Products.Select(p => p.Slug).Should().ContainInOrder("dish-b", "dish-a");

        // Duplicate ranks are loud (A12).
        var duplicate = await admin.PutAsJsonAsync($"/commerce/admin/collections/{collectionId}/items", new
        {
            items = new[] { new { productId = second, rank = 1 }, new { productId = first, rank = 1 } },
        });
        duplicate.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Facets_Should_BeAuthorable_AndImmediatelyFilterTheBrowse()
    {
        // A4 — a facet group added in admin is served publicly and filters browse with zero
        // frontend change.
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId);
        await SeedProductAsync(tenantId, "salad", tags: """["vegan"]""");
        await SeedProductAsync(tenantId, "suya", tags: """["spicy"]""");
        var admin = await AdminClient(tenantId);

        var created = await admin.PostAsJsonAsync("/commerce/admin/facet-groups", new
        {
            key = "dietary",
            label = "Dietary",
            matchKind = "Tag",
            optionsJson = """[{"value":"vegan","label":"Vegan"}]""",
        });
        created.StatusCode.Should().Be(HttpStatusCode.OK);

        var anonymous = AnonymousClient(tenantId);
        var facets = await anonymous.GetStringAsync("/commerce/catalog/facets");
        facets.Should().Contain("dietary").And.Contain("Vegan");

        var filtered = await anonymous.GetStringAsync("/commerce/catalog/products?facet.dietary=vegan");
        filtered.Should().Contain("salad").And.NotContain("suya");

        // Unknown values are loud, never silently unfiltered (§6).
        var unknown = await anonymous.GetAsync("/commerce/catalog/products?facet.dietary=carnivore");
        unknown.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CategoryTree_Should_HideDeactivatedSubtrees_FromThePublicRead()
    {
        // A17 — endpoint half: deactivate via admin PUT, the public tree hides it.
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId);
        var admin = await AdminClient(tenantId);
        var categoryId = await SeedCategoryAsync(tenantId, "mains", "Mains");

        (await AnonymousClient(tenantId).GetStringAsync("/commerce/catalog/categories")).Should().Contain("mains");

        var update = await admin.PutAsJsonAsync($"/commerce/admin/categories/{categoryId}", new
        {
            name = "Mains",
            isActive = false,
        });
        update.StatusCode.Should().Be(HttpStatusCode.OK);

        (await AnonymousClient(tenantId).GetStringAsync("/commerce/catalog/categories")).Should().NotContain("mains");

        // The retire/reactivate loop must close: the ADMIN read still lists the retired category
        // with its id and lifecycle state — without it the back office could never find the id
        // to reactivate (A17).
        var adminList = await admin.GetFromJsonAsync<List<AdminCategoryResponse>>("/commerce/admin/categories");
        var retired = adminList.Should().ContainSingle(c => c.Slug == "mains").Which;
        retired.IsActive.Should().BeFalse();
        retired.Id.Should().Be(categoryId);
    }

    [Fact]
    public async Task Browse_Should_Reject_RankSortWithoutACollection()
    {
        // A16 at the HTTP boundary.
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId);

        var response = await AnonymousClient(tenantId).GetAsync("/commerce/catalog/products?sort=rank");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AdminMerchandisingEndpoints_Should_RejectAnonymousCallers()
    {
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId);
        var anonymous = AnonymousClient(tenantId);

        var collection = await anonymous.PostAsJsonAsync("/commerce/admin/collections", new { slug = "x", title = "X" });
        var config = await anonymous.PutAsJsonAsync("/commerce/admin/storefront-config", new { resultsPageSize = 9 });

        collection.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
        config.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    // ─── Seeding ─────────────────────────────────────────────────────────────

    private HttpClient AnonymousClient(Guid tenantId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        return client;
    }

    private Task<HttpClient> AdminClient(Guid tenantId)
        => _factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create().WithRoles("Operations").WithTenant(tenantId));

    private async Task SeedTenantAsync(Guid tenantId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();

        if (await db.Tenants.AnyAsync(t => t.Id == tenantId)) return;

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Merchandising Test Tenant",
            Environment = "Testing",
            DefaultCurrency = "GBP",
            SupportedCountriesJson = "[]",
            Status = TenantStatus.Active,
        });
        await db.SaveChangesAsync();
    }

    private async Task<Guid> SeedProductAsync(
        Guid tenantId, string slug, string? tags = null, string? keywords = null, string? heroUrl = null)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;

        var productId = Guid.NewGuid();
        db.Products.Add(new Product
        {
            Id = productId,
            TenantId = tenantId,
            Slug = slug,
            Name = slug,
            Description = "A dish",
            Kind = ProductKinds.Simple,
            Status = ProductStatuses.Active,
            TagsJson = tags ?? "[]",
            SearchKeywordsJson = keywords ?? "[]",
        });

        if (heroUrl is not null)
        {
            db.ProductMedia.Add(new ProductMedia
            {
                Id = Guid.NewGuid(), TenantId = tenantId, ProductId = productId,
                Url = heroUrl, Kind = "image", SortOrder = 0,
            });
        }

        await db.SaveChangesAsync();
        return productId;
    }

    private async Task<Guid> SeedCategoryAsync(Guid tenantId, string slug, string name)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;

        var category = new ProductCategory
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Slug = slug, Name = name, SortOrder = 1, IsActive = true,
        };
        db.ProductCategories.Add(category);
        await db.SaveChangesAsync();
        return category.Id;
    }

    private async Task SeedCollectionAsync(Guid tenantId, string slug, params (Guid ProductId, int Rank)[] members)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;

        var collection = new Collection
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Slug = slug, Title = slug,
            Kind = CollectionKinds.Featured, SortOrder = 1, IsActive = true,
        };
        db.Collections.Add(collection);
        foreach (var (productId, rank) in members)
        {
            db.CollectionItems.Add(new CollectionItem
            {
                Id = Guid.NewGuid(), TenantId = tenantId, CollectionId = collection.Id,
                ProductId = productId, Rank = rank,
            });
        }
        await db.SaveChangesAsync();
    }

    // ─── Response shapes ─────────────────────────────────────────────────────

    private sealed record ConfigResponse(
        string Currency,
        string RecommendedChoiceLabel,
        int ResultsPageSize,
        DeliveryResponse? Delivery,
        string? DefaultBoxSlug,
        object? Box);

    private sealed record DeliveryResponse(decimal ListAmount, decimal ChargedAmount);

    private sealed record AdminCollectionResponse(Guid Id, string Slug);

    private sealed record AdminCategoryResponse(Guid Id, string Slug, string Name, bool IsActive);

    private sealed record PublicCollectionResponse(string Slug, string Title, List<MemberResponse> Products);

    private sealed record MemberResponse(string Slug);
}
