using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Aonik.Infrastructure.Caching;
using Aonik.Infrastructure.Persistence;
using Aonik.Platform.Contracts.Models.Settings;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Entities.Settings;
using Aonik.Platform.Settings;
using Aonik.SharedKernel.Abstractions.Settings;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Api.Tests;

public class PublicBusinessProfileEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Endpoint = "/v1/business-profile";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly CustomWebApplicationFactory _factory;

    public PublicBusinessProfileEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        // The test factory strips hosted services; restore production's cache invalidation subscription.
        _factory.Services.GetRequiredService<FusionCacheInvalidationHandler>();
    }

    [Fact]
    public async Task Get_Should_ReturnOnlyThePublicAllowlist_WithoutTenantAdministrativeFallbacks()
    {
        var tenantId = await SeedTenantAsync();
        await SeedSettingAsync(tenantId, """
            {"isPublished":true,"internalNote":"wrapper-secret","profile":{
              "displayName":"Public kitchen","tenantId":"private-id","contactEmail":"private-contact",
              "contact":{"phone":"+442012345678","password":"nested-secret"},
              "legal":{"companyName":"Kitchen Ltd","bankAccount":"private-bank"}}}
            """);

        var response = await AnonymousClient(tenantId).GetAsync(Endpoint);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("displayName").GetString().Should().Be("Public kitchen");
        body.GetProperty("contact").GetProperty("email").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("website").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("logoUrl").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("openingHours").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetRawText().Should().NotContain("private").And.NotContain("secret").And.NotContain("isPublished");
        response.Headers.Vary.Should().Contain("X-Tenant-Id");
        response.Headers.CacheControl!.Public.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{broken")]
    [InlineData("{\"isPublished\":false,\"profile\":{\"displayName\":\"Draft\"}}")]
    [InlineData("{\"isPublished\":true,\"profile\":{\"displayName\":\"Kitchen\",\"website\":\"javascript:alert(1)\"}}")]
    [InlineData("{\"isPublished\":true,\"profile\":{\"displayName\":\"Kitchen\",\"openingHours\":{\"timezone\":\"Europe/London\",\"weeklyHours\":[{\"dayOfWeek\":1,\"closesAt\":\"17:00:00\"}],\"bankHolidays\":[],\"exceptionalClosures\":[]}}}")]
    public async Task Get_Should_ReturnUncacheable404_WhenNotExplicitlyPublishedAndValid(string? document)
    {
        var tenantId = await SeedTenantAsync();
        if (document is not null) await SeedSettingAsync(tenantId, document);

        var response = await AnonymousClient(tenantId).GetAsync(Endpoint);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        response.Headers.Vary.Should().Contain("X-Tenant-Id");
    }

    [Fact]
    public async Task Get_Should_IsolateTenantProfiles_AndNeverUseAGlobalSettingFallback()
    {
        var firstTenant = await SeedTenantAsync();
        var secondTenant = await SeedTenantAsync();
        var unpublishedTenant = await SeedTenantAsync();
        await SeedSettingAsync(firstTenant, Document("First kitchen"));
        await SeedSettingAsync(secondTenant, Document("Second kitchen"));
        await SeedSettingAsync(null, Document("Global private profile"));

        var first = await AnonymousClient(firstTenant).GetFromJsonAsync<PublicBusinessProfileDto>(Endpoint);
        var second = await AnonymousClient(secondTenant).GetFromJsonAsync<PublicBusinessProfileDto>(Endpoint);
        var unpublished = await AnonymousClient(unpublishedTenant).GetAsync(Endpoint);

        first!.DisplayName.Should().Be("First kitchen");
        second!.DisplayName.Should().Be("Second kitchen");
        unpublished.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_Should_NotShareCacheEntries_WhenTenantComesFromAuthentication()
    {
        var tenantId = await SeedTenantAsync();
        await SeedSettingAsync(tenantId, Document("Authenticated kitchen"));
        var client = await _factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithTenant(tenantId)
            .WithRoles("PersonalUser"));

        var headerless = await client.GetAsync(Endpoint);
        headerless.StatusCode.Should().Be(HttpStatusCode.OK);
        headerless.Headers.CacheControl!.NoStore.Should().BeTrue();
        headerless.Headers.Vary.Should().Contain("X-Tenant-Id");

        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        var withHeader = await client.GetAsync(Endpoint);
        withHeader.StatusCode.Should().Be(HttpStatusCode.OK);
        withHeader.Headers.CacheControl!.Public.Should().BeTrue();

        foreach (var headerValues in new[]
        {
            new[] { "invalid-tenant" },
            new[] { Guid.NewGuid().ToString() },
            new[] { tenantId.ToString(), tenantId.ToString() },
        })
        {
            client.DefaultRequestHeaders.Remove("X-Tenant-Id");
            client.DefaultRequestHeaders.Add("X-Tenant-Id", headerValues);
            var response = await client.GetAsync(Endpoint);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Headers.CacheControl!.NoStore.Should().BeTrue();
            (await response.Content.ReadFromJsonAsync<PublicBusinessProfileDto>())!.DisplayName
                .Should().Be("Authenticated kitchen", "the resolved authentication tenant remains authoritative");
        }
    }

    [Fact]
    public async Task ExistingTenantSettingsWrite_Should_PublishUpdateAndWithdrawThePublicProfile()
    {
        var tenantId = await SeedTenantAsync();
        var admin = await _factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithTenant(tenantId)
            .WithRoles("TenantAdmin").WithPermissions("Settings.Write"));
        var anonymous = AnonymousClient(tenantId);

        (await anonymous.GetAsync(Endpoint)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        foreach (var name in new[] { "Initial kitchen", "Updated kitchen" })
        {
            var write = await admin.PutAsJsonAsync($"/tenant/settings/values/{BusinessProfileSettingNames.Profile}",
                new { key = BusinessProfileSettingNames.Profile, value = Document(name) });
            write.StatusCode.Should().Be(HttpStatusCode.OK);
            (await anonymous.GetFromJsonAsync<PublicBusinessProfileDto>(Endpoint))!.DisplayName.Should().Be(name);
        }

        var withdraw = await admin.PutAsJsonAsync($"/tenant/settings/values/{BusinessProfileSettingNames.Profile}",
            new { key = BusinessProfileSettingNames.Profile, value = Document("Updated kitchen", isPublished: false) });
        withdraw.StatusCode.Should().Be(HttpStatusCode.OK);
        (await anonymous.GetAsync(Endpoint)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PublicSettingsList_Should_NotExposeTheRawProfileOrItsDraftData()
    {
        var tenantId = await SeedTenantAsync();
        await SeedSettingAsync(tenantId, Document("Private draft kitchen", isPublished: false));

        var response = await AnonymousClient(tenantId).GetAsync($"/v1/settings/public?tenantId={tenantId}");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotContain(BusinessProfileSettingNames.Profile).And.NotContain("Private draft kitchen");
    }

    [Fact]
    public async Task PublicProfile_Should_NotGrantAnonymousOrOrdinaryUsersSettingsWriteAccess()
    {
        var tenantId = await SeedTenantAsync();
        var member = await _factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithTenant(tenantId)
            .WithRoles("PersonalUser"));
        foreach (var client in new[] { AnonymousClient(tenantId), member })
        {
            var response = await client.PutAsJsonAsync($"/tenant/settings/values/{BusinessProfileSettingNames.Profile}",
                new { key = BusinessProfileSettingNames.Profile, value = Document("Unauthorized kitchen") });
            response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
        }
    }

    private HttpClient AnonymousClient(Guid tenantId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        return client;
    }

    private async Task<Guid> SeedTenantAsync()
    {
        var tenantId = Guid.NewGuid();
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId, Name = $"private-admin-name-{tenantId}", Environment = "Testing",
            DefaultCurrency = "GBP", SupportedCountriesJson = "[]", Status = TenantStatus.Active,
            ContactEmail = "private-owner@example.test", ContactMobile = "+440000000000",
            Website = "https://private-admin.example.test", LogoUrl = "https://private-admin.example.test/logo.png",
            AddressLine1 = "Private owner's home", PostalCode = "PRIVATE",
        });
        await db.SaveChangesAsync();
        return tenantId;
    }

    private async Task SeedSettingAsync(Guid? tenantId, string value)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        db.Settings.Add(new Setting
        {
            Id = Guid.NewGuid(), Key = BusinessProfileSettingNames.Profile, Value = value,
            Scope = tenantId.HasValue ? SettingScope.Tenant : SettingScope.Global, TenantId = tenantId,
        });
        await db.SaveChangesAsync();
    }

    private static string Document(string name, bool isPublished = true)
        => JsonSerializer.Serialize(new PublicBusinessProfileDocument(isPublished, new(name)), JsonOptions);
}
