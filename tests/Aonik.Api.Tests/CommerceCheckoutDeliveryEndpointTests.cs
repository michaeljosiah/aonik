using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;

using Aonik.Commerce.Contracts.Api.Checkout;
using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Contracts.Models.Fulfilment;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Entities.Fulfilment;
using Aonik.Commerce.Entities.Inventory;
using Aonik.Commerce.Entities.Promotions;
using Aonik.Commerce.Services.Checkout;
using Aonik.Commerce.Services.Fulfilment;
using Aonik.Finance.Entities.Orders;
using Aonik.Infrastructure.Persistence;
using Aonik.Platform.Entities.Identity;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Payments;

using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aonik.Api.Tests;

public class CommerceCheckoutDeliveryEndpointTests : IClassFixture<CommerceCheckoutDeliveryEndpointTests.CheckoutDeliveryFactory>
{
    private readonly CheckoutDeliveryFactory _factory;
    private static readonly DateOnly SelectedDate = new(2026, 10, 15);

    public CommerceCheckoutDeliveryEndpointTests(CheckoutDeliveryFactory factory) => _factory = factory;

    [Fact]
    public async Task Checkout_Should_PersistNormalizedDelivery_AndReplayItsSnapshotAfterCalendarChanges()
    {
        var seeded = await SeedCartAsync();
        using var client = Client(seeded);
        var input = ValidDelivery() with
        {
            Purchaser = new(" Buyer@Example.test ", " Ada ", " Customer ", " +44 7700 900111 "),
            Address = new(" 12 Sample Street ", " ", " London ", " Greater London ", " sw1a 1aa ", " gb "),
            Recipient = new(" Sam Recipient ", " 07700 900222 "),
            Notes = " Ring the bell.\nLeave with reception. "
        };

        using var response = await client.PostAsJsonAsync(CheckoutPath(seeded), Request(input));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var checkout = (await response.Content.ReadFromJsonAsync<CheckoutResult>())!;
        checkout.Total.Should().Be(20m);
        checkout.PaymentStatus.Should().Be("RequiresAction");
        checkout.GuestOrderToken.Should().NotBeNullOrWhiteSpace();
        var expected = new OrderDeliveryDto(
            new("Buyer@Example.test", "Ada", "Customer", "+44 7700 900111"),
            new("12 Sample Street", null, "London", "Greater London", "SW1A 1AA", "GB"),
            SelectedDate, "Europe/London", new("Sam Recipient", "07700 900222"),
            "Ring the bell.\nLeave with reception.");
        using var confirmationClient = Client(seeded, includeCartToken: false);
        confirmationClient.DefaultRequestHeaders.Add("X-Order-Token", checkout.GuestOrderToken);
        using var confirmation = await confirmationClient.GetAsync(GuestPath(checkout.OrderId));
        confirmation.StatusCode.Should().Be(HttpStatusCode.OK);
        confirmation.Headers.CacheControl!.NoStore.Should().BeTrue();
        confirmation.Headers.GetValues("Referrer-Policy").Should().Equal("no-referrer");
        (await confirmation.Content.ReadFromJsonAsync<StorefrontOrderDetailDto>())!.Delivery.Should().Be(expected);
        (await confirmation.Content.ReadAsStringAsync()).Should().NotContain("private-payment-secret")
            .And.NotContain("private-payment-url");

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = seeded.TenantId;
            var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
            var calendar = await db.FulfilmentCalendars.SingleAsync(row => row.TenantId == seeded.TenantId);
            calendar.IsActive = false;
            calendar.Timezone = "UTC";
            await db.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Remove("X-Cart-Version");
        using var replayResponse = await client.PostAsJsonAsync(CheckoutPath(seeded), Request(input with
        {
            Purchaser = null!, Address = null!, DeliveryDate = default, WindowId = "unsupported"
        }));
        replayResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var replay = (await replayResponse.Content.ReadFromJsonAsync<CheckoutResult>())!;
        (replay with { GuestOrderToken = null }).Should().Be(checkout with { GuestOrderToken = null });
        _factory.PaymentCalls.Count.Should().Be(seeded.PaymentCallsBefore + 1);

        using var reread = await confirmationClient.GetAsync(GuestPath(checkout.OrderId));
        (await reread.Content.ReadFromJsonAsync<StorefrontOrderDetailDto>())!.Delivery.Should().Be(expected,
            "the originally issued read token and delivery facts survive later request and calendar changes");
        await AssertOneCheckoutAsync(seeded, checkout.OrderId, expected);
    }

