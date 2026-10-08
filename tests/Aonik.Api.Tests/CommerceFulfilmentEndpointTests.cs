using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Fulfilment;
using Aonik.Commerce.Entities.Fulfilment;
using Aonik.Infrastructure.Persistence;
using Aonik.Platform.Entities.Identity;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Api.Tests;

/// <summary>
/// Spec 069 §6 over the real DI container: the anonymous promise read (200/404, tenant-partitioned
/// caching — A7), and the admin calendar surface's authorization.
/// </summary>
public class CommerceFulfilmentEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public CommerceFulfilmentEndpointTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task DeliveryConfig_Should_Serve404Unconfigured_And200WithTenantPartitioning()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await SeedTenantAsync(tenantA);
        await SeedTenantAsync(tenantB);

        // A4 — unconfigured is a 404, uncached.
        var before = await Client(tenantA).GetAsync("/commerce/config/delivery");
        before.StatusCode.Should().Be(HttpStatusCode.NotFound);
        before.Headers.CacheControl!.NoStore.Should().BeTrue();

        await SeedCalendarAsync(tenantA);

        var response = await Client(tenantA).GetAsync("/commerce/config/delivery");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Vary.Should().Contain("X-Tenant-Id", "A7 — a shared cache must never cross-serve tenants");
        response.Headers.CacheControl!.Public.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().Be(TimeSpan.FromMinutes(5));
        var promise = await response.Content.ReadFromJsonAsync<JsonElement>();
        promise.GetProperty("timezone").GetString().Should().Be("Europe/London");
        DateOnly.Parse(promise.GetProperty("earliestDeliveryDate").GetString()!)
            .DayOfWeek.Should().Be(DayOfWeek.Thursday, "the calendar delivers Thursdays");

        // A7 — tenant B has no calendar and sees no promise.
        (await Client(tenantB).GetAsync("/commerce/config/delivery"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AdminCalendar_Should_RejectAnonymous_AndEchoThePromiseOnUpsert()
    {
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId);

        (await Client(tenantId).GetAsync("/commerce/admin/fulfilment-calendar"))
            .StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
        (await Client(tenantId).PutAsJsonAsync("/commerce/admin/fulfilment-calendar", new { timezone = "Europe/London" }))
            .StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);

        var admin = await _factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create().WithRoles("Operations").WithTenant(tenantId));
        var upsert = await admin.PutAsJsonAsync("/commerce/admin/fulfilment-calendar", new
        {
            timezone = "Europe/London",
            deliveryDays = new[] { "thursday" },
            cutoffLocalTime = "12:00:00",
            leadDays = 14,
            blackoutDates = Array.Empty<string>(),
            isActive = true,
        });
        upsert.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await upsert.Content.ReadFromJsonAsync<JsonElement>();
        dto.GetProperty("currentPromise").ValueKind.Should().Be(JsonValueKind.Object,
            "A5 — the upsert response already shows the new promise");

        var read = await admin.GetAsync("/commerce/admin/fulfilment-calendar");
        read.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeliveryDates_Should_ServeTheCurrentCalendar_WithoutCachingOrCrossTenantFallback()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await SeedTenantAsync(tenantA);
        await SeedTenantAsync(tenantB);
        await SeedCalendarAsync(tenantA);
        using var client = Client(tenantA);

        var response = await client.GetAsync("/commerce/config/delivery/dates");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        response.Headers.Vary.Should().Contain("X-Tenant-Id");
        var dates = await response.Content.ReadFromJsonAsync<DeliveryDatesDto>();
        dates.Should().NotBeNull();
        dates!.Timezone.Should().Be("Europe/London");
        dates.FromDate.Should().Be(dates.EarliestDeliveryDate);
        dates.ToDate.Should().Be(dates.FromDate.AddDays(30));
        dates.Dates.Should().HaveCount(5).And.BeInAscendingOrder().And.OnlyContain(date => date.DayOfWeek == DayOfWeek.Thursday);

        var single = await client.GetFromJsonAsync<DeliveryDatesDto>(
            $"/commerce/config/delivery/dates?fromDate={dates.FromDate:yyyy-MM-dd}&days=1");
        single!.Dates.Should().Equal(dates.EarliestDeliveryDate);
        var maximum = await client.GetFromJsonAsync<DeliveryDatesDto>(
            $"/commerce/config/delivery/dates?fromDate={dates.FromDate:yyyy-MM-dd}&days=62");
        maximum!.ToDate.Should().Be(dates.FromDate.AddDays(61));
        var past = await client.GetFromJsonAsync<DeliveryDatesDto>("/commerce/config/delivery/dates?fromDate=2020-01-01&days=1");
        past!.Dates.Should().BeEmpty();

        var missing = await Client(tenantB).GetAsync("/commerce/config/delivery/dates");
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        missing.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    [Theory]
    [InlineData("?days=0", HttpStatusCode.BadRequest)]
    [InlineData("?days=63", HttpStatusCode.BadRequest)]
    [InlineData("?fromDate=9999-12-31&days=2", HttpStatusCode.BadRequest)]
    [InlineData("?days=invalid", HttpStatusCode.UnprocessableEntity)]
    [InlineData("?fromDate=invalid", HttpStatusCode.UnprocessableEntity)]
    [InlineData("?days=", HttpStatusCode.UnprocessableEntity)]
    public async Task DeliveryDates_Should_RejectInvalidRangeInput(string query, HttpStatusCode expected)
    {
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId);

        var response = await Client(tenantId).GetAsync("/commerce/config/delivery/dates" + query);

        response.StatusCode.Should().Be(expected);
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("deleted")]
    [InlineData("invalid-blackouts")]
    public async Task DeliveryDates_Should_WithholdUnavailableCalendars(string state)
    {
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId);
        await SeedCalendarAsync(tenantId);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;
            var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
            var calendar = await db.FulfilmentCalendars.SingleAsync(row => row.TenantId == tenantId);
            if (state == "inactive") calendar.IsActive = false;
            if (state == "deleted") calendar.IsDeleted = true;
            if (state == "invalid-blackouts") calendar.BlackoutDatesJson = "invalid";
            await db.SaveChangesAsync();
        }

        var response = await Client(tenantId).GetAsync("/commerce/config/delivery/dates");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    [Fact]
    public async Task DeliveryDates_Should_ReflectAdminCalendarChangesImmediately_AndSupportHeaderlessCustomerReads()
    {
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId);
        var admin = await _factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create().WithRoles("Operations").WithTenant(tenantId));
        var initial = await admin.PutAsJsonAsync("/commerce/admin/fulfilment-calendar", new
        {
            timezone = "Europe/London",
            deliveryDays = new[] { "thursday" },
            cutoffLocalTime = "12:00:00",
            leadDays = 7,
            blackoutDates = Array.Empty<string>(),
            isActive = true,
        });
        initial.StatusCode.Should().Be(HttpStatusCode.OK);
        var initialCalendar = await initial.Content.ReadFromJsonAsync<FulfilmentCalendarDto>();
        var firstDate = initialCalendar!.CurrentPromise!.EarliestDeliveryDate;
        var customer = await _factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create().WithRoles("PersonalUser").WithTenant(tenantId));
        customer.DefaultRequestHeaders.Remove("X-Tenant-Id");
        var route = $"/commerce/config/delivery/dates?fromDate={firstDate:yyyy-MM-dd}&days=1";
        var before = await customer.GetAsync(route);
        before.StatusCode.Should().Be(HttpStatusCode.OK);
        before.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await before.Content.ReadFromJsonAsync<DeliveryDatesDto>())!.Dates.Should().Equal(firstDate);

        var changed = await admin.PutAsJsonAsync("/commerce/admin/fulfilment-calendar", new
        {
            timezone = "Europe/London",
            deliveryDays = new[] { "thursday" },
            cutoffLocalTime = "12:00:00",
            leadDays = 7,
            blackoutDates = new[] { firstDate.ToString("yyyy-MM-dd") },
            isActive = true,
        });
        changed.StatusCode.Should().Be(HttpStatusCode.OK);
        var after = await customer.GetAsync(route);

        after.StatusCode.Should().Be(HttpStatusCode.OK);
        after.Headers.CacheControl!.NoStore.Should().BeTrue();
        var updatedDates = await after.Content.ReadFromJsonAsync<DeliveryDatesDto>();
        updatedDates!.Dates.Should().BeEmpty();
        updatedDates.EarliestDeliveryDate.Should().Be(firstDate.AddDays(7));
    }

    // ─── Seeding ─────────────────────────────────────────────────────────────

    private HttpClient Client(Guid tenantId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        return client;
    }

    private async Task SeedTenantAsync(Guid tenantId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Fulfilment Test Tenant",
            Environment = "Testing",
            DefaultCurrency = "GBP",
            SupportedCountriesJson = "[]",
            Status = TenantStatus.Active,
        });
        await db.SaveChangesAsync();
    }

    private async Task SeedCalendarAsync(Guid tenantId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;
        db.FulfilmentCalendars.Add(new FulfilmentCalendar
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Timezone = "Europe/London",
            DeliveryDaysJson = """["thursday"]""",
            CutoffLocalTime = new TimeOnly(12, 0),
            LeadDays = 14,
            IsActive = true,
        });
        await db.SaveChangesAsync();
    }
}
