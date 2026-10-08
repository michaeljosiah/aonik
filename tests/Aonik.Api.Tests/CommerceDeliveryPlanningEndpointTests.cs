using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Production;
using Aonik.Commerce.Entities.Fulfilment;
using Aonik.Infrastructure.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Ordering;

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Api.Tests;

public class CommerceDeliveryPlanningEndpointTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task ProductionSheet_Should_SelectRecordedDeliveryDate_WithoutUtcWindow()
    {
        var tenantId = Guid.NewGuid();
        using var client = await AdminClient(tenantId);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;
            var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();
            var order = await orders.CreateAsync(new CreateOrderCommand(OrderTypeCodes.ProductPurchase, null, "GBP",
                [new OrderItemCommand(OrderTypeCodes.ProductPurchase, 0, 7m, "GBP", Quantity: 7m, ProductId: Guid.NewGuid())]));
            await orders.TransitionAsync(order.Id, OrderStatusCodes.Pending);
            var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
            db.OrderDeliveryDetails.Add(new OrderDeliveryDetails
            {
                Id = Guid.NewGuid(), TenantId = tenantId, OrderId = order.Id,
                DeliveryDate = new DateOnly(2026, 7, 10), Timezone = "Europe/London",
                PurchaserEmail = "buyer@example.com", PurchaserFirstName = "Test", PurchaserLastName = "Buyer",
                PurchaserPhone = "07700900123", RecipientName = "Test Buyer", RecipientPhone = "07700900123",
                AddressLine1 = "1 Test Road", City = "London", Postcode = "SW1A 1AA", CountryCode = "GB",
            });
            await db.SaveChangesAsync();
        }

        using var response = await client.GetAsync("/commerce/admin/planning/production-sheet?deliveryDate=2026-07-10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var sheet = await response.Content.ReadFromJsonAsync<ProductionSheetDto>();
        sheet!.Window.Should().BeNull();
        sheet.DeliveryDate.Should().Be(new DateOnly(2026, 7, 10));
        sheet.Lines.Should().ContainSingle().Which.PortionsDemanded.Should().Be(7m);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("buyer@example.com").And.NotContain("1 Test Road");
    }

    [Theory]
    [InlineData("production-sheet")]
    [InlineData("prep-list")]
    public async Task PlanningRead_Should_RejectMissingIncompleteAndMixedSelectors(string endpoint)
    {
        using var client = await AdminClient(Guid.NewGuid());
        foreach (var query in new[]
        {
            "", "?fromUtc=2026-07-01T00:00:00Z", "?toUtc=2026-07-02T00:00:00Z",
            "?deliveryDate=2026-07-10&fromUtc=2026-07-01T00:00:00Z",
        })
        {
            using var response = await client.GetAsync($"/commerce/admin/planning/{endpoint}{query}");
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest, query);
        }
        foreach (var query in new[]
        {
            "?deliveryDate=invalid",
            "?deliveryDate=invalid&fromUtc=2026-07-01T00:00:00Z&toUtc=2026-07-02T00:00:00Z",
            "?deliveryDate=2026-07-10&fromUtc=invalid",
            "?deliveryDate=2026-07-10&toUtc=invalid",
        })
        {
            using var response = await client.GetAsync($"/commerce/admin/planning/{endpoint}{query}");
            response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, query);
        }

        using var dateRead = await client.GetAsync($"/commerce/admin/planning/{endpoint}?deliveryDate=2026-07-10");
        dateRead.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await dateRead.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("deliveryDate").GetString().Should().Be("2026-07-10");

        using var rangeRead = await client.GetAsync($"/commerce/admin/planning/{endpoint}?fromUtc=2026-07-01T00:00:00Z&toUtc=2026-07-02T00:00:00Z");
        rangeRead.StatusCode.Should().Be(HttpStatusCode.OK, "the creation-time selector remains supported");
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"fromUtc\":\"2026-07-01T00:00:00Z\"}")]
    [InlineData("{\"deliveryDate\":\"2026-07-10\"}")]
    [InlineData("{\"deliveryDate\":\"2026-07-10\",\"fromUtc\":\"2026-07-01T00:00:00Z\",\"plannedFor\":\"2026-07-08T06:00:00Z\"}")]
    public async Task ProductionFromSheet_Should_RejectIncompleteOrAmbiguousSelection(string json)
    {
        using var client = await AdminClient(Guid.NewGuid());

        using var response = await client.PostAsJsonAsync("/commerce/admin/production-orders/from-sheet", JsonSerializer.Deserialize<JsonElement>(json));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private Task<HttpClient> AdminClient(Guid tenantId) => factory.CreateAuthenticatedClientAsync(
        TestAuthOptions.Create().WithRoles("Operations").WithTenant(tenantId));
}
