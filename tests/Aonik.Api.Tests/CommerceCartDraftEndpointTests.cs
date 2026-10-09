using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Cart;
using Aonik.Finance.Entities.Orders;
using Aonik.Infrastructure.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Settings;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Api.Tests;

public partial class CommerceBoxCartEndpointTests
{
    [Fact]
    public async Task Draft_Should_RestoreIncompleteFieldsAndGiftIntent_WithoutChangingBoxContents()
    {
        var tenantId = Guid.NewGuid();
        var (bundleId, variantId) = await SeedBoxWorldAsync(tenantId);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;
            await scope.ServiceProvider.GetRequiredService<ITenantSettingStore>().SetTenantValueAsync(
                CommerceSettingNames.StorefrontGreetingCard,
                """{"isEnabled":true,"currency":"GBP","amount":3}""", tenantId);
        }
        using var client = Client(tenantId);
        var box = await CreateBoxAsync(client, bundleId, variantId, 2);
        client.DefaultRequestHeaders.Add("X-Cart-Token", box.CartToken);
        UseCartVersion(client, box.CartVersion);

        using var saved = await client.PutAsJsonAsync(DraftPath(box.Box.CartId), new
        {
            purchaser = new { email = " unfinished@ ", firstName = " Ada ", lastName = "", phone = "07" },
            address = new { line1 = " 12 Kitchen Road ", city = "", postcode = " sw1a ", countryCode = " gb " },
            recipient = new { name = " Sam ", phone = "" },
            gift = new { giftIntent = true, hidePrices = true, includeGreetingCard = true, greetingCardMessage = " Happy birthday! " },
            notes = " Ring the bell.\nUse the side door. ",
            createAccount = true,
            discountCode = " SAVE10 ",
            reservationId = Guid.NewGuid(),
            paymentClientSecret = "never-store-this-secret",
            password = "never-store-this-password"
        });

        saved.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertNoStore(saved);
        var result = (await saved.Content.ReadFromJsonAsync<CartCheckoutDraftResponse>())!;
        result.CartId.Should().Be(box.Box.CartId);
        result.Status.Should().Be(CartStatuses.Open);
        result.OrderId.Should().BeNull();
        result.Draft.Should().NotBeNull();
        result.Draft!.Purchaser.Should().Be(new CheckoutContactDto("unfinished@", "Ada", "", "07"));
        result.Draft.Address.Should().Be(new DeliveryAddressDto("12 Kitchen Road", null, "", null, "SW1A", "GB"));
        result.Draft.Gift.Should().Be(new CartGiftDraftDto(true, true, true, "Happy birthday!"));
        result.Draft.CreateAccount.Should().BeTrue();
        result.Draft.DiscountCode.Should().Be("SAVE10");
        result.Draft.Notes.Should().Be("Ring the bell.\nUse the side door.");