    [Fact]
    public async Task Checkout_Should_UseSubmittedPurchaser_WhenRecipientIsNotProvided()
    {
        var seeded = await SeedCartAsync();
        using var client = Client(seeded);

        using var response = await client.PostAsJsonAsync(CheckoutPath(seeded), Request(ValidDelivery()));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var checkout = (await response.Content.ReadFromJsonAsync<CheckoutResult>())!;
        client.DefaultRequestHeaders.Add("X-Order-Token", checkout.GuestOrderToken);
        var confirmation = await client.GetFromJsonAsync<StorefrontOrderDetailDto>(GuestPath(checkout.OrderId));
        confirmation!.Delivery!.Recipient.Should().Be(new DeliveryRecipientDto("Pat Customer", "+44 7700 900123"));
    }

    [Fact]
    public async Task Checkout_Should_RejectMissingBoxDelivery_BeforeInventoryOrderOrPaymentEffects()
    {
        var seeded = await SeedCartAsync(box: true);
        using var client = Client(seeded);

        using var response = await client.PostAsJsonAsync(CheckoutPath(seeded), Request(null));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Delivery details are required");
        await AssertNoCheckoutEffectsAsync(seeded);
    }

    [Theory]
    [InlineData("purchaser")]
    [InlineData("email")]
    [InlineData("phone")]
    [InlineData("address")]
    [InlineData("country")]
    [InlineData("recipient")]
    [InlineData("date")]
    [InlineData("wrong-weekday")]
    [InlineData("window")]
    public async Task Checkout_Should_RejectInvalidNestedDelivery_BeforeEffects(string invalid)
    {
        var seeded = await SeedCartAsync();
        using var client = Client(seeded);
        var valid = ValidDelivery();
        var delivery = invalid switch
        {
            "purchaser" => valid with { Purchaser = null! },
            "email" => valid with { Purchaser = valid.Purchaser with { Email = "invalid" } },
            "phone" => valid with { Purchaser = valid.Purchaser with { Phone = "phone-number" } },
            "address" => valid with { Address = null! },
            "country" => valid with { Address = valid.Address with { CountryCode = "UKK" } },
            "recipient" => valid with { Recipient = new("", "+44 7700 900123") },
            "date" => valid with { DeliveryDate = default },
            "wrong-weekday" => valid with { DeliveryDate = SelectedDate.AddDays(1) },
            "window" => valid with { WindowId = "morning" },
            _ => throw new InvalidOperationException(invalid)
        };

        using var response = await client.PostAsJsonAsync(CheckoutPath(seeded), Request(delivery));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertNoCheckoutEffectsAsync(seeded);
    }

    [Fact]
    public async Task Checkout_Should_HideAnUnauthorizedCart_BeforeDeliveryValidation()
    {
        var seeded = await SeedCartAsync();
        using var client = Client(seeded, includeCartToken: false);
        client.DefaultRequestHeaders.Add("X-Cart-Token", CartAccess.MintToken());

        using var response = await client.PostAsJsonAsync(CheckoutPath(seeded), Request(
            ValidDelivery() with { Purchaser = null!, Address = null!, DeliveryDate = default }));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await AssertNoCheckoutEffectsAsync(seeded);
    }

    [Fact]
    public async Task Checkout_Should_RequireActualPurchaserDetails_ForAnAnonymousNonshippingCart()
    {
        var seeded = await SeedCartAsync(calendar: false);
        using var client = Client(seeded);

        using var response = await client.PostAsJsonAsync(CheckoutPath(seeded), Request(null));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Purchaser contact details");
        await AssertNoCheckoutEffectsAsync(seeded);
    }

