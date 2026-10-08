using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Contracts.Models.Fulfilment;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Services.Checkout;
using Aonik.Commerce.Services.Fulfilment;
using Aonik.Finance.Entities.Orders;
using Aonik.Infrastructure.Caching;
using Aonik.Infrastructure.Persistence;
using Aonik.Platform.Entities.Identity;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aonik.Api.Tests;

public sealed class CommerceDeliveryCoverageEndpointTests
    : IClassFixture<CommerceDeliveryCoverageEndpointTests.CoverageFactory>
{
    private const string PublicPath = "/commerce/delivery/coverage";
    private const string AdminPath = "/commerce/admin/delivery-coverage";
    private readonly CoverageFactory _factory;

    public CommerceDeliveryCoverageEndpointTests(CoverageFactory factory)
    {
        _factory = factory;
        // Shared factory disables hosted services, including the settings cache subscriber.
        _factory.Services.GetRequiredService<FusionCacheInvalidationHandler>();
    }

    [Fact]
    public async Task Coverage_Should_RequireTenantConfiguration_AndRefreshAfterAuthorizedEdits()
    {
        var tenantA = await SeedTenantAsync();
        var tenantB = await SeedTenantAsync();
        using var customer = Client(tenantA);
        using var otherCustomer = Client(tenantB);
        using var admin = await AdminClientAsync(tenantA, "Operations");
        var callsBefore = _factory.Lookups.Count;

        using var missing = await customer.GetAsync(PublicPath + "?postcode=SW1A1AA");
        missing.StatusCode.Should().Be(HttpStatusCode.OK);
        (await missing.Content.ReadFromJsonAsync<DeliveryCoverageDto>())!.Status.Should().Be(DeliveryCoverageStatuses.Unavailable);
        AssertNoStore(missing);
        _factory.Lookups.Count.Should().Be(callsBefore);
        using var missingConfiguration = await admin.GetAsync(AdminPath);
        missingConfiguration.StatusCode.Should().Be(HttpStatusCode.NotFound);
        AssertNoStore(missingConfiguration);

        using var write = await admin.PutAsJsonAsync(AdminPath,
            new DeliveryCoverageConfigDto(true, [" sw1a ", "SW1A"], Source: "Approved private courier reference"));
        write.StatusCode.Should().Be(HttpStatusCode.OK);
        (await write.Content.ReadFromJsonAsync<DeliveryCoverageConfigDto>())!.AllowedOutwardCodes.Should().Equal("SW1A");
        AssertNoStore(write);
        using var read = await admin.GetAsync(AdminPath);
        read.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertNoStore(read);

        using var served = await customer.GetAsync(PublicPath + "?postcode=sw1a1aa");
        served.StatusCode.Should().Be(HttpStatusCode.OK);
        (await served.Content.ReadFromJsonAsync<DeliveryCoverageDto>()).Should().Be(new DeliveryCoverageDto("serves", "SW1A 1AA"));
        (await served.Content.ReadAsStringAsync()).Should().NotContain("courier reference").And.NotContain("allowedOutwardCodes");
        AssertNoStore(served);
        (await otherCustomer.GetFromJsonAsync<DeliveryCoverageDto>(PublicPath + "?postcode=SW1A1AA"))!
            .Status.Should().Be(DeliveryCoverageStatuses.Unavailable);

        using var exclude = await admin.PutAsJsonAsync(AdminPath, new DeliveryCoverageConfigDto(true, ["SW1A"], ["SW1A"]));
        exclude.StatusCode.Should().Be(HttpStatusCode.OK);
        (await customer.GetFromJsonAsync<DeliveryCoverageDto>(PublicPath + "?postcode=SW1A1AA"))!
            .Status.Should().Be(DeliveryCoverageStatuses.NotServed);
        await AssertNoCommerceEffectsAsync(tenantA);
    }

    [Fact]
    public async Task Coverage_Should_WithholdDisabledConfiguration_WithoutProviderLookup()
    {
        var tenantId = await SeedTenantAsync();
        await ConfigureAsync(tenantId, new(false, ["SW1A"]));
        using var client = Client(tenantId);
        var before = _factory.Lookups.Count;

        using var response = await client.GetAsync(PublicPath + "?postcode=SW1A1AA");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<DeliveryCoverageDto>())!.Status.Should().Be(DeliveryCoverageStatuses.Unavailable);
        AssertNoStore(response);
        _factory.Lookups.Count.Should().Be(before);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?postcode=")]
    [InlineData("?postcode=not-a-postcode")]
    [InlineData("?postcode=SW1A1AA&postcode=W1A0AX")]
    public async Task Coverage_Should_RejectMissingMalformedOrRepeatedPostcode_BeforeLookup(string query)
    {
        var tenantId = await SeedTenantAsync();
        await ConfigureAsync(tenantId, new(true, ["SW1A"]));
        using var client = Client(tenantId);
        var before = _factory.Lookups.Count;

        using var response = await client.GetAsync(PublicPath + query);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        error.GetProperty("code").GetString().Should().Be(DeliveryCoverageException.InvalidPostcode);
        error.GetProperty("fieldName").GetString().Should().Be("postcode");
        AssertNoStore(response);
        _factory.Lookups.Count.Should().Be(before);
    }

    [Theory]
    [InlineData("EC1A1BB", HttpStatusCode.BadRequest)]
    [InlineData("W1A0AX", HttpStatusCode.OK)]
    public async Task Coverage_Should_DistinguishMissingPostcodeFromUnavailableProvider(string postcode, HttpStatusCode expected)
    {
        var tenantId = await SeedTenantAsync();
        await ConfigureAsync(tenantId, new(true, ["EC1A", "W1A"]));
        using var client = Client(tenantId);

        using var response = await client.GetAsync(PublicPath + "?postcode=" + postcode);

        response.StatusCode.Should().Be(expected);
        AssertNoStore(response);
        if (expected == HttpStatusCode.OK)
            (await response.Content.ReadFromJsonAsync<DeliveryCoverageDto>())!.Status.Should().Be(DeliveryCoverageStatuses.Unavailable);
        else
            (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()
                .Should().Be(DeliveryCoverageException.InvalidPostcode);
    }

    [Theory]
    [InlineData("Operations", true, true)]
    [InlineData("TenantAdmin", true, true)]
    [InlineData("PlatformAdmin", true, true)]
    [InlineData("ReadOnly", true, false)]
    [InlineData("PersonalUser", false, false)]
    public async Task Configuration_Should_ApplyExistingStaffReadAndWritePolicies(string role, bool canRead, bool canWrite)
    {
        var tenantId = await SeedTenantAsync();
        await ConfigureAsync(tenantId, new(true, ["SW1A"]));
        using var client = await AdminClientAsync(tenantId, role);

        using var read = await client.GetAsync(AdminPath);
        using var write = await client.PutAsJsonAsync(AdminPath, new DeliveryCoverageConfigDto(true, []));

        read.StatusCode.Should().Be(canRead ? HttpStatusCode.OK : HttpStatusCode.Forbidden);
        write.StatusCode.Should().Be(canWrite ? HttpStatusCode.OK : HttpStatusCode.Forbidden);
        AssertNoStore(read);
        AssertNoStore(write);
    }

    [Fact]
    public async Task Configuration_Should_RejectAnonymousAndMalformedWrites_WithoutReplacingSavedRules()
    {
        var tenantId = await SeedTenantAsync();
        await ConfigureAsync(tenantId, new(true, ["SW1A"]));
        using var anonymous = Client(tenantId);
        using var denied = await anonymous.PutAsJsonAsync(AdminPath, new DeliveryCoverageConfigDto(true, []));
        denied.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
        AssertNoStore(denied);
        using var admin = await AdminClientAsync(tenantId, "Operations");
        foreach (var json in new[]
                 {
                     "{\"allowedOutwardCodes\":[\"SW1A\"]}",
                     "{\"isEnabled\":true}",
                     "{\"isEnabled\":true,\"allowedOutwardCodes\":null}",
                     "{\"isEnabled\":true,\"allowedOutwardCodes\":[\"SW1A\"],\"excludedOutwardCode\":[\"SW1A\"]}",
                     "{broken"
                 })
        {
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var rejected = await admin.PutAsync(AdminPath, content);
            rejected.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);
            AssertNoStore(rejected);
        }
        (await admin.GetFromJsonAsync<DeliveryCoverageConfigDto>(AdminPath))!.AllowedOutwardCodes.Should().Equal("SW1A");
    }

    [Fact]
    public async Task Coverage_Should_PreserveNoStoreOnTenantErrors_WithoutLookup()
    {
        using var missingTenant = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new("https://localhost") });
        using var unknownTenant = Client(Guid.NewGuid());
        using var inactiveTenant = Client(await SeedTenantAsync("Suspended"));
        var before = _factory.Lookups.Count;

        using var missing = await missingTenant.GetAsync(PublicPath + "?postcode=SW1A1AA");
        using var unknown = await unknownTenant.GetAsync(PublicPath + "?postcode=SW1A1AA");
        using var inactive = await inactiveTenant.GetAsync(PublicPath + "?postcode=SW1A1AA");

        missing.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        inactive.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        AssertNoStore(missing);
        AssertNoStore(unknown);
        AssertNoStore(inactive);
        _factory.Lookups.Count.Should().Be(before);
    }

    [Fact]
    public async Task RateLimit_Should_PartitionByResolvedTenantAndClientIp_AndExposeRetryAfter()
    {
        var firstTenant = await SeedTenantAsync();
        var otherTenant = await SeedTenantAsync();
        await ConfigureAsync(firstTenant, new(true, ["SW1A"]));
        await ConfigureAsync(otherTenant, new(true, ["SW1A"]));
        for (var request = 0; request < CoverageFactory.Permits; request++)
            (await CheckFromIpAsync(firstTenant, "198.51.100.10")).Response.StatusCode.Should().Be(200);
        var before = _factory.Lookups.Count;

        var limited = await CheckFromIpAsync(firstTenant, "::ffff:198.51.100.10", origin: "http://localhost:5173");

        limited.Response.StatusCode.Should().Be(429);
        limited.Response.Headers.CacheControl.ToString().Should().Be("no-store");
        int.Parse(limited.Response.Headers.RetryAfter.ToString()).Should().BeGreaterThanOrEqualTo(1);
        limited.Response.Headers.AccessControlAllowOrigin.ToString().Should().Be("http://localhost:5173");
        limited.Response.Headers.AccessControlExposeHeaders.ToString().Should().Contain("Retry-After");
        _factory.Lookups.Count.Should().Be(before);
        (await CheckFromIpAsync(firstTenant, "198.51.100.11")).Response.StatusCode.Should().Be(200);
        (await CheckFromIpAsync(otherTenant, "198.51.100.10")).Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task RateLimit_Should_IgnoreSpoofedForwarding_ButUseTheTrustedProxyResult()
    {
        var tenantId = await SeedTenantAsync();
        await ConfigureAsync(tenantId, new(true, ["SW1A"]));
        for (var request = 0; request < CoverageFactory.Permits; request++)
            (await CheckFromIpAsync(tenantId, "198.51.100.20", forwarded: $"203.0.113.{request + 1}"))
                .Response.StatusCode.Should().Be(200);

        (await CheckFromIpAsync(tenantId, "198.51.100.20", forwarded: "203.0.113.99"))
            .Response.StatusCode.Should().Be(429);
        for (var request = 0; request < CoverageFactory.Permits; request++)
            (await CheckFromIpAsync(tenantId, "127.0.0.1", forwarded: "203.0.113.50"))
                .Response.StatusCode.Should().Be(200);
        (await CheckFromIpAsync(tenantId, "127.0.0.1", forwarded: "203.0.113.50"))
            .Response.StatusCode.Should().Be(429);
        (await CheckFromIpAsync(tenantId, "127.0.0.1", forwarded: "203.0.113.51"))
            .Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task Checkout_Should_ShareThePublicLookupBudget_AndNotRepeatUpstreamWorkAfterRejection()
    {
        var tenantId = await SeedTenantAsync();
        await ConfigureAsync(tenantId, new(true, ["W1A"]));
        var cart = await SeedBoxCartAsync(tenantId);
        using var client = Client(tenantId);
        client.DefaultRequestHeaders.Add("X-Cart-Token", cart.Token);
        client.DefaultRequestHeaders.Add("X-Cart-Version", cart.Version);
        var before = _factory.Lookups.Count;
        using var first = await client.GetAsync(PublicPath + "?postcode=W1A0AX");
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        for (var request = 1; request < CoverageFactory.Permits; request++)
        {
            using var unavailable = await client.PostAsJsonAsync(CheckoutPath(cart), CheckoutBody("W1A 0AX"));
            unavailable.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            AssertNoStore(unavailable);
        }

        using var rejected = await client.PostAsJsonAsync(CheckoutPath(cart), CheckoutBody("W1A 0AX"));

        rejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        rejected.Headers.RetryAfter!.Delta.Should().BeGreaterThan(TimeSpan.Zero);
        AssertNoStore(rejected);
        _factory.Lookups.Count.Should().Be(before + CoverageFactory.Permits);
        await AssertNoCommerceEffectsAsync(tenantId, cart);
    }

    [Theory]
    [InlineData("missing-config", HttpStatusCode.ServiceUnavailable, DeliveryCoverageException.Unavailable)]
    [InlineData("not-served", HttpStatusCode.BadRequest, DeliveryCoverageException.NotServed)]
    [InlineData("wrong-country", HttpStatusCode.BadRequest, DeliveryCoverageException.UnsupportedCountry)]
    public async Task Checkout_Should_BlockUnverifiedBoxDelivery_BeforeAnyCheckoutEffects(
        string failure, HttpStatusCode status, string code)
    {
        var tenantId = await SeedTenantAsync();
        if (failure != "missing-config") await ConfigureAsync(tenantId, new(true, ["W1A"]));
        var cart = await SeedBoxCartAsync(tenantId);
        using var client = Client(tenantId);
        client.DefaultRequestHeaders.Add("X-Cart-Token", cart.Token);
        client.DefaultRequestHeaders.Add("X-Cart-Version", cart.Version);

        using var response = await client.PostAsJsonAsync(CheckoutPath(cart),
            CheckoutBody("SW1A 1AA", failure == "wrong-country" ? "FR" : "GB"));

        response.StatusCode.Should().Be(status);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be(code);
        AssertNoStore(response);
        await AssertNoCommerceEffectsAsync(tenantId, cart);
    }

    private HttpClient Client(Guid tenantId)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new("https://localhost") });
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        return client;
    }

    private Task<HttpClient> AdminClientAsync(Guid tenantId, string role)
        => _factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithTenant(tenantId).WithRoles(role));

    private Task<HttpContext> CheckFromIpAsync(Guid tenantId, string remote, string? forwarded = null, string? origin = null)
        => _factory.Server.SendAsync(context =>
        {
            context.Request.Method = "GET";
            context.Request.Scheme = "https";
            context.Request.Host = new HostString("localhost");
            context.Request.Path = PublicPath;
            context.Request.QueryString = new QueryString("?postcode=SW1A1AA");
            context.Connection.RemoteIpAddress = IPAddress.Parse(remote);
            context.Request.Headers["X-Tenant-Id"] = tenantId.ToString();
            if (forwarded is not null) context.Request.Headers["X-Forwarded-For"] = forwarded;
            if (origin is not null) context.Request.Headers.Origin = origin;
        });

    private async Task<Guid> SeedTenantAsync(string status = TenantStatus.Active)
    {
        var tenantId = Guid.NewGuid();
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId, Name = "Delivery coverage test", Environment = "Testing", Status = status,
            DefaultCurrency = "GBP", SupportedCountriesJson = "[]"
        });
        await db.SaveChangesAsync();
        return tenantId;
    }

    private async Task ConfigureAsync(Guid tenantId, DeliveryCoverageConfigDto configuration)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;
        await scope.ServiceProvider.GetRequiredService<IDeliveryCoverageService>().UpdateConfigurationAsync(configuration);
    }

    private async Task<SeededCart> SeedBoxCartAsync(Guid tenantId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        var cart = new Cart
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AnonymousToken = CartAccess.MintToken(), Currency = "GBP",
            BoxBundleProductId = Guid.NewGuid(),
            Items = [new CartItem { TenantId = tenantId, ProductVariantId = Guid.NewGuid(), Quantity = 1, UnitPriceSnapshot = 10 }]
        };
        // Delivery coverage is checked before catalogue/drift/calendar/stock work, so this fixture
        // needs only a persisted owned cart and line, not a synthetic sellable box world.
        cart.Items[0].CartId = cart.Id;
        db.Carts.Add(cart);
        await db.SaveChangesAsync();
        return new(cart.Id, cart.AnonymousToken!, Convert.ToBase64String(cart.RowVersion));
    }

    private async Task AssertNoCommerceEffectsAsync(Guid tenantId, SeededCart? cart = null)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        (await db.InventoryReservations.CountAsync(row => row.TenantId == tenantId)).Should().Be(0);
        (await db.OrderChargeSummaries.CountAsync(row => row.TenantId == tenantId)).Should().Be(0);
        (await db.OrderDeliveryDetails.CountAsync(row => row.TenantId == tenantId)).Should().Be(0);
        (await db.Set<Order>().CountAsync(row => row.TenantId == tenantId)).Should().Be(0);
        if (cart is null) (await db.Carts.CountAsync(row => row.TenantId == tenantId)).Should().Be(0);
        else
        {
            var stored = await db.Carts.SingleAsync(row => row.Id == cart.Id);
            stored.OrderId.Should().BeNull();
            stored.Status.Should().Be(CartStatuses.Open);
            Convert.ToBase64String(stored.RowVersion).Should().Be(cart.Version);
        }
    }

    private static object CheckoutBody(string postcode, string country = "GB") => new
    {
        provider = "Test", paymentMethodType = "Card",
        delivery = new CheckoutDeliveryDetails(
            new("buyer@example.test", "Pat", "Customer", "07000000000"),
            new("1 Test Street", null, "London", null, postcode, country), new DateOnly(2026, 11, 5))
    };

    private static string CheckoutPath(SeededCart cart) => $"/commerce/carts/{cart.Id}/checkout";
    private static void AssertNoStore(HttpResponseMessage response) => response.Headers.CacheControl!.NoStore.Should().BeTrue();
    private sealed record SeededCart(Guid Id, string Token, string Version);

    public sealed class CoverageFactory : CustomWebApplicationFactory
    {
        public const int Permits = 3;
        public ConcurrentQueue<string> Lookups { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Commerce:DeliveryCoverage:RequestsPerMinute"] = Permits.ToString() }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPostcodeLookup>();
                services.AddSingleton<IPostcodeLookup>(new TestPostcodeLookup(Lookups));
            });
        }
    }

    private sealed class TestPostcodeLookup(ConcurrentQueue<string> calls) : IPostcodeLookup
    {
        public Task<PostcodeLookupResult> LookupAsync(string normalisedPostcode, CancellationToken cancellationToken = default)
        {
            calls.Enqueue(normalisedPostcode);
            return Task.FromResult(normalisedPostcode switch
            {
                "EC1A 1BB" => new PostcodeLookupResult(PostcodeLookupStatus.NotFound),
                "W1A 0AX" => new PostcodeLookupResult(PostcodeLookupStatus.Unavailable),
                _ => new PostcodeLookupResult(PostcodeLookupStatus.Found, normalisedPostcode)
            });
        }
    }
}
