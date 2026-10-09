using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Fulfilment;
using Aonik.Commerce.Entities.Fulfilment;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Services.Checkout;
using Aonik.Infrastructure.Persistence;
using Aonik.Platform.Entities.Identity;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions;

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
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
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
        await SeedCapacitiesAsync(tenantId);
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
        dates.FromDate.Should().Be(dates.EarliestDeliveryDate!.Value);
        dates.ToDate.Should().Be(dates.FromDate.AddDays(30));
        dates.Dates.Should().HaveCount(5).And.BeInAscendingOrder().And.OnlyContain(date => date.DayOfWeek == DayOfWeek.Thursday);

        var single = await client.GetFromJsonAsync<DeliveryDatesDto>(
            $"/commerce/config/delivery/dates?fromDate={dates.FromDate:yyyy-MM-dd}&days=1");
        single!.Dates.Should().Equal(dates.EarliestDeliveryDate!.Value);
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
        await SeedCapacitiesAsync(tenantId);
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

    [Fact]
    public async Task ReservationEndpoints_Should_RequireCartAuthorityAndVersion_AndPreserveExpiryOnReplay()
    {
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId);
        await SeedCalendarAsync(tenantId);
        using var client = Client(tenantId);
        var date = (await client.GetFromJsonAsync<FulfilmentPromiseDto>("/commerce/config/delivery"))!.EarliestDeliveryDate;
        var (cartId, token, version) = await SeedCartAsync(tenantId);
        var route = $"/commerce/carts/{cartId}/delivery-reservation";
        (await client.GetAsync(route)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        client.DefaultRequestHeaders.Add("X-Cart-Token", token);
        (await client.PutAsJsonAsync(route, new ReserveDeliveryDateRequest(date))).StatusCode.Should().Be(HttpStatusCode.Conflict);
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Cart-Version", version);

        var response = await client.PutAsJsonAsync(route, new ReserveDeliveryDateRequest(date));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var first = (await response.Content.ReadFromJsonAsync<CartDeliveryReservationDto>())!;
        first.Reservation!.Status.Should().Be(DeliveryReservationStatuses.Held);
        first.Reservation.ExpiresAtUtc.Should().Be(first.Reservation.SelectedAtUtc.AddMinutes(15));
        client.DefaultRequestHeaders.Remove("X-Cart-Version");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Cart-Version", first.CartVersion);
        var replay = await client.PutAsJsonAsync(route, new ReserveDeliveryDateRequest(date));
        (await replay.Content.ReadFromJsonAsync<CartDeliveryReservationDto>())!.Reservation.Should().Be(first.Reservation);

        var released = await client.DeleteAsync(route);
        released.StatusCode.Should().Be(HttpStatusCode.OK);
        (await released.Content.ReadFromJsonAsync<CartDeliveryReservationDto>())!.Reservation!.Status.Should().Be(DeliveryReservationStatuses.Released);
    }

    [Fact]
    public async Task CapacityEndpoints_Should_RequireAdminAndExplicitUnit_AndExposeFullVersusUnknown()
    {
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId);
        await SeedCalendarAsync(tenantId);
        using var client = Client(tenantId);
        var date = (await client.GetFromJsonAsync<FulfilmentPromiseDto>("/commerce/config/delivery"))!.EarliestDeliveryDate;
        var route = $"/commerce/admin/delivery-capacity/{date:yyyy-MM-dd}";
        (await client.PutAsJsonAsync(route, new UpdateDeliveryDateCapacityRequest("box", 0)))
            .StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
        var admin = await _factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithRoles("Operations").WithTenant(tenantId));
        var customer = await _factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithRoles("PersonalUser").WithTenant(tenantId));
        (await customer.GetAsync($"/commerce/admin/delivery-capacity?fromDate={date:yyyy-MM-dd}&days=1"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var configured = (await admin.GetFromJsonAsync<List<DeliveryDateCapacityDto>>($"/commerce/admin/delivery-capacity?fromDate={date:yyyy-MM-dd}&days=1"))!.Single();
        (await admin.PutAsJsonAsync(route, new UpdateDeliveryDateCapacityRequest("portions", 1, configured.Version)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PutAsJsonAsync(route, new UpdateDeliveryDateCapacityRequest("box", 0, configured.Version)))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var (cartId, token, version) = await SeedCartAsync(tenantId);
        client.DefaultRequestHeaders.Add("X-Cart-Token", token);
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Cart-Version", version);
        var reserve = $"/commerce/carts/{cartId}/delivery-reservation";
        (await client.PutAsJsonAsync(reserve, new ReserveDeliveryDateRequest(date))).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await client.PutAsJsonAsync(reserve, new ReserveDeliveryDateRequest(date.AddDays(140)))).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var availability = await client.GetFromJsonAsync<DeliveryDatesDto>($"/commerce/config/delivery/dates?fromDate={date:yyyy-MM-dd}&days=2");
        availability!.Availability!.Select(d => d.Status).Should().Equal("fully_booked", "no_delivery");
    }

    [Theory]
    [InlineData("{\"unit\":\"box\"}")]
    [InlineData("{\"unit\":\"box\",\"capcity\":10}")]
    public async Task CapacityEndpoints_Should_RejectAbsentCapacity_AndAcceptAnExplicitZero(string json)
    {
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId);
        using var admin = await _factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithRoles("Operations").WithTenant(tenantId));
        const string writeRoute = "/commerce/admin/delivery-capacity/2030-01-03";
        const string readRoute = "/commerce/admin/delivery-capacity?fromDate=2030-01-03&days=1";
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        using var rejected = await admin.PutAsync(writeRoute, content);

        rejected.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);
        (await admin.GetFromJsonAsync<List<DeliveryDateCapacityDto>>(readRoute)).Should().BeEmpty();
        using var accepted = await admin.PutAsJsonAsync(writeRoute, new UpdateDeliveryDateCapacityRequest("box", 0));
        accepted.StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.GetFromJsonAsync<List<DeliveryDateCapacityDto>>(readRoute))!.Should()
            .ContainSingle().Which.Capacity.Should().Be(0);
    }

    [Fact]
    public async Task DeliveryDates_Should_WithholdPromise_WhenCalendarExistsButCapacityWasNotAuthored()
    {
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(tenantId);
        await SeedCalendarAsync(tenantId);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;
            var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
            db.DeliveryDateCapacities.RemoveRange(await db.DeliveryDateCapacities.Where(row => row.TenantId == tenantId).ToListAsync());
            await db.SaveChangesAsync();
        }
        using var client = Client(tenantId);

        (await client.GetAsync("/commerce/config/delivery")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var dates = (await client.GetFromJsonAsync<DeliveryDatesDto>("/commerce/config/delivery/dates"))!;
        dates.EarliestDeliveryDate.Should().BeNull();
        dates.Dates.Should().BeEmpty();
        dates.Availability.Should().Contain(date => date.Status == "unknown");
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
        await SeedCapacitiesAsync(tenantId);
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

    private async Task SeedCapacitiesAsync(Guid tenantId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        var today = DateOnly.FromDateTime(scope.ServiceProvider.GetRequiredService<IClock>().UtcNow);
        db.DeliveryDateCapacities.AddRange(Enumerable.Range(0, 120).Select(offset => new DeliveryDateCapacity
        {
            TenantId = tenantId, DeliveryDate = today.AddDays(offset), Unit = "box", Capacity = 20
        }));
        await db.SaveChangesAsync();
    }

    private async Task<(Guid Id, string Token, string Version)> SeedCartAsync(Guid tenantId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        var cart = new Cart
        {
            TenantId = tenantId, Currency = "GBP", BoxBundleProductId = Guid.NewGuid(), BoxSize = 6,
            AnonymousToken = CartAccess.MintToken()
        };
        db.Carts.Add(cart);
        await db.SaveChangesAsync();
        return (cart.Id, cart.AnonymousToken, Convert.ToBase64String(cart.RowVersion));
    }
}
