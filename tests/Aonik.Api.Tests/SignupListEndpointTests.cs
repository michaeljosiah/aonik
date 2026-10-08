using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Aonik.Infrastructure.Caching;
using Aonik.Infrastructure.Persistence;
using Aonik.Platform.Contracts.Models.SignupLists;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Entities.Settings;
using Aonik.Platform.Settings;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Settings;

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Api.Tests;

public class SignupListEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string PublicRoute = "/v1/signup-lists";
    private const string AdminRoute = "/admin/signup-lists";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly CustomWebApplicationFactory _factory;

    public SignupListEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        // The test factory strips hosted services; restore the settings cache invalidation subscription.
        _factory.Services.GetRequiredService<FusionCacheInvalidationHandler>();
    }

    [Fact]
    public async Task Configuration_Should_UsePublishedTenantSettings_AndStayUncacheable()
    {
        var tenantId = await SeedTenantAsync(configured: false);
        var anonymous = AnonymousClient(tenantId);
        (await anonymous.GetFromJsonAsync<SignupListsConfigurationDto>(PublicRoute))!.Lists.Should().BeEmpty();
        var admin = await _factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithTenant(tenantId)
            .WithRoles("TenantAdmin").WithPermissions("Settings.Write"));

        var write = await admin.PutAsJsonAsync($"/tenant/settings/values/{SignupListSettingNames.Configuration}",
            new { key = SignupListSettingNames.Configuration, value = Configuration() });
        var response = await anonymous.GetAsync(PublicRoute);
        var configuration = await response.Content.ReadFromJsonAsync<SignupListsConfigurationDto>();

        write.StatusCode.Should().Be(HttpStatusCode.OK);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        configuration!.Lists.Select(x => x.ListType).Should().BeEquivalentTo(
            SignupListTypes.Newsletter, SignupListTypes.DeliveryAvailability, SignupListTypes.PrivateTable);
        configuration.Lists.Single(x => x.ListType == SignupListTypes.PrivateTable).Services!
            .Should().ContainSingle().Which.Id.Should().Be("recipe-development");

        var otherTenant = await SeedTenantAsync(configured: false);
        (await AnonymousClient(otherTenant).GetFromJsonAsync<SignupListsConfigurationDto>(PublicRoute))!
            .Lists.Should().BeEmpty();
    }

    [Theory]
    [InlineData(SignupListTypes.Newsletter)]
    [InlineData(SignupListTypes.DeliveryAvailability)]
    [InlineData(SignupListTypes.PrivateTable)]
    public async Task Capture_Should_ReturnTheSameEmpty202_ForNewAndRepeatedEmail(string listType)
    {
        var tenantId = await SeedTenantAsync();
        var anonymous = AnonymousClient(tenantId);
        var request = Capture(listType);

        foreach (var capture in new[] { request, request with { Email = "  GUEST@example.test  " } })
        {
            var response = await anonymous.PostAsJsonAsync($"{PublicRoute}/{listType}", capture);
            response.StatusCode.Should().Be(HttpStatusCode.Accepted);
            (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
            response.Headers.Location.Should().BeNull();
            AssertPrivate(response);
        }

        var page = await (await AdminClient(tenantId)).GetFromJsonAsync<PagedResult<SignupSubscriptionDto>>(
            $"{AdminRoute}/{listType}");
        var subscription = page!.Items.Should().ContainSingle().Which;
        subscription.Email.Should().Be("guest@example.test");
        subscription.ConsentVersion.Should().Be("v1");
        subscription.ConsentText.Should().Be($"Consent for {listType}");
        subscription.ConsentSource.Should().Be($"{listType}-form");
        subscription.UnsubscribeToken.Should().NotBeNullOrWhiteSpace();
        if (listType == SignupListTypes.DeliveryAvailability)
        {
            subscription.Postcode.Should().Be("SW1A 1AA");
            subscription.PostcodeOutwardCode.Should().Be("SW1A");
        }
        if (listType == SignupListTypes.PrivateTable)
        {
            subscription.Name.Should().Be("Guest Cook");
            subscription.Country.Should().Be("GB");
            subscription.Service.Should().Be("recipe-development");
        }
    }

    [Theory]
    [InlineData(SignupListTypes.Newsletter, "not-an-email", "v1", null, null)]
    [InlineData(SignupListTypes.Newsletter, "guest@example.test", "old", null, null)]
    [InlineData(SignupListTypes.DeliveryAvailability, "guest@example.test", "v1", "SW1A", null)]
    [InlineData(SignupListTypes.PrivateTable, "guest@example.test", "v1", null, "unknown-service")]
    [InlineData("unknown-list", "guest@example.test", "v1", null, null)]
    public async Task Capture_Should_RejectInvalidInputAndUnavailableConsent_WithPrivateHeaders(
        string listType, string email, string version, string? postcode, string? service)
    {
        var tenantId = await SeedTenantAsync();
        var request = Capture(listType) with { Email = email, ConsentVersion = version, Postcode = postcode, Service = service };

        var response = await AnonymousClient(tenantId).PostAsJsonAsync($"{PublicRoute}/{listType}", request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        AssertPrivate(response);
    }

    [Fact]
    public async Task Capture_Should_BindListTypeOnlyFromTheRoute()
    {
        var tenantId = await SeedTenantAsync();
        var response = await AnonymousClient(tenantId).PostAsJsonAsync(
            $"{PublicRoute}/newsletter?listType=private-table",
            new { email = "guest@example.test", consentVersion = "v1", listType = "private-table" });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var admin = await AdminClient(tenantId);
        (await admin.GetFromJsonAsync<PagedResult<SignupSubscriptionDto>>($"{AdminRoute}/newsletter?listType=private-table"))!
            .Items.Should().ContainSingle();
        (await admin.GetFromJsonAsync<PagedResult<SignupSubscriptionDto>>($"{AdminRoute}/private-table"))!
            .Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Unsubscribe_Should_BeReplaySafe_AndRepeatedCaptureMustNotReverseWithdrawal()
    {
        var tenantId = await SeedTenantAsync();
        var anonymous = AnonymousClient(tenantId);
        await anonymous.PostAsJsonAsync($"{PublicRoute}/newsletter", Capture(SignupListTypes.Newsletter));
        var admin = await AdminClient(tenantId);
        var page = await admin.GetFromJsonAsync<PagedResult<SignupSubscriptionDto>>($"{AdminRoute}/newsletter");
        var subscription = page!.Items.Single();
        var route = $"{PublicRoute}/newsletter/{subscription.Id}/unsubscribe";

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var response = await anonymous.PostAsJsonAsync(route,
                new { token = subscription.UnsubscribeToken, listType = "private-table", subscriptionId = Guid.NewGuid() });
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
            AssertPrivate(response);
        }
        (await anonymous.PostAsJsonAsync($"{PublicRoute}/newsletter", Capture(SignupListTypes.Newsletter)))
            .StatusCode.Should().Be(HttpStatusCode.Accepted);

        (await admin.GetFromJsonAsync<PagedResult<SignupSubscriptionDto>>($"{AdminRoute}/newsletter"))!
            .Items.Should().BeEmpty();
        var withdrawn = (await admin.GetFromJsonAsync<PagedResult<SignupSubscriptionDto>>(
            $"{AdminRoute}/newsletter?includeUnsubscribed=true"))!.Items.Should().ContainSingle().Which;
        withdrawn.UnsubscribedAtUtc.Should().NotBeNull();
        withdrawn.UnsubscribeToken.Should().BeNull();
    }

    [Fact]
    public async Task Unsubscribe_Should_RequireABodyTokenScopedToTenantListAndSubscription()
    {
        var tenantId = await SeedTenantAsync();
        var otherTenantId = await SeedTenantAsync();
        var anonymous = AnonymousClient(tenantId);
        await anonymous.PostAsJsonAsync($"{PublicRoute}/newsletter", Capture(SignupListTypes.Newsletter));
        var admin = await AdminClient(tenantId);
        var subscription = (await admin.GetFromJsonAsync<PagedResult<SignupSubscriptionDto>>($"{AdminRoute}/newsletter"))!.Items.Single();
        var route = $"{PublicRoute}/newsletter/{subscription.Id}/unsubscribe";

        var invalidRequests = new[]
        {
            (Client: anonymous, Route: route, Token: (string?)null),
            (Client: anonymous, Route: route, Token: "invalid-token"),
            (Client: anonymous, Route: route + $"?token={subscription.UnsubscribeToken}", Token: (string?)null),
            (Client: anonymous, Route: $"{PublicRoute}/private-table/{subscription.Id}/unsubscribe", Token: subscription.UnsubscribeToken),
            (Client: anonymous, Route: $"{PublicRoute}/newsletter/{Guid.NewGuid()}/unsubscribe?subscriptionId={subscription.Id}", Token: subscription.UnsubscribeToken),
            (Client: AnonymousClient(otherTenantId), Route: route, Token: subscription.UnsubscribeToken),
        };
        foreach (var invalid in invalidRequests)
        {
            var response = await invalid.Client.PostAsJsonAsync(invalid.Route,
                new { token = invalid.Token, listType = "newsletter", subscriptionId = subscription.Id });
            response.StatusCode.Should().Be(invalid.Token is null ? HttpStatusCode.UnprocessableEntity : HttpStatusCode.NotFound);
            AssertPrivate(response);
        }

        var get = await anonymous.GetAsync(route + $"?token={subscription.UnsubscribeToken}");
        get.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
        (await admin.GetFromJsonAsync<PagedResult<SignupSubscriptionDto>>($"{AdminRoute}/newsletter"))!
            .Items.Should().ContainSingle();
    }

    [Fact]
    public async Task AdminListsAndAreas_Should_RequireBothAdminPolicyAndCustomerReadPermission()
    {
        var tenantId = await SeedTenantAsync();
        var ordinaryUser = await _factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithTenant(tenantId)
            .WithRoles("PersonalUser").WithPermissions("Customers.Read"));
        var adminWithoutPermission = await _factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithTenant(tenantId)
            .WithRoles("TenantAdmin"));

        foreach (var route in new[] { $"{AdminRoute}/newsletter", $"{AdminRoute}/delivery-availability/areas" })
        {
            (await AnonymousClient(tenantId).GetAsync(route)).StatusCode.Should()
                .BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
            (await ordinaryUser.GetAsync(route)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await adminWithoutPermission.GetAsync(route)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }

    [Fact]
    public async Task AdminListsAndAreas_Should_PageAndCountOnlyActiveSubscriptionsForTheirTenant()
    {
        var tenantId = await SeedTenantAsync();
        var otherTenant = await SeedTenantAsync();
        var anonymous = AnonymousClient(tenantId);
        for (var index = 0; index < 3; index++)
            (await anonymous.PostAsJsonAsync($"{PublicRoute}/delivery-availability",
                Capture(SignupListTypes.DeliveryAvailability) with { Email = $"guest{index}@example.test" }))
                .StatusCode.Should().Be(HttpStatusCode.Accepted);
        await AnonymousClient(otherTenant).PostAsJsonAsync($"{PublicRoute}/delivery-availability",
            Capture(SignupListTypes.DeliveryAvailability));
        var admin = await AdminClient(tenantId);
        var firstPageResponse = await admin.GetAsync($"{AdminRoute}/delivery-availability?pageSize=1");
        var firstPage = await firstPageResponse.Content.ReadFromJsonAsync<PagedResult<SignupSubscriptionDto>>();
        firstPage!.TotalCount.Should().Be(3);
        firstPage.Items.Should().ContainSingle();
        firstPage.PageSize.Should().Be(1);
        firstPageResponse.Headers.CacheControl!.NoStore.Should().BeTrue();
        var secondPage = await admin.GetFromJsonAsync<PagedResult<SignupSubscriptionDto>>(
            $"{AdminRoute}/delivery-availability?pageNumber=2&pageSize=1");
        secondPage!.Items.Single().Id.Should().NotBe(firstPage.Items.Single().Id);

        var subscription = firstPage.Items.Single();
        await anonymous.PostAsJsonAsync($"{PublicRoute}/delivery-availability/{subscription.Id}/unsubscribe",
            new { token = subscription.UnsubscribeToken });
        var areaResponse = await admin.GetAsync($"{AdminRoute}/delivery-availability/areas");
        var areas = await areaResponse.Content.ReadFromJsonAsync<List<SignupAreaDemandDto>>();

        areaResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        areaResponse.Headers.CacheControl!.NoStore.Should().BeTrue();
        areas.Should().BeEquivalentTo(new[] { new SignupAreaDemandDto("SW1A", 2) });
        (await (await AdminClient(otherTenant)).GetFromJsonAsync<PagedResult<SignupSubscriptionDto>>(
            $"{AdminRoute}/delivery-availability"))!.Items.Should().ContainSingle();
        foreach (var query in new[] { "pageNumber=0", "pageSize=101" })
            (await admin.GetAsync($"{AdminRoute}/delivery-availability?{query}")).StatusCode.Should()
                .Be(HttpStatusCode.UnprocessableEntity);
        (await admin.GetAsync($"{AdminRoute}/delivery-availability?pageNumber=2147483647&pageSize=100"))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    private static void AssertPrivate(HttpResponseMessage response)
    {
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        response.Headers.GetValues("Referrer-Policy").Should().ContainSingle().Which.Should().Be("no-referrer");
    }

    private HttpClient AnonymousClient(Guid tenantId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        return client;
    }

    private Task<HttpClient> AdminClient(Guid tenantId)
        => _factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithTenant(tenantId)
            .WithRoles("TenantAdmin").WithPermissions("Customers.Read"));

    private async Task<Guid> SeedTenantAsync(bool configured = true)
    {
        var tenantId = Guid.NewGuid();
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId, Name = $"Signup tests {tenantId}", Environment = "Testing",
            DefaultCurrency = "GBP", SupportedCountriesJson = "[]", Status = TenantStatus.Active
        });
        if (configured)
            db.Settings.Add(new Setting
            {
                Id = Guid.NewGuid(), Key = SignupListSettingNames.Configuration, Value = Configuration(),
                Scope = SettingScope.Tenant, TenantId = tenantId
            });
        await db.SaveChangesAsync();
        return tenantId;
    }

    private static string Configuration()
        => JsonSerializer.Serialize(new SignupListsConfigurationDto([
            new(SignupListTypes.Newsletter, "v1", $"Consent for {SignupListTypes.Newsletter}"),
            new(SignupListTypes.DeliveryAvailability, "v1", $"Consent for {SignupListTypes.DeliveryAvailability}"),
            new(SignupListTypes.PrivateTable, "v1", $"Consent for {SignupListTypes.PrivateTable}",
                [new("recipe-development", "Recipe development")])
        ]), JsonOptions);

    private static SignupCaptureRequest Capture(string listType)
        => listType switch
        {
            SignupListTypes.DeliveryAvailability => new("guest@example.test", "v1", Postcode: "sw1a1aa"),
            SignupListTypes.PrivateTable => new("guest@example.test", "v1", Name: "Guest Cook", Country: "gb", Service: "recipe-development"),
            _ => new("guest@example.test", "v1")
        };
}