    [Fact]
    public async Task Checkout_Should_ReadDeliveryAndDiscountFromTheSavedDraft()
    {
        var seeded = await SeedCartAsync();
        using var client = Client(seeded);
        var delivery = ValidDelivery();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = seeded.TenantId;
            var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
            db.Discounts.Add(new Discount
            {
                TenantId = seeded.TenantId, Code = "SAVE10", Kind = DiscountKinds.Percentage, Value = 10m, IsActive = true
            });
            await db.SaveChangesAsync();
        }
        using var saved = await client.PutAsJsonAsync($"/commerce/carts/{seeded.CartId}/checkout-draft",
            new CartCheckoutDraftDto(delivery.Purchaser, delivery.Address, DeliveryDate: delivery.DeliveryDate,
                Notes: "Saved instructions", CreateAccount: true, DiscountCode: "SAVE10"));
        saved.StatusCode.Should().Be(HttpStatusCode.OK);
        var draft = (await saved.Content.ReadFromJsonAsync<CartCheckoutDraftResponse>())!;
        client.DefaultRequestHeaders.Remove("X-Cart-Version");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Cart-Version", draft.CartVersion).Should().BeTrue();

        using var preview = await client.PostAsJsonAsync($"/commerce/carts/{seeded.CartId}/discount/validate", new { code = "SAVE10" });
        preview.StatusCode.Should().Be(HttpStatusCode.OK);
        var quote = (await preview.Content.ReadFromJsonAsync<CartDiscountQuoteDto>())!;
        using var response = await client.PostAsJsonAsync(CheckoutPath(seeded), Request(null) with { ExpectedTotal = quote.Total });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var checkout = (await response.Content.ReadFromJsonAsync<CheckoutResult>())!;
        checkout.DiscountTotal.Should().Be(2m);
        checkout.Total.Should().Be(18m);
        client.DefaultRequestHeaders.Add("X-Order-Token", checkout.GuestOrderToken);
        var confirmation = await client.GetFromJsonAsync<StorefrontOrderDetailDto>(GuestPath(checkout.OrderId));
        var expected = new OrderDeliveryDto(delivery.Purchaser, delivery.Address, delivery.DeliveryDate,
            "Europe/London", new("Pat Customer", delivery.Purchaser.Phone), "Saved instructions");
        confirmation!.Delivery.Should().Be(expected);
        await AssertOneCheckoutAsync(seeded, checkout.OrderId, expected);
    }

    [Fact]
    public async Task Checkout_Should_UseExplicitDeliveryAsAWhole_InsteadOfCombiningItWithTheDraft()
    {
        var seeded = await SeedCartAsync();
        using var client = Client(seeded);
        using var saved = await client.PutAsJsonAsync($"/commerce/carts/{seeded.CartId}/checkout-draft",
            new CartCheckoutDraftDto(Purchaser: new("unfinished@", "Draft", "Buyer", "07"),
                Recipient: new("Draft recipient", "07"), Notes: "Old draft notes"));
        saved.StatusCode.Should().Be(HttpStatusCode.OK);
        var draft = (await saved.Content.ReadFromJsonAsync<CartCheckoutDraftResponse>())!;
        client.DefaultRequestHeaders.Remove("X-Cart-Version");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Cart-Version", draft.CartVersion);

        using var response = await client.PostAsJsonAsync(CheckoutPath(seeded), Request(ValidDelivery()));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var checkout = (await response.Content.ReadFromJsonAsync<CheckoutResult>())!;
        client.DefaultRequestHeaders.Add("X-Order-Token", checkout.GuestOrderToken);
        var confirmation = await client.GetFromJsonAsync<StorefrontOrderDetailDto>(GuestPath(checkout.OrderId));
        confirmation!.Delivery!.Purchaser.Should().Be(ValidDelivery().Purchaser);
        confirmation.Delivery.Recipient.Name.Should().Be("Pat Customer");
        confirmation.Delivery.Notes.Should().BeNull();
    }

    [Fact]
    public async Task CompletedCart_Should_ReturnItsOrderAndState_OnAnAttemptedDraftOverwrite()
    {
        var seeded = await SeedCartAsync();
        using var client = Client(seeded);
        using var response = await client.PostAsJsonAsync(CheckoutPath(seeded), Request(ValidDelivery()));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var checkout = (await response.Content.ReadFromJsonAsync<CheckoutResult>())!;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = seeded.TenantId;
            var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
            var cart = await db.Carts.SingleAsync(row => row.Id == seeded.CartId);
            cart.Status = CartStatuses.CheckedOut;
            await db.SaveChangesAsync();
        }

        using var write = await client.PutAsJsonAsync($"/commerce/carts/{seeded.CartId}/checkout-draft", new { notes = "A stale tab" });

        write.StatusCode.Should().Be(HttpStatusCode.Conflict);
        write.Headers.CacheControl!.NoStore.Should().BeTrue();
        var conflict = await write.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        conflict.GetProperty("code").GetString().Should().Be("commerce.cart_locked");
        conflict.GetProperty("cartId").GetGuid().Should().Be(seeded.CartId);
        conflict.GetProperty("status").GetString().Should().Be(CartStatuses.CheckedOut);
        conflict.GetProperty("orderId").GetGuid().Should().Be(checkout.OrderId);
        _factory.PaymentCalls.Count.Should().Be(seeded.PaymentCallsBefore + 1);
    }

    private static CheckoutDeliveryDetails ValidDelivery() => new(
        new("buyer@example.test", "Pat", "Customer", "+44 7700 900123"),
        new("12 Sample Street", null, "London", null, "SW1A 1AA", "GB"), SelectedDate);

    [Fact]
    public async Task PaymentRecovery_Should_RequireOwnershipAndCurrentVersion_AndReuseTheOrderAfterEditing()
    {
        var seeded = await SeedCartAsync();
        using var client = Client(seeded);
        using var checkoutResponse = await client.PostAsJsonAsync(CheckoutPath(seeded), Request(ValidDelivery()));
        checkoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var checkout = (await checkoutResponse.Content.ReadFromJsonAsync<CheckoutResult>())!;
        var paymentPath = $"/commerce/carts/{seeded.CartId}/payment";
        using var outsider = Client(seeded, includeCartToken: false);
        using var hidden = await outsider.GetAsync(paymentPath);
        hidden.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var stateResponse = await client.GetAsync(paymentPath);
        stateResponse.Headers.CacheControl!.NoStore.Should().BeTrue();
        var state = (await stateResponse.Content.ReadFromJsonAsync<CartPaymentStateDto>())!;
        state.CanEdit.Should().BeFalse();
        state.PaymentIntentId.Should().Be(checkout.PaymentIntentId);
        client.DefaultRequestHeaders.Remove("X-Cart-Version");
        using var missingVersion = await client.PostAsJsonAsync(paymentPath + "/recover", new { checkout.PaymentIntentId });
        missingVersion.StatusCode.Should().Be(HttpStatusCode.Conflict);
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Cart-Version", state.CartVersion);
        using var recoveredResponse = await client.PostAsJsonAsync(paymentPath + "/recover", new { checkout.PaymentIntentId });
        recoveredResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var recovered = (await recoveredResponse.Content.ReadFromJsonAsync<CartPaymentStateDto>())!;
        recovered.CanEdit.Should().BeTrue();
        recovered.OrderId.Should().Be(checkout.OrderId);
        recovered.CheckoutUrl.Should().BeNull();
        client.DefaultRequestHeaders.Remove("X-Cart-Version");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Cart-Version", recovered.CartVersion);
        using var nextResponse = await client.PostAsJsonAsync(CheckoutPath(seeded), Request(ValidDelivery() with { Notes = "Retry instructions" }));
        nextResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var next = (await nextResponse.Content.ReadFromJsonAsync<CheckoutResult>())!;
        next.OrderId.Should().Be(checkout.OrderId);
        next.PaymentIntentId.Should().NotBe(checkout.PaymentIntentId);
    }

    private static CheckoutRequest Request(CheckoutDeliveryDetails? delivery)
        => new("Stripe", "Card", null, null, null, null, delivery);

    private static string CheckoutPath(SeededCart seeded) => $"/commerce/carts/{seeded.CartId}/checkout";
    private static string GuestPath(Guid orderId) => $"/commerce/storefront/guest-orders/{orderId}";

    private HttpClient Client(SeededCart seeded, bool includeCartToken = true)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", seeded.TenantId.ToString());
        if (includeCartToken)
        {
            client.DefaultRequestHeaders.Add("X-Cart-Token", seeded.CartToken);
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Cart-Version", seeded.CartVersion).Should().BeTrue();
        }
        return client;
    }

    private async Task<SeededCart> SeedCartAsync(bool box = false, bool calendar = true)
    {
        var tenantId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var cartId = Guid.NewGuid();
        var token = CartAccess.MintToken();
        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId, Name = "Delivery checkout tenant", Environment = "Testing",
            DefaultCurrency = "GBP", SupportedCountriesJson = "[]", Status = TenantStatus.Active
        });
        db.Products.Add(new Product
        {
            Id = productId, TenantId = tenantId, Name = "Dish", Slug = "dish", Status = ProductStatuses.Active,
            Variants = [new ProductVariant { Id = variantId, TenantId = tenantId, ProductId = productId, Sku = "DISH", Name = "Dish" }]
        });
        db.InventoryLevels.Add(new InventoryLevel { TenantId = tenantId, ProductVariantId = variantId, OnHand = 10m });
        db.Carts.Add(new Cart
        {
            Id = cartId, TenantId = tenantId, AnonymousToken = token, Currency = "GBP",
            // The missing-delivery guard runs before box preparation; no synthetic box configuration is needed.
            BoxBundleProductId = box ? productId : null,
            Items = [new CartItem
            {
                TenantId = tenantId, CartId = cartId, ProductVariantId = variantId,
                Quantity = 2m, UnitPriceSnapshot = 10m, Sku = "DISH", NameSnapshot = "Dish"
            }]
        });
        if (calendar)
            db.FulfilmentCalendars.Add(new FulfilmentCalendar
            {
                TenantId = tenantId, Timezone = "Europe/London", DeliveryDaysJson = "[\"thursday\"]",
                CutoffLocalTime = new TimeOnly(12, 0), LeadDays = 7, IsActive = true
            });
        await db.SaveChangesAsync();
        return new SeededCart(tenantId, cartId, variantId, token, _factory.PaymentCalls.Count,
            Convert.ToBase64String(db.Carts.Local.Single(row => row.Id == cartId).RowVersion));
    }

    private async Task AssertNoCheckoutEffectsAsync(SeededCart seeded)
    {
        _factory.PaymentCalls.Count.Should().Be(seeded.PaymentCallsBefore);
        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = seeded.TenantId;
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        (await db.Carts.SingleAsync(row => row.Id == seeded.CartId)).OrderId.Should().BeNull();
        (await db.Set<Order>().CountAsync(row => row.TenantId == seeded.TenantId)).Should().Be(0);
        (await db.OrderDeliveryDetails.CountAsync(row => row.TenantId == seeded.TenantId)).Should().Be(0);
        (await db.OrderChargeSummaries.CountAsync(row => row.TenantId == seeded.TenantId)).Should().Be(0);
        (await db.InventoryReservations.CountAsync(row => row.TenantId == seeded.TenantId)).Should().Be(0);
        (await db.InventoryLevels.SingleAsync(row => row.ProductVariantId == seeded.VariantId)).Reserved.Should().Be(0m);
    }

    private async Task AssertOneCheckoutAsync(SeededCart seeded, Guid orderId, OrderDeliveryDto? delivery)
    {
        _factory.PaymentCalls.Count.Should().Be(seeded.PaymentCallsBefore + 1);
        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = seeded.TenantId;
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        (await db.Carts.SingleAsync(row => row.Id == seeded.CartId)).OrderId.Should().Be(orderId);
        (await db.Set<Order>().SingleAsync(row => row.TenantId == seeded.TenantId)).PayerPartyId.Should().NotBeNull();
        (await db.Carts.SingleAsync(row => row.Id == seeded.CartId)).BuyerPartyId.Should().BeNull();
        (await db.OrderChargeSummaries.SingleAsync(row => row.TenantId == seeded.TenantId)).OrderId.Should().Be(orderId);
        (await db.InventoryReservations.CountAsync(row => row.TenantId == seeded.TenantId)).Should().Be(1);
        (await db.InventoryLevels.SingleAsync(row => row.ProductVariantId == seeded.VariantId)).Reserved.Should().Be(2m);
        var snapshots = await db.OrderDeliveryDetails.Where(row => row.TenantId == seeded.TenantId).ToListAsync();
        if (delivery is null) snapshots.Should().BeEmpty();
        else
        {
            snapshots.Should().ContainSingle();
            snapshots[0].OrderId.Should().Be(orderId);
            OrderDeliveryMapper.Map(snapshots[0]).Should().Be(delivery);
        }
    }

    private sealed record SeededCart(Guid TenantId, Guid CartId, Guid VariantId, string CartToken, int PaymentCallsBefore,
        string CartVersion);

    public sealed class CheckoutDeliveryFactory : CustomWebApplicationFactory
    {
        public ConcurrentQueue<CreateGuestPaymentIntentForOrderCommand> PaymentCalls { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPaymentInitiator>();
                services.AddSingleton<IPaymentInitiator>(new TestPaymentInitiator(PaymentCalls));
                services.RemoveAll<IClock>();
                services.AddSingleton<IClock>(new TestClock());
                // These tests own delivery snapshots; postcode enforcement has a separate real-service API fixture.
                services.RemoveAll<IDeliveryCoverageService>();
                services.AddSingleton<IDeliveryCoverageService>(new AllowedTestCoverage());
            });
        }
    }

    private sealed class AllowedTestCoverage : IDeliveryCoverageService
    {
        public Task<DeliveryCoverageDto> CheckAsync(string? postcode, CancellationToken cancellationToken = default)
            => Task.FromResult(new DeliveryCoverageDto(DeliveryCoverageStatuses.Serves, postcode));
        public Task<DeliveryCoverageConfigDto?> GetConfigurationAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<DeliveryCoverageConfigDto> UpdateConfigurationAsync(DeliveryCoverageConfigDto configuration,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow => new(2026, 10, 8, 9, 0, 0, DateTimeKind.Utc);
    }

    private sealed class TestPaymentInitiator(ConcurrentQueue<CreateGuestPaymentIntentForOrderCommand> calls) : IPaymentInitiator
    {
        private readonly ConcurrentDictionary<Guid, PaymentIntentStateRef> _states = new();
        public Task<PaymentIntentRef> CreateGuestIntentForOrderAsync(CreateGuestPaymentIntentForOrderCommand command,
            CancellationToken cancellationToken = default)
        {
            calls.Enqueue(command);
            var id = command.PaymentIntentId!.Value;
            _states[id] = new(id, command.OrderId, command.Amount, command.Currency, "RequiresAction", false,
                "https://example.test/private-payment-url");
            return Task.FromResult(new PaymentIntentRef(id, "RequiresAction",
                "private-payment-secret", "https://example.test/private-payment-url"));
        }
        public Task<PaymentIntentStateRef?> GetStateAsync(Guid paymentIntentId, CancellationToken cancellationToken = default)
            => Task.FromResult(_states.GetValueOrDefault(paymentIntentId));
        public Task<PaymentIntentStateRef> ExpireAsync(Guid paymentIntentId, CancellationToken cancellationToken = default)
        {
            var state = _states[paymentIntentId] with { Status = "Cancelled", CanNoLongerPay = true, CheckoutUrl = null };
            _states[paymentIntentId] = state;
            return Task.FromResult(state);
        }
    }
}
