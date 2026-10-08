using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Services.Checkout;
using Aonik.Infrastructure.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Api.Tests;

public partial class CommerceBoxCartEndpointTests
{
    private const string CurrentBoxRoute = "/commerce/carts/box/current";

    [Fact]
    public async Task CurrentBox_Should_RequireAuthentication_AndNeverCreateACartOnAMiss()
    {
        var tenantId = Guid.NewGuid();
        await SeedBoxWorldAsync(tenantId);
        var noProfile = await _factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithTenant(tenantId)
            .WithRoles("PersonalUser"));
        var customer = await CustomerAsync(tenantId);

        var anonymousResponse = await Client(tenantId).GetAsync(CurrentBoxRoute);
        anonymousResponse.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
        AssertNoStore(anonymousResponse);
        foreach (var client in new[] { noProfile, customer.Client })
        {
            var response = await client.GetAsync(CurrentBoxRoute);
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            AssertNoStore(response);
        }

        (await ReadCartsAsync(tenantId)).Should().BeEmpty();
    }

    [Fact]
    public async Task CurrentBox_Should_ReturnAnEmptyActiveBox_OnlyForTheAuthenticatedCustomer()
    {
        var tenantId = Guid.NewGuid();
        var (bundleId, _) = await SeedBoxWorldAsync(tenantId);
        var customer = await CustomerAsync(tenantId);
        var otherCustomer = await CustomerAsync(tenantId);
        var created = await CreateBoxAsync(customer.Client, bundleId);

        var response = await customer.Client.GetAsync(CurrentBoxRoute + $"?buyerPartyId={otherCustomer.PartyId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertNoStore(response);
        var current = await response.Content.ReadFromJsonAsync<BoxCartDto>();
        current!.Box.CartId.Should().Be(created.Box.CartId);
        current.Box.Lines.Should().BeEmpty();
        current.Quote.UnitsSelected.Should().Be(0);
        current.CartVersion.Should().NotBeNull();
        current.CartToken.Should().BeNull();

        var otherTenantId = Guid.NewGuid();
        await SeedBoxWorldAsync(otherTenantId);
        var otherTenantCustomer = await CustomerAsync(otherTenantId);
        foreach (var client in new[] { otherCustomer.Client, otherTenantCustomer.Client })
        {
            var inaccessible = await client.GetAsync(CurrentBoxRoute + $"?partyId={customer.PartyId}&buyerPartyId={customer.PartyId}");
            inaccessible.StatusCode.Should().Be(HttpStatusCode.NotFound);
            AssertNoStore(inaccessible);
        }
        (await ReadCartsAsync(tenantId)).Should().ContainSingle();
    }

    [Fact]
    public async Task BoxCreate_Should_ReturnExistingBoxConflict_WithoutCreatingASecondBox()
    {
        var tenantId = Guid.NewGuid();
        var (bundleId, _) = await SeedBoxWorldAsync(tenantId);
        var customer = await CustomerAsync(tenantId);
        var first = await CreateBoxAsync(customer.Client, bundleId);

        var response = await customer.Client.PostAsJsonAsync("/commerce/carts/box", new { bundleProductId = bundleId, size = 12 });
        var conflict = await ReadConflictAsync(response, ActiveBoxConflictException.Existing);

        conflict.GetProperty("guest").ValueKind.Should().Be(JsonValueKind.Null);
        var saved = conflict.GetProperty("savedCandidates").EnumerateArray().Should().ContainSingle().Which;
        saved.GetProperty("cartId").GetGuid().Should().Be(first.Box.CartId);
        saved.GetProperty("cartVersion").GetString().Should().Be(first.CartVersion);
        (await ReadCartsAsync(tenantId)).Should().ContainSingle();
    }

    [Theory]
    [InlineData(CartAdoptionDecisions.KeepGuest)]
    [InlineData(CartAdoptionDecisions.UseSaved)]
    public async Task AdoptBox_Should_RequireAFreshChoice_KeepOnlyTheChosenContents_AndRetireGuestAccess(string decision)
    {
        var tenantId = Guid.NewGuid();
        var (bundleId, variantId) = await SeedBoxWorldAsync(tenantId);
        var customer = await CustomerAsync(tenantId);
        var saved = await CreateBoxAsync(customer.Client, bundleId, variantId, 2);
        var anonymous = Client(tenantId);
        var guest = await CreateBoxAsync(anonymous, bundleId, variantId, 1, size: 12);
        anonymous.DefaultRequestHeaders.Add("X-Cart-Token", guest.CartToken);
        customer.Client.DefaultRequestHeaders.Add("X-Cart-Token", guest.CartToken);
        var adoptRoute = $"/commerce/carts/{guest.Box.CartId}/adopt";

        var conflictResponse = await customer.Client.PostAsync(adoptRoute, null);
        var conflict = await ReadConflictAsync(conflictResponse, ActiveBoxConflictException.ChoiceRequired);
        conflict.GetProperty("guest").GetProperty("cartId").GetGuid().Should().Be(guest.Box.CartId);
        conflict.GetProperty("savedCandidates")[0].GetProperty("cartId").GetGuid().Should().Be(saved.Box.CartId);
        conflict.GetProperty("hasMore").GetBoolean().Should().BeFalse();
        conflict.GetRawText().Should().NotContain(guest.CartToken!).And.NotContain(customer.PartyId.ToString());
        var choice = new AdoptCartChoice(decision, saved.Box.CartId,
            conflict.GetProperty("savedCandidates")[0].GetProperty("cartVersion").GetString()!,
            conflict.GetProperty("guest").GetProperty("cartVersion").GetString()!);

        var stale = await customer.Client.PostAsJsonAsync(adoptRoute, choice with { ExpectedGuestCartVersion = "AQ==" });
        await ReadConflictAsync(stale, ActiveBoxConflictException.StaleChoice);
        (await customer.Client.GetFromJsonAsync<BoxCartDto>(CurrentBoxRoute))!.Box.CartId.Should().Be(saved.Box.CartId);
        (await anonymous.GetAsync($"/commerce/carts/{guest.Box.CartId}")).StatusCode.Should().Be(HttpStatusCode.OK);

        var selectedResponse = await customer.Client.PostAsJsonAsync(adoptRoute, new
        {
            choice.Decision, choice.ExpectedSavedCartId, choice.ExpectedSavedCartVersion, choice.ExpectedGuestCartVersion,
            buyerPartyId = Guid.NewGuid(), partyId = Guid.NewGuid()
        });
        selectedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertNoStore(selectedResponse);
        var selected = await selectedResponse.Content.ReadFromJsonAsync<CartDto>();
        var selectedId = decision == CartAdoptionDecisions.KeepGuest ? guest.Box.CartId : saved.Box.CartId;
        selected!.Id.Should().Be(selectedId);
        selected.BuyerPartyId.Should().Be(customer.PartyId);
        selected.AnonymousToken.Should().BeNull();
        var current = await customer.Client.GetFromJsonAsync<BoxCartDto>(CurrentBoxRoute);
        current!.Box.CartId.Should().Be(selectedId);
        current.Box.Lines.Should().ContainSingle().Which.Quantity.Should()
            .Be(decision == CartAdoptionDecisions.KeepGuest ? 1 : 2);
        current.Box.Size.Should().Be(decision == CartAdoptionDecisions.KeepGuest ? 12 : 6);
        current.CartToken.Should().BeNull();

        var retiredTokenResponse = await anonymous.GetAsync($"/commerce/carts/{guest.Box.CartId}");
        retiredTokenResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        AssertNoStore(retiredTokenResponse);
        var rows = await ReadCartsAsync(tenantId);
        rows.Should().HaveCount(2);
        rows.Single(x => x.Id == selectedId).Status.Should().Be(CartStatuses.Open);
        rows.Single(x => x.Id != selectedId).Status.Should().Be(CartStatuses.Abandoned);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Adopt_Should_PreserveEmptyBodyCompatibility_ForGenericAndBoxCarts(bool boxCart)
    {
        var tenantId = Guid.NewGuid();
        var (bundleId, _) = await SeedBoxWorldAsync(tenantId);
        var customer = await CustomerAsync(tenantId);
        var anonymous = Client(tenantId);
        Guid cartId;
        string token;
        if (boxCart)
        {
            var guest = await CreateBoxAsync(anonymous, bundleId);
            cartId = guest.Box.CartId;
            token = guest.CartToken!;
        }
        else
        {
            // A generic cart remains independent of the customer's box selection.
            await CreateBoxAsync(customer.Client, bundleId);
            var created = await anonymous.PostAsJsonAsync("/commerce/carts", new { currency = "GBP" });
            created.StatusCode.Should().Be(HttpStatusCode.OK);
            AssertNoStore(created);
            var guest = await created.Content.ReadFromJsonAsync<CartDto>();
            cartId = guest!.Id;
            token = guest.AnonymousToken!;
        }
        customer.Client.DefaultRequestHeaders.Add("X-Cart-Token", token);
        var adopted = await customer.Client.PostAsync($"/commerce/carts/{cartId}/adopt", null);

        adopted.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertNoStore(adopted);
        var cart = await adopted.Content.ReadFromJsonAsync<CartDto>();
        cart!.Id.Should().Be(cartId);
        cart.BuyerPartyId.Should().Be(customer.PartyId);
        cart.AnonymousToken.Should().BeNull();
        anonymous.DefaultRequestHeaders.Add("X-Cart-Token", token);
        (await anonymous.GetAsync($"/commerce/carts/{cartId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Adopt_Should_ReturnPrivateErrors_WithoutLeakingSavedBoxesToAnUnprovenGuest()
    {
        var tenantId = Guid.NewGuid();
        var (bundleId, _) = await SeedBoxWorldAsync(tenantId);
        var customer = await CustomerAsync(tenantId);
        var saved = await CreateBoxAsync(customer.Client, bundleId);
        var guest = await CreateBoxAsync(Client(tenantId), bundleId);
        var route = $"/commerce/carts/{guest.Box.CartId}/adopt";

        var unproven = await customer.Client.PostAsync(route, null);
        unproven.StatusCode.Should().Be(HttpStatusCode.NotFound);
        AssertNoStore(unproven);
        (await unproven.Content.ReadAsStringAsync()).Should().NotContain(saved.Box.CartId.ToString());
        var anonymous = await Client(tenantId).PostAsync(route, null);
        anonymous.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
        AssertNoStore(anonymous);

        customer.Client.DefaultRequestHeaders.Add("X-Cart-Token", guest.CartToken);
        var malformed = await customer.Client.PostAsync(route, new StringContent("{broken", Encoding.UTF8, "application/json"));
        malformed.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        AssertNoStore(malformed);
        (await malformed.Content.ReadAsStringAsync()).Should().NotContain("broken");
        (await ReadCartsAsync(tenantId)).Should().OnlyContain(x => x.Status == CartStatuses.Open);
    }

    [Fact]
    public async Task CurrentBox_Should_ReportLegacyMultipleActiveBoxes_InsteadOfChoosingSilently()
    {
        var tenantId = Guid.NewGuid();
        var (bundleId, _) = await SeedBoxWorldAsync(tenantId);
        var customer = await CustomerAsync(tenantId);
        var first = await CreateBoxAsync(customer.Client, bundleId);
        var extraId = Guid.NewGuid();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;
            var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
            db.Carts.Add(new Cart
            {
                Id = extraId, TenantId = tenantId, BuyerPartyId = customer.PartyId, Currency = "GBP",
                BoxBundleProductId = bundleId, BoxSize = 12, Status = CartStatuses.Open
            });
            await db.SaveChangesAsync();
        }

        var response = await customer.Client.GetAsync(CurrentBoxRoute);
        var conflict = await ReadConflictAsync(response, ActiveBoxConflictException.Multiple);

        conflict.GetProperty("guest").ValueKind.Should().Be(JsonValueKind.Null);
        conflict.GetProperty("savedCandidates").EnumerateArray().Select(x => x.GetProperty("cartId").GetGuid())
            .Should().BeEquivalentTo(new[] { first.Box.CartId, extraId });
        (await ReadCartsAsync(tenantId)).Should().HaveCount(2).And.OnlyContain(x => x.Status == CartStatuses.Open);
    }

    private async Task<(HttpClient Client, Guid PartyId)> CustomerAsync(Guid tenantId)
    {
        var options = TestAuthOptions.Create().WithTenant(tenantId).WithRoles("PersonalUser").WithPermissions("Customers.Read");
        var client = await _factory.CreateAuthenticatedClientAsync(options);
        var partyId = await WorkspaceTestSeeding.SeedPartyAsync(_factory, tenantId, options.UserId, "Box customer");
        return (client, partyId);
    }

    private static async Task<BoxCartDto> CreateBoxAsync(HttpClient client, Guid bundleId,
        Guid? variantId = null, int quantity = 0, int size = 6)
    {
        var response = await client.PostAsJsonAsync("/commerce/carts/box", new
        {
            bundleProductId = bundleId, size,
            firstLine = variantId.HasValue ? new { productVariantId = variantId.Value, quantity } : null
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertNoStore(response);
        return (await response.Content.ReadFromJsonAsync<BoxCartDto>())!;
    }

    private async Task<List<Cart>> ReadCartsAsync(Guid tenantId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;
        return await scope.ServiceProvider.GetRequiredService<AonikDbContext>().Carts.AsNoTracking()
            .Where(x => x.TenantId == tenantId).ToListAsync();
    }

    private static async Task<JsonElement> ReadConflictAsync(HttpResponseMessage response, string code)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        AssertNoStore(response);
        var conflict = await response.Content.ReadFromJsonAsync<JsonElement>();
        conflict.EnumerateObject().Select(x => x.Name).Should()
            .BeEquivalentTo("code", "message", "guest", "savedCandidates", "hasMore");
        conflict.GetProperty("code").GetString().Should().Be(code);
        return conflict;
    }

    private static void AssertNoStore(HttpResponseMessage response)
        => response.Headers.CacheControl!.NoStore.Should().BeTrue();
}
