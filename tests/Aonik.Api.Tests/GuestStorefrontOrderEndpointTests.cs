using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Entities.Fulfilment;
using Aonik.Commerce.Entities.Promotions;
using Aonik.Commerce.Services.Checkout;
using Aonik.Finance.Entities.Orders;
using Aonik.Infrastructure.Persistence;
using Aonik.Platform.Entities.Identity;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Api.Tests;

public class GuestStorefrontOrderEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public GuestStorefrontOrderEndpointTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Read_Should_ReturnConfirmationDetails_AndPollPersistedPaymentStatusWithTheSameToken()
    {
        // Arrange
        var seeded = await SeedGuestCheckoutAsync(Guid.NewGuid());
        using var client = Client(seeded.TenantId);
        client.DefaultRequestHeaders.Add("X-Order-Token", seeded.OrderToken);

        // Act
        using var pending = await client.GetAsync(GuestPath(seeded.OrderId));

        // Assert
        pending.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertPrivateHeaders(pending);
        var body = await pending.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("orderId").GetGuid().Should().Be(seeded.OrderId);
        body.GetProperty("paymentStatus").GetString().Should().Be("RequiresAction");
        body.GetProperty("status").GetString().Should().Be(OrderStatuses.Pending);
        body.GetProperty("total").GetDecimal().Should().Be(95m);
        body.GetProperty("boxSize").GetInt32().Should().Be(6);
        body.GetProperty("items").GetArrayLength().Should().Be(1);
        body.GetProperty("items")[0].GetProperty("sku").GetString().Should().Be("MEAL-BOX");
        body.GetProperty("selections").GetArrayLength().Should().Be(1);
        body.GetProperty("selections")[0].GetProperty("sku").GetString().Should().Be("DISH-01");
        body.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(
        [
            "orderId", "placedAtUtc", "status", "currency", "subtotal", "discountTotal", "taxTotal",
            "total", "boxSize", "items", "selections", "paymentStatus", "delivery",
        ]);
        var delivery = body.GetProperty("delivery");
        delivery.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(new[]
            { "purchaser", "address", "deliveryDate", "timezone", "recipient", "notes", "gift" });
        delivery.GetProperty("gift").ValueKind.Should().Be(JsonValueKind.Null);
        delivery.GetProperty("deliveryDate").GetString().Should().Be("2026-10-25");
        delivery.GetProperty("timezone").GetString().Should().Be("Europe/London");
        delivery.GetProperty("purchaser").GetProperty("email").GetString().Should().Be("purchaser@example.com");
        delivery.GetProperty("recipient").GetProperty("name").GetString().Should().Be("Sam Recipient");
        delivery.GetProperty("address").GetProperty("line1").GetString().Should().Be("10 Kitchen Road");
        delivery.GetProperty("notes").GetString().Should().Be("Ring the bell");
        body.GetRawText().Should().NotContain("private-checkout-secret").And.NotContain("private-checkout-url")
            .And.NotContain("private-order-note").And.NotContain("private@example.com")
            .And.NotContain("detailsJson").And.NotContain("payerPartyId").And.NotContain("tenantId")
            .And.NotContain("paymentIntentId").And.NotContain("invoiceId");

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = seeded.TenantId;
            var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
            var summary = await db.OrderChargeSummaries.SingleAsync(s => s.OrderId == seeded.OrderId);
            summary.PaymentStatus = CheckoutPaymentStatuses.Captured;
            await db.SaveChangesAsync();
        }

        using var paid = await client.GetAsync(GuestPath(seeded.OrderId));
        paid.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertPrivateHeaders(paid);
        var refreshed = await paid.Content.ReadFromJsonAsync<JsonElement>();
        refreshed.GetProperty("paymentStatus").GetString().Should().Be(CheckoutPaymentStatuses.Captured);
        refreshed.GetProperty("status").GetString().Should().Be(OrderStatuses.Pending,
            "payment and order states remain distinct");

        await AssertCheckoutUnchangedAsync(seeded);
    }

    [Fact]
    public async Task Read_Should_ReturnTheSame404_ForMissingMalformedRepeatedAndQueryTokens()
    {
        // Arrange
        var seeded = await SeedGuestCheckoutAsync(Guid.NewGuid());
        using var client = Client(seeded.TenantId);
        string?[] invalidTokens =
        [
            null, "", "not-a-valid-token", "a", seeded.CartToken, new string('x', 2048),
            seeded.OrderToken + "=", seeded.OrderToken.Insert(10, " "),
            seeded.OrderToken + "," + seeded.OrderToken,
        ];

        foreach (var token in invalidTokens)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, GuestPath(seeded.OrderId));
            if (token is not null) request.Headers.TryAddWithoutValidation("X-Order-Token", token);

            // Act
            using var response = await client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            AssertPrivateHeaders(response);
            (await response.Content.ReadAsStringAsync()).Should().NotContain("purchaser@example.com")
                .And.NotContain("Kitchen Road");
        }

        using var repeated = new HttpRequestMessage(HttpMethod.Get, GuestPath(seeded.OrderId));
        repeated.Headers.TryAddWithoutValidation("X-Order-Token", [seeded.OrderToken, seeded.OrderToken]);
        using var repeatedResponse = await client.SendAsync(repeated);
        repeatedResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        AssertPrivateHeaders(repeatedResponse);

        using var queryResponse = await client.GetAsync(
            GuestPath(seeded.OrderId) + "?token=" + Uri.EscapeDataString(seeded.OrderToken));
        queryResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        AssertPrivateHeaders(queryResponse);
    }

    [Fact]
    public async Task Read_Should_RejectTokensForAnotherOrderOrTenant()
    {
        // Arrange
        var seeded = await SeedGuestCheckoutAsync(Guid.NewGuid());
        var anotherOrder = await SeedGuestCheckoutAsync(seeded.TenantId);
        var anotherTenant = await SeedGuestCheckoutAsync(Guid.NewGuid());
        using var client = Client(seeded.TenantId);
        client.DefaultRequestHeaders.Add("X-Order-Token", seeded.OrderToken);

        // Act
        using var wrongOrder = await client.GetAsync(GuestPath(anotherOrder.OrderId));
        using var foreignClient = Client(anotherTenant.TenantId);
        foreignClient.DefaultRequestHeaders.Add("X-Order-Token", seeded.OrderToken);
        using var wrongTenant = await foreignClient.GetAsync(GuestPath(seeded.OrderId));

        // Assert
        wrongOrder.StatusCode.Should().Be(HttpStatusCode.NotFound);
        wrongTenant.StatusCode.Should().Be(HttpStatusCode.NotFound);
        AssertPrivateHeaders(wrongOrder);
        AssertPrivateHeaders(wrongTenant);
    }

    [Theory]
    [InlineData("cart")]
    [InlineData("order")]
    [InlineData("summary")]
    public async Task Read_Should_Return404_WhenACheckoutRecordIsMissing(string missing)
    {
        // Arrange
        var seeded = await SeedGuestCheckoutAsync(Guid.NewGuid());
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = seeded.TenantId;
            var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
            if (missing == "cart") db.Carts.Remove(await db.Carts.SingleAsync(c => c.Id == seeded.CartId));
            if (missing == "order") db.Set<Order>().Remove(await db.Set<Order>().SingleAsync(o => o.Id == seeded.OrderId));
            if (missing == "summary") db.OrderChargeSummaries.Remove(await db.OrderChargeSummaries.SingleAsync(s => s.OrderId == seeded.OrderId));
            await db.SaveChangesAsync();
        }
        using var client = Client(seeded.TenantId);
        client.DefaultRequestHeaders.Add("X-Order-Token", seeded.OrderToken);

        // Act
        using var response = await client.GetAsync(GuestPath(seeded.OrderId));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        AssertPrivateHeaders(response);
    }

    [Fact]
    public async Task CheckoutReplay_Should_ReturnProtectedGuestTokenAndPrivateHeaders_WithoutAnotherCheckout()
    {
        // Arrange
        var seeded = await SeedGuestCheckoutAsync(Guid.NewGuid());
        using var client = Client(seeded.TenantId);
        client.DefaultRequestHeaders.Add("X-Cart-Token", seeded.CartToken);
        var command = new { provider = "Stripe", paymentMethodType = "Card" };

        // Act
        using var response = await client.PostAsJsonAsync($"/commerce/carts/{seeded.CartId}/checkout", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertPrivateHeaders(response);
        var result = await response.Content.ReadFromJsonAsync<CheckoutResult>();
        result.Should().NotBeNull();
        result!.OrderId.Should().Be(seeded.OrderId);
        result.PaymentIntentId.Should().Be(seeded.PaymentIntentId);
        result.GuestOrderToken.Should().NotBeNullOrWhiteSpace();
        result.ClientSecret.Should().Be("private-checkout-secret");

        using var reader = Client(seeded.TenantId);
        foreach (var token in new[] { seeded.OrderToken, result.GuestOrderToken! })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, GuestPath(seeded.OrderId));
            request.Headers.Add("X-Order-Token", token);
            using var read = await reader.SendAsync(request);
            read.StatusCode.Should().Be(HttpStatusCode.OK, "replay does not revoke an earlier guest capability");
        }
        using var unauthorised = await reader.PostAsJsonAsync($"/commerce/carts/{seeded.CartId}/checkout", command);
        unauthorised.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await unauthorised.Content.ReadAsStringAsync()).Should().NotContain("guestOrderToken")
            .And.NotContain("private-checkout-secret");
        await AssertCheckoutUnchangedAsync(seeded);
    }

    [Fact]
    public async Task GuestToken_Should_NotAuthenticateTheCustomerOrderRoutes()
    {
        // Arrange
        var seeded = await SeedGuestCheckoutAsync(Guid.NewGuid());
        using var client = Client(seeded.TenantId);
        client.DefaultRequestHeaders.Add("X-Order-Token", seeded.OrderToken);

        // Act
        using var detail = await client.GetAsync($"/commerce/storefront/orders/{seeded.OrderId}");
        using var list = await client.GetAsync("/commerce/storefront/orders");

        // Assert
        detail.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        list.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("PlatformAdmin")]
    [InlineData("TenantAdmin")]
    [InlineData("Operations")]
    [InlineData("ReadOnly")]
    public async Task AdminReads_Should_AllowStaff_AndReturnPrivateDeliveryDetails(string role)
    {
        var tenantId = Guid.NewGuid();
        using var client = await _factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithTenant(tenantId).WithRoles(role));
        var seeded = await SeedGuestCheckoutAsync(tenantId);

        using var detail = await client.GetAsync($"/commerce/admin/orders/{seeded.OrderId}/storefront");
        using var list = await client.GetAsync("/commerce/admin/orders");

        detail.StatusCode.Should().Be(HttpStatusCode.OK);
        detail.Headers.CacheControl!.NoStore.Should().BeTrue();
        var order = await detail.Content.ReadFromJsonAsync<AdminOrderStorefrontDto>();
        order!.Delivery!.Address.Line1.Should().Be("10 Kitchen Road");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        list.Headers.CacheControl!.NoStore.Should().BeTrue();
        var page = await list.Content.ReadFromJsonAsync<JsonElement>();
        page.GetProperty("items")[0].GetProperty("deliveryDate").GetString().Should().Be("2026-10-25");
        page.GetRawText().Should().NotContain("purchaser@example.com").And.NotContain("Kitchen Road");

        using var otherTenant = await _factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithTenant(Guid.NewGuid()).WithRoles(role));
        using var hidden = await otherTenant.GetAsync($"/commerce/admin/orders/{seeded.OrderId}/storefront");
        hidden.StatusCode.Should().Be(HttpStatusCode.NotFound);
        hidden.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    [Fact]
    public async Task Customer_Should_ReadOnlyOwnDelivery_AndNeverUseTheTenantWideAdminOrderRoutes()
    {
        var tenantId = Guid.NewGuid();
        var options = TestAuthOptions.Create().WithTenant(tenantId).WithRoles("PersonalUser").WithPermissions("Customers.Read");
        using var customer = await _factory.CreateAuthenticatedClientAsync(options);
        var partyId = await WorkspaceTestSeeding.SeedPartyAsync(_factory, tenantId, options.UserId, "Delivery customer");
        var seeded = await SeedGuestCheckoutAsync(tenantId);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;
            var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
            (await db.Carts.SingleAsync(c => c.Id == seeded.CartId)).BuyerPartyId = partyId;
            await db.SaveChangesAsync();
        }

        using var detail = await customer.GetAsync($"/commerce/storefront/orders/{seeded.OrderId}");
        using var list = await customer.GetAsync("/commerce/storefront/orders");

        detail.StatusCode.Should().Be(HttpStatusCode.OK);
        detail.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await detail.Content.ReadFromJsonAsync<StorefrontOrderDetailDto>())!.Delivery!.Recipient.Name.Should().Be("Sam Recipient");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        list.Headers.CacheControl!.NoStore.Should().BeTrue();
        var page = await list.Content.ReadFromJsonAsync<JsonElement>();
        page.GetProperty("items")[0].GetProperty("deliveryDate").GetString().Should().Be("2026-10-25");
        page.GetRawText().Should().NotContain("purchaser@example.com").And.NotContain("Kitchen Road");
        foreach (var path in new[] { "/commerce/admin/orders", $"/commerce/admin/orders/{seeded.OrderId}/storefront" })
        {
            using var forbidden = await customer.GetAsync(path);
            forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await forbidden.Content.ReadAsStringAsync()).Should().NotContain("Kitchen Road");
        }

        var otherOptions = TestAuthOptions.Create().WithTenant(tenantId).WithRoles("PersonalUser").WithPermissions("Customers.Read");
        using var otherCustomer = await _factory.CreateAuthenticatedClientAsync(otherOptions);
        await WorkspaceTestSeeding.SeedPartyAsync(_factory, tenantId, otherOptions.UserId, "Another customer");
        using var hidden = await otherCustomer.GetAsync($"/commerce/storefront/orders/{seeded.OrderId}?partyId={partyId}");
        hidden.StatusCode.Should().Be(HttpStatusCode.NotFound);
        hidden.Headers.CacheControl!.NoStore.Should().BeTrue();

        using var admin = await _factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithTenant(tenantId).WithRoles("Operations"));
        using var partySummary = await admin.GetAsync($"/commerce/admin/parties/{partyId}/storefront");
        partySummary.StatusCode.Should().Be(HttpStatusCode.OK);
        partySummary.Headers.CacheControl!.NoStore.Should().BeTrue();
        var summary = await partySummary.Content.ReadAsStringAsync();
        summary.Should().Contain("2026-10-25").And.NotContain("purchaser@example.com").And.NotContain("Kitchen Road");
    }

    [Fact]
    public async Task LegacyOrder_Should_ReturnNullDelivery_WhenNoSnapshotWasRecorded()
    {
        var seeded = await SeedGuestCheckoutAsync(Guid.NewGuid());
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = seeded.TenantId;
            var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
            db.OrderDeliveryDetails.Remove(await db.OrderDeliveryDetails.SingleAsync(d => d.OrderId == seeded.OrderId));
            await db.SaveChangesAsync();
        }
        using var client = Client(seeded.TenantId);
        client.DefaultRequestHeaders.Add("X-Order-Token", seeded.OrderToken);

        using var response = await client.GetAsync(GuestPath(seeded.OrderId));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertPrivateHeaders(response);
        (await response.Content.ReadFromJsonAsync<StorefrontOrderDetailDto>())!.Delivery.Should().BeNull();
    }

    private HttpClient Client(Guid tenantId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        return client;
    }

    private static string GuestPath(Guid orderId) => $"/commerce/storefront/guest-orders/{orderId}";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Packing_Should_UseGiftSnapshot_AndOmitHiddenMoneyWithoutChangingFinancialReads(bool hidePrices)
    {
        var tenantId = Guid.NewGuid();
        using var staff = await _factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithTenant(tenantId).WithRoles("Operations"));
        var seeded = await SeedGuestCheckoutAsync(tenantId, gift: true, hidePrices: hidePrices, paymentStatus: CheckoutPaymentStatuses.Captured);

        using var response = await staff.GetAsync($"/commerce/admin/orders/{seeded.OrderId}/packing");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertPrivateHeaders(response);
        var packing = await response.Content.ReadFromJsonAsync<JsonElement>();
        packing.GetProperty("gift").GetProperty("hidePrices").GetBoolean().Should().Be(hidePrices);
        packing.GetProperty("gift").GetProperty("greetingCardMessage").GetString().Should().Be("Happy birthday!\n<script>literal text</script>");
        packing.GetProperty("recipient").GetProperty("name").GetString().Should().Be("Sam Recipient");
        packing.GetProperty("items")[1].GetProperty("name").GetString().Should().Be("Greeting card");
        var json = packing.GetRawText();
        json.Should().NotContain("purchaser@example.com").And.NotContain("1111 1111")
            .And.NotContain("private-checkout").And.NotContain("paymentIntentId").And.NotContain("buyerPartyId");
        packing.TryGetProperty("prices", out var prices).Should().Be(!hidePrices);
        if (hidePrices)
        {
            foreach (var property in new[] { "currency", "unitPrice", "amount", "total", "subtotal", "discountTotal", "taxTotal" })
                json.Should().NotContain($"\"{property}\":");
        }
        else
        {
            prices.GetProperty("charge").GetProperty("total").GetDecimal().Should().Be(98m);
            prices.GetProperty("items")[1].GetProperty("amount").GetDecimal().Should().Be(3m);
        }

        using var financeResponse = await staff.GetAsync($"/commerce/admin/orders/{seeded.OrderId}/storefront");
        var finance = await financeResponse.Content.ReadFromJsonAsync<AdminOrderStorefrontDto>();
        finance!.Charge.Total.Should().Be(98m);
        finance.Items.Single(item => item.ItemType == CheckoutService.GreetingCardItemType).IsAddOn.Should().BeFalse();
        using var guest = Client(tenantId);
        guest.DefaultRequestHeaders.Add("X-Order-Token", seeded.OrderToken);
        using var purchaserResponse = await guest.GetAsync(GuestPath(seeded.OrderId));
        var purchaser = await purchaserResponse.Content.ReadFromJsonAsync<StorefrontOrderDetailDto>();
        purchaser!.Total.Should().Be(98m);
        purchaser.Delivery!.Gift!.HidePrices.Should().Be(hidePrices);
    }

    [Theory]
    [InlineData("RequiresAction")]
    [InlineData("Processing")]
    [InlineData("Cancelled")]
    public async Task Packing_Should_RejectUnconfirmedPayment(string paymentStatus)
    {
        var tenantId = Guid.NewGuid();
        using var staff = await _factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithTenant(tenantId).WithRoles("Operations"));
        var seeded = await SeedGuestCheckoutAsync(tenantId, gift: true, paymentStatus: paymentStatus);
        using var response = await staff.GetAsync($"/commerce/admin/orders/{seeded.OrderId}/packing");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        AssertPrivateHeaders(response);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("Kitchen Road").And.NotContain("purchaser@example.com");
    }

    [Theory]
    [InlineData("PlatformAdmin")]
    [InlineData("TenantAdmin")]
    [InlineData("Operations")]
    [InlineData("ReadOnly")]
    public async Task Packing_Should_AllowStaff_AndIsolateOtherTenants(string role)
    {
        var tenantId = Guid.NewGuid();
        using var staff = await _factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithTenant(tenantId).WithRoles(role));
        var seeded = await SeedGuestCheckoutAsync(tenantId, paymentStatus: CheckoutPaymentStatuses.Captured);
        var path = $"/commerce/admin/orders/{seeded.OrderId}/packing";
        using var response = await staff.GetAsync(path);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertPrivateHeaders(response);
        using var other = await _factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithTenant(Guid.NewGuid()).WithRoles(role));
        using var hidden = await other.GetAsync(path);
        hidden.StatusCode.Should().Be(HttpStatusCode.NotFound);
        AssertPrivateHeaders(hidden);
    }

    [Fact]
    public async Task Packing_Should_RejectAnonymousAndCustomerAccess_EvenWithGuestCapability()
    {
        var tenantId = Guid.NewGuid();
        var seeded = await SeedGuestCheckoutAsync(tenantId, gift: true, paymentStatus: CheckoutPaymentStatuses.Captured);
        var path = $"/commerce/admin/orders/{seeded.OrderId}/packing";
        using var anonymous = Client(tenantId);
        anonymous.DefaultRequestHeaders.Add("X-Order-Token", seeded.OrderToken);
        using var denied = await anonymous.GetAsync(path);
        denied.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        AssertPrivateHeaders(denied);
        using var customer = await _factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithTenant(tenantId).WithRoles("PersonalUser"));
        using var forbidden = await customer.GetAsync(path);
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        AssertPrivateHeaders(forbidden);
    }

    private static void AssertPrivateHeaders(HttpResponseMessage response)
    {
        response.Headers.CacheControl.Should().NotBeNull();
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        response.Headers.GetValues("Referrer-Policy").Should().Equal("no-referrer");
    }

    private async Task AssertCheckoutUnchangedAsync(GuestCheckout seeded)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = seeded.TenantId;
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        var cart = await db.Carts.SingleAsync(c => c.Id == seeded.CartId);
        cart.OrderId.Should().Be(seeded.OrderId);
        cart.Status.Should().Be(CartStatuses.Open);
        cart.AnonymousToken.Should().Be(seeded.CartToken);
        (await db.Set<Order>().CountAsync(o => o.TenantId == seeded.TenantId)).Should().Be(1);
        (await db.OrderChargeSummaries.CountAsync(s => s.TenantId == seeded.TenantId)).Should().Be(1);
        (await db.OrderChargeSummaries.SingleAsync(s => s.OrderId == seeded.OrderId)).PaymentIntentId
            .Should().Be(seeded.PaymentIntentId);
    }

    private async Task<GuestCheckout> SeedGuestCheckoutAsync(Guid tenantId, bool gift = false, bool hidePrices = true,
        string paymentStatus = "RequiresAction")
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        if (!await db.Tenants.AnyAsync(t => t.Id == tenantId))
        {
            db.Tenants.Add(new Tenant
            {
                Id = tenantId, Name = $"Guest order tenant {tenantId:N}", Environment = "Testing",
                DefaultCurrency = "GBP", SupportedCountriesJson = "[]", Status = TenantStatus.Active,
            });
        }

        var orderId = Guid.NewGuid();
        var cartId = Guid.NewGuid();
        var paymentIntentId = Guid.NewGuid();
        var cartToken = CartAccess.MintToken();
        Guid? bundleProductId = gift ? Guid.NewGuid() : null;
        var total = gift ? 98m : 95m;
        db.Set<Order>().Add(new Order
        {
            Id = orderId, TenantId = tenantId, OrderType = "ProductPurchase",
            PayerPartyId = Guid.NewGuid(), AmountIn = total, CurrencyIn = "GBP",
            Status = OrderStatuses.Pending, ProvenanceJson = "{}",
            Items =
            [
                new OrderItem
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, OrderId = orderId, ItemType = "ProductPurchase",
                    Quantity = 1m, UnitPrice = 95m, AmountIn = 95m, CurrencyIn = "GBP", Sku = "MEAL-BOX",
                    Status = OrderStatuses.Pending, ProductId = bundleProductId,
                    DetailsJson = """{"privateNote":"private-order-note","email":"private@example.com"}""",
                },
            ],
        });
        if (gift)
            db.Set<OrderItem>().Add(new OrderItem
            {
                Id = Guid.NewGuid(), TenantId = tenantId, OrderId = orderId, ItemIndex = 1,
                ItemType = CheckoutService.GreetingCardItemType, Quantity = 1, UnitPrice = 3m,
                AmountIn = 3m, CurrencyIn = "GBP", Sku = "GREETING-CARD", Status = OrderStatuses.Pending,
            });
        db.Carts.Add(new Cart
        {
            Id = cartId, TenantId = tenantId, AnonymousToken = cartToken, OrderId = orderId,
            Currency = "GBP", Status = CartStatuses.Open, BoxSize = 6, BoxBundleProductId = bundleProductId,
        });
        db.OrderChargeSummaries.Add(new OrderChargeSummary
        {
            Id = Guid.NewGuid(), TenantId = tenantId, OrderId = orderId, Currency = "GBP",
            Subtotal = total, Total = total, PaymentIntentId = paymentIntentId,
            PaymentStatus = paymentStatus, PaymentClientSecret = "private-checkout-secret", GreetingCardCharged = gift ? 3m : 0m,
            PaymentCheckoutUrl = "https://example.com/private-checkout-url",
        });
        db.OrderBundleSelections.Add(new OrderBundleSelection
        {
            Id = Guid.NewGuid(), TenantId = tenantId, OrderId = orderId,
            BundleSlotId = Guid.NewGuid(), ProductVariantId = Guid.NewGuid(), Quantity = 6m, Sku = "DISH-01",
        });
        db.OrderDeliveryDetails.Add(new OrderDeliveryDetails
        {
            TenantId = tenantId, OrderId = orderId, DeliveryDate = new DateOnly(2026, 10, 25), Timezone = "Europe/London",
            PurchaserEmail = "purchaser@example.com", PurchaserFirstName = "Ada", PurchaserLastName = "Cook", PurchaserPhone = "+44 20 1111 1111",
            RecipientName = "Sam Recipient", RecipientPhone = "+44 20 2222 2222",
            AddressLine1 = "10 Kitchen Road", City = "London", Postcode = "SW1A 1AA", CountryCode = "GB", Notes = "Ring the bell",
            IsGift = gift, HidePrices = hidePrices, IncludeGreetingCard = gift,
            GreetingCardMessage = gift ? "Happy birthday!\n<script>literal text</script>" : null,
        });
        await db.SaveChangesAsync();

        var token = scope.ServiceProvider.GetRequiredService<GuestOrderAccess>().Issue(tenantId, orderId);
        return new GuestCheckout(tenantId, orderId, cartId, paymentIntentId, cartToken, token);
    }

    private sealed record GuestCheckout(
        Guid TenantId, Guid OrderId, Guid CartId, Guid PaymentIntentId, string CartToken, string OrderToken);
}