        using var resumedClient = Client(tenantId);
        resumedClient.DefaultRequestHeaders.Add("X-Cart-Token", box.CartToken);
        using var resumed = await resumedClient.GetAsync($"/commerce/carts/{box.Box.CartId}");
        resumed.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertNoStore(resumed);
        var restored = (await resumed.Content.ReadFromJsonAsync<BoxCartDto>())!;
        restored.CheckoutDraft.Should().Be(result.Draft);
        restored.Box.Lines.Should().ContainSingle().Which.Quantity.Should().Be(2);
        restored.Quote.Total.Should().Be(box.Quote.Total + 3m, "the selected greeting card uses the configured tenant price");
        restored.CartToken.Should().BeNull();
        (await resumed.Content.ReadAsStringAsync()).Should().NotContain("never-store-this")
            .And.NotContain("reservationId").And.NotContain("password");
    }

    [Fact]
    public async Task CurrentBox_Should_AllowPrivateDraftRecovery_WhenSelectedGreetingCardBecomesUnavailable()
    {
        var tenantId = Guid.NewGuid();
        var (bundleId, variantId) = await SeedBoxWorldAsync(tenantId);
        var customer = await CustomerAsync(tenantId);
        using var client = customer.Client;
        var box = await CreateBoxAsync(client, bundleId, variantId, 2);
        UseCartVersion(client, box.CartVersion);
        await SetOfferAsync(true);
        var draft = new CartCheckoutDraftDto(
            Purchaser: new("buyer@example.test", "Pat", "Customer", "07"),
            Notes: "Keep these delivery instructions",
            Gift: new(true, IncludeGreetingCard: true, GreetingCardMessage: "Enjoy!"));
        using var saved = await client.PutAsJsonAsync(DraftPath(box.Box.CartId), draft);
        saved.StatusCode.Should().Be(HttpStatusCode.OK);
        var savedDraft = (await saved.Content.ReadFromJsonAsync<CartCheckoutDraftResponse>())!;
        await SetOfferAsync(false);
        client.DefaultRequestHeaders.Remove("X-Cart-Version");

        using var current = await client.GetAsync(CurrentBoxRoute);

        current.StatusCode.Should().Be(HttpStatusCode.Conflict);
        AssertNoStore(current);
        var conflict = await current.Content.ReadFromJsonAsync<JsonElement>();
        conflict.GetProperty("code").GetString().Should().Be("commerce.greeting_card_unavailable");
        var recoveredId = conflict.GetProperty("cartId").GetGuid();
        recoveredId.Should().Be(box.Box.CartId);
        conflict.GetProperty("cartVersion").GetString().Should().Be(savedDraft.CartVersion);
        using var recovered = await client.GetAsync(DraftPath(recoveredId));
        recovered.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertNoStore(recovered);
        var recoverable = (await recovered.Content.ReadFromJsonAsync<CartCheckoutDraftResponse>())!;
        recoverable.Draft.Should().Be(savedDraft.Draft);
        recoverable.CartVersion.Should().Be(savedDraft.CartVersion);

        var otherCustomer = await CustomerAsync(tenantId);
        using var otherClient = otherCustomer.Client;
        using var anonymous = Client(tenantId);
        foreach (var unauthorized in new[] { anonymous, otherClient })
        {
            using var denied = await unauthorized.GetAsync(DraftPath(recoveredId));
            denied.StatusCode.Should().Be(HttpStatusCode.NotFound);
            AssertNoStore(denied);
            (await denied.Content.ReadAsStringAsync()).Should().NotContain("buyer@example.test")
                .And.NotContain("Keep these delivery instructions");
        }

        UseCartVersion(client, recoverable.CartVersion);
        using var removed = await client.PutAsJsonAsync(DraftPath(recoveredId),
            recoverable.Draft! with { Gift = recoverable.Draft.Gift! with { IncludeGreetingCard = false } });
        removed.StatusCode.Should().Be(HttpStatusCode.OK);
        var restored = (await client.GetFromJsonAsync<BoxCartDto>(CurrentBoxRoute))!;
        restored.Box.Lines.Should().BeEquivalentTo(box.Box.Lines, options => options
            .Using<JsonElement>(context => JsonElement.DeepEquals(context.Subject, context.Expectation).Should().BeTrue())
            .WhenTypeIs<JsonElement>());
        restored.CheckoutDraft!.Purchaser.Should().Be(draft.Purchaser);
        restored.CheckoutDraft.Notes.Should().Be(draft.Notes);
        restored.CheckoutDraft.Gift!.GiftIntent.Should().BeTrue();
        restored.CheckoutDraft.Gift.IncludeGreetingCard.Should().BeFalse();
        restored.Quote.Total.Should().Be(box.Quote.Total);

        async Task SetOfferAsync(bool enabled)
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;
            await scope.ServiceProvider.GetRequiredService<ITenantSettingStore>().SetTenantValueAsync(
                CommerceSettingNames.StorefrontGreetingCard,
                JsonSerializer.Serialize(new { isEnabled = enabled, currency = "GBP", amount = 3m }), tenantId);
        }
    }

    [Fact]
    public async Task Draft_Should_ReplaceAndClearSections_AndRestoreForTheSignedInCustomer()
    {
        var tenantId = Guid.NewGuid();
        var (bundleId, _) = await SeedBoxWorldAsync(tenantId);
        var customer = await CustomerAsync(tenantId);
        using var client = customer.Client;
        var box = await CreateBoxAsync(client, bundleId);
        UseCartVersion(client, box.CartVersion);
        using var first = await client.PutAsJsonAsync(DraftPath(box.Box.CartId), new CartCheckoutDraftDto(
            Purchaser: new("buyer@example.test", "Pat", "Customer", "07"), Notes: "Remember me", CreateAccount: true));
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstDraft = (await first.Content.ReadFromJsonAsync<CartCheckoutDraftResponse>())!;
        UseCartVersion(client, firstDraft.CartVersion);

        using var replacement = await client.PutAsJsonAsync(DraftPath(box.Box.CartId), new { notes = "Only this remains" });

        replacement.StatusCode.Should().Be(HttpStatusCode.OK);
        var replaced = (await replacement.Content.ReadFromJsonAsync<CartCheckoutDraftResponse>())!;
        replaced.Draft.Should().Be(new CartCheckoutDraftDto(Notes: "Only this remains"));
        var current = await client.GetFromJsonAsync<BoxCartDto>(CurrentBoxRoute);
        current!.CheckoutDraft.Should().Be(replaced.Draft);
        UseCartVersion(client, current.CartVersion);
        using var clear = await client.PutAsJsonAsync(DraftPath(box.Box.CartId), new { purchaser = (object?)null, notes = (string?)null });
        clear.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertNoStore(clear);
        (await clear.Content.ReadFromJsonAsync<CartCheckoutDraftResponse>())!.Draft.Should().BeNull();
        (await client.GetFromJsonAsync<BoxCartDto>(CurrentBoxRoute))!.CheckoutDraft.Should().BeNull();
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("malformed")]
    [InlineData("repeated")]
    [InlineData("overlong")]
    [InlineData("whitespace")]
    [InlineData("stale")]
    public async Task EveryCartWrite_Should_RequireOneCurrentVersion_BeforeEffects(string versionCase)
    {
        var tenantId = Guid.NewGuid();
        var (bundleId, variantId) = await SeedBoxWorldAsync(tenantId);
        using var boxClient = Client(tenantId);
        var box = await CreateBoxAsync(boxClient, bundleId, variantId, 6);
        boxClient.DefaultRequestHeaders.Add("X-Cart-Token", box.CartToken);
        using var genericClient = Client(tenantId);
        using var create = await genericClient.PostAsJsonAsync("/commerce/carts", new { currency = "GBP" });
        var generic = (await create.Content.ReadFromJsonAsync<CartDto>())!;
        genericClient.DefaultRequestHeaders.Add("X-Cart-Token", generic.AnonymousToken);

        foreach (var route in BoxWriteRoutes)
        {
            using var request = BoxWriteRequest(route, box, variantId);
            AddVersionHeader(request, versionCase, box.CartVersion);
            using var response = await boxClient.SendAsync(request);
            await AssertCartConflictAsync(response, box.Box.CartId, box.CartVersion);
        }
        foreach (var route in GenericWriteRoutes)
        {
            using var request = GenericWriteRequest(route, generic.Id, variantId, bundleId);
            AddVersionHeader(request, versionCase, generic.CartVersion);
            using var response = await genericClient.SendAsync(request);
            await AssertCartConflictAsync(response, generic.Id, generic.CartVersion);
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        (await db.Carts.Where(row => row.TenantId == tenantId).ToListAsync())
            .Should().OnlyContain(row => row.OrderId == null && row.CheckoutDraftJson == null && row.Status == CartStatuses.Open);
        (await db.CartItems.Where(row => row.TenantId == tenantId).ToListAsync()).Should().ContainSingle().Which.Quantity.Should().Be(6m);
        (await db.InventoryReservations.CountAsync(row => row.TenantId == tenantId)).Should().Be(0);
        (await db.Set<Order>().CountAsync(row => row.TenantId == tenantId)).Should().Be(0);
    }

    [Fact]
    public async Task EveryCartWrite_Should_AuthorizeBeforeReturningVersionOrDraftState()
    {
        var tenantId = Guid.NewGuid();
        var (bundleId, variantId) = await SeedBoxWorldAsync(tenantId);
        using var creator = Client(tenantId);
        var box = await CreateBoxAsync(creator, bundleId, variantId, 6);
        using var create = await creator.PostAsJsonAsync("/commerce/carts", new { currency = "GBP" });
        var generic = (await create.Content.ReadFromJsonAsync<CartDto>())!;
        using var intruder = Client(tenantId);
        intruder.DefaultRequestHeaders.Add("X-Cart-Token", "not-the-cart-token");

        foreach (var request in BoxWriteRoutes.Select(route => BoxWriteRequest(route, box, variantId))
                     .Concat(GenericWriteRoutes.Select(route => GenericWriteRequest(route, generic.Id, variantId, bundleId))))
        {
            using (request)
            using (var response = await intruder.SendAsync(request))
            {
                response.StatusCode.Should().Be(HttpStatusCode.NotFound, request.RequestUri!.ToString());
                AssertNoStore(response);
                (await response.Content.ReadAsStringAsync()).Should().NotContain("cartVersion").And.NotContain("checkoutDraft");
            }
        }
    }

    [Fact]
    public async Task ReadsAndQuote_Should_IgnoreInvalidVersionHeaders_AndPreservePrivateResponses()
    {
        var tenantId = Guid.NewGuid();
        var (bundleId, _) = await SeedBoxWorldAsync(tenantId);
        using var client = Client(tenantId);
        var box = await CreateBoxAsync(client, bundleId);
        client.DefaultRequestHeaders.Add("X-Cart-Token", box.CartToken);
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Cart-Version", ["invalid", "invalid"]);

        using var read = await client.GetAsync($"/commerce/carts/{box.Box.CartId}");
        using var quote = await client.PostAsync($"/commerce/carts/{box.Box.CartId}/quote", null);

        read.StatusCode.Should().Be(HttpStatusCode.OK);
        quote.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertNoStore(read);
        AssertNoStore(quote);
    }

    [Fact]
    public async Task Draft_Should_RejectBoundedInput_OnlyAfterOwnershipAndVersionChecks()
    {
        var tenantId = Guid.NewGuid();
        var (bundleId, _) = await SeedBoxWorldAsync(tenantId);
        using var client = Client(tenantId);
        var box = await CreateBoxAsync(client, bundleId);
        client.DefaultRequestHeaders.Add("X-Cart-Token", box.CartToken);
        UseCartVersion(client, box.CartVersion);

        foreach (var notes in new[] { new string('x', 1001), "invalid\u0000notes" })
        {
            using var response = await client.PutAsJsonAsync(DraftPath(box.Box.CartId), new { notes });
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            AssertNoStore(response);
        }
        UseCartVersion(client, "AQIDBAUGBwg=");
        using var stale = await client.PutAsJsonAsync(DraftPath(box.Box.CartId), new { notes = new string('x', 1001) });
        await AssertCartConflictAsync(stale, box.Box.CartId, box.CartVersion);
        (await client.GetFromJsonAsync<BoxCartDto>($"/commerce/carts/{box.Box.CartId}"))!.CheckoutDraft.Should().BeNull();
    }

    private static readonly string[] BoxWriteRoutes = ["size", "add-line", "add-extra", "update-line", "delete-line", "continue", "draft", "checkout"];
    private static readonly string[] GenericWriteRoutes = ["add-item", "add-bundle", "delete-item", "draft", "checkout"];

    private static string DraftPath(Guid cartId) => $"/commerce/carts/{cartId}/checkout-draft";

    private static HttpRequestMessage BoxWriteRequest(string route, BoxCartDto box, Guid variantId)
    {
        var path = $"/commerce/carts/{box.Box.CartId}";
        return route switch
        {
            "size" => JsonRequest(HttpMethod.Patch, path + "/size", new { size = 12 }),
            "add-line" => JsonRequest(HttpMethod.Post, path + "/lines", new { productVariantId = variantId, quantity = 1 }),
            "add-extra" => JsonRequest(HttpMethod.Post, path + "/extras", new { productVariantId = variantId, quantity = 1 }),
            "update-line" => JsonRequest(HttpMethod.Patch, path + $"/lines/{box.Box.Lines[0].LineId}", new { quantity = 5 }),
            "delete-line" => new(HttpMethod.Delete, path + $"/lines/{box.Box.Lines[0].LineId}"),
            "continue" => new(HttpMethod.Post, path + "/continue"),
            "draft" => JsonRequest(HttpMethod.Put, DraftPath(box.Box.CartId), new { notes = "New draft" }),
            "checkout" => JsonRequest(HttpMethod.Post, path + "/checkout", new { provider = "Test", paymentMethodType = "Card" }),
            _ => throw new InvalidOperationException(route)
        };
    }

    private static HttpRequestMessage GenericWriteRequest(string route, Guid cartId, Guid variantId, Guid bundleId)
    {
        var path = $"/commerce/carts/{cartId}";
        return route switch
        {
            "add-item" => JsonRequest(HttpMethod.Post, path + "/items", new { productVariantId = variantId, quantity = 1 }),
            "add-bundle" => JsonRequest(HttpMethod.Post, path + "/bundles", new
            {
                bundleProductId = bundleId,
                selection = new[] { new { bundleSlotId = Guid.NewGuid(), productVariantId = variantId, quantity = 1 } }
            }),
            "delete-item" => new(HttpMethod.Delete, path + $"/items/{Guid.NewGuid()}"),
            "draft" => JsonRequest(HttpMethod.Put, DraftPath(cartId), new { notes = "New draft" }),
            "checkout" => JsonRequest(HttpMethod.Post, path + "/checkout", new { provider = "Test", paymentMethodType = "Card" }),
            _ => throw new InvalidOperationException(route)
        };
    }

    private static HttpRequestMessage JsonRequest<T>(HttpMethod method, string path, T body)
        => new(method, path) { Content = JsonContent.Create(body) };

    private static void AddVersionHeader(HttpRequestMessage request, string versionCase, string currentVersion)
    {
        if (versionCase == "missing") return;
        if (versionCase == "repeated")
            request.Headers.TryAddWithoutValidation("X-Cart-Version", [currentVersion, currentVersion]).Should().BeTrue();
        else
            request.Headers.TryAddWithoutValidation("X-Cart-Version", versionCase switch
            {
                "malformed" => "not-base64",
                "overlong" => new string('A', 65),
                "whitespace" => " ",
                "stale" => "AQIDBAUGBwg=",
                _ => throw new InvalidOperationException(versionCase)
            }).Should().BeTrue();
    }

    private static async Task AssertCartConflictAsync(HttpResponseMessage response, Guid cartId, string version)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Conflict, response.RequestMessage!.RequestUri!.ToString());
        AssertNoStore(response);
        var conflict = await response.Content.ReadFromJsonAsync<JsonElement>();
        conflict.GetProperty("code").GetString().Should().Be("commerce.cart_conflict");
        conflict.GetProperty("cartId").GetGuid().Should().Be(cartId);
        conflict.GetProperty("cartVersion").GetString().Should().Be(version);
        conflict.GetProperty("status").GetString().Should().Be(CartStatuses.Open);
        conflict.GetProperty("orderId").ValueKind.Should().Be(JsonValueKind.Null);
    }
}
