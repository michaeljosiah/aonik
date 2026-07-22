using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

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
