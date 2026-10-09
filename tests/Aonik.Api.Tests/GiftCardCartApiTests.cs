using System.Net;
using System.Net.Http.Json;

using Aonik.Commerce.Contracts.Models.GiftCards;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Checkout;
using Aonik.Commerce.Services.GiftCards;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Tasks;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Api.Tests;

public sealed class GiftCardCartApiTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private const string Secret = "UNRECOGNISED-GIFT-CARD-CODE";
    private const string GiftPath = "/commerce/storefront/gift-cards";

    [Fact]
    public async Task Options_Should_DefaultToDisabled_WithoutCreatingCartOrFinancialValue()
    {
        var owner = await CustomerAsync(Guid.NewGuid());
        using var anonymous = Anonymous(owner.TenantId);
        using var response = await anonymous.GetAsync(GiftPath + "/options");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        Private(response);
        var options = (await response.Content.ReadFromJsonAsync<GiftCardOptionsDto>())!;
        options.Enabled.Should().BeFalse();
        options.Values.Should().BeEmpty();
        (await response.Content.ReadAsStringAsync()).Should().NotContain("ledgerId").And.NotContain("liabilityAccountId");
        await using var scope = Scope(owner.TenantId);
        (await scope.ServiceProvider.GetRequiredService<CommerceDbContext>().Carts.CountAsync(x => x.TenantId == owner.TenantId)).Should().Be(0);
        (await scope.ServiceProvider.GetRequiredService<Aonik.Finance.Persistence.FinanceDbContext>().GiftCards.CountAsync(x => x.TenantId == owner.TenantId)).Should().Be(0);
    }

    [Theory]
    [InlineData("", HttpStatusCode.NotFound)]
    [InlineData("?code=URL-SECRET", HttpStatusCode.BadRequest)]
    public async Task Balance_Should_AcceptCodeOnlyInBody_AndKeepUnknownCodePrivate(string query, HttpStatusCode status)
    {
        var owner = await CustomerAsync(Guid.NewGuid());
        using var anonymous = Anonymous(owner.TenantId);
        using var response = await anonymous.PostAsJsonAsync(GiftPath + "/balance" + query, new { code = Secret });
        response.StatusCode.Should().Be(status);
        Private(response);
        (await response.Content.ReadAsStringAsync()).Should().NotContain(Secret).And.NotContain("URL-SECRET");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(129)]
    public async Task Balance_Should_RejectMissingOrOversizedCode(int length)
    {
        var owner = await CustomerAsync(Guid.NewGuid());
        using var anonymous = Anonymous(owner.TenantId);
        using var response = await anonymous.PostAsJsonAsync(GiftPath + "/balance", new { code = new string('X', length) });
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        Private(response);
    }

    [Fact]
    public async Task Purchase_Should_AuthorizeBeforeVersionGuard_ThenFailClosedWhenUnconfigured()
    {
        var cart = await SeedCartAsync();
        using var anonymous = Anonymous(cart.TenantId);
        var selection = Selection();
        foreach (var token in new string?[] { null, "wrong-token" })
        {
            using var rejected = await SendAsync(anonymous, HttpMethod.Put, PurchasePath(cart.Id), selection, token, cart.Version);
            rejected.StatusCode.Should().Be(HttpStatusCode.NotFound);
            Private(rejected);
        }
        foreach (var version in new string?[] { null, Convert.ToBase64String(new byte[] { 99 }) })
        {
            using var rejected = await SendAsync(anonymous, HttpMethod.Put, PurchasePath(cart.Id), selection, cart.Token, version);
            rejected.StatusCode.Should().Be(HttpStatusCode.Conflict);
            Private(rejected);
        }
        using var disabled = await SendAsync(anonymous, HttpMethod.Put, PurchasePath(cart.Id), selection, cart.Token, cart.Version);
        disabled.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Private(disabled);
        await AssertUnchangedAsync(cart);
    }

    [Fact]
    public async Task Tender_Should_RejectQuerySecrets_AndInvalidCodeWithoutPersistingGrant()
    {
        var cart = await SeedCartAsync();
        using var anonymous = Anonymous(cart.TenantId);
        foreach (var query in new[] { "", "?code=URL-SECRET" })
        {
            using var rejected = await SendAsync(anonymous, HttpMethod.Put, TenderPath(cart.Id) + query,
                new { code = Secret, requestedAmount = 5m }, cart.Token, cart.Version);
            rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            Private(rejected);
            (await rejected.Content.ReadAsStringAsync()).Should().NotContain(Secret).And.NotContain("URL-SECRET").And.NotContain("privateCartGrant");
        }
        await AssertUnchangedAsync(cart);
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(129, 5)]
    [InlineData(20, 0)]
    [InlineData(20, -1)]
    [InlineData(20, 1000000)]
    public async Task Tender_Should_ValidateBoundedCodeAndRequestedAmount(int codeLength, int amount)
    {
        var cart = await SeedCartAsync();
        using var anonymous = Anonymous(cart.TenantId);
        using var response = await SendAsync(anonymous, HttpMethod.Put, TenderPath(cart.Id),
            new { code = new string('X', codeLength), requestedAmount = amount }, cart.Token, cart.Version);
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        Private(response);
        await AssertUnchangedAsync(cart);
    }

    [Theory]
    [InlineData("gift-card-purchase")]
    [InlineData("gift-card-tender")]
    public async Task Remove_Should_UsePrincipalForOwnedCart_AndPreserveLockedCart(string route)
    {
        var cart = await SeedCartAsync(owned: true);
        using var anonymous = Anonymous(cart.TenantId);
        var other = await CustomerAsync(cart.TenantId);
        var foreign = await CustomerAsync(Guid.NewGuid());
        var path = $"/commerce/carts/{cart.Id}/{route}";
        foreach (var client in new[] { anonymous, other.Client, foreign.Client })
        {
            using var rejected = await SendAsync(client, HttpMethod.Delete, path,
                new { partyId = cart.Owner.PartyId }, cart.Token, cart.Version);
            rejected.StatusCode.Should().Be(HttpStatusCode.NotFound);
            Private(rejected);
        }
        using var removed = await SendAsync(cart.Owner.Client, HttpMethod.Delete, path, null, null, cart.Version);
        removed.StatusCode.Should().Be(HttpStatusCode.OK);
        Private(removed);
        (await removed.Content.ReadAsStringAsync()).Should().NotContain("privateCartGrant").And.NotContain(cart.Token);
        var selected = (await removed.Content.ReadFromJsonAsync<GiftCardCartResponse>())!;
        selected.CartId.Should().Be(cart.Id);

        await using (var scope = Scope(cart.TenantId))
        {
            var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
            var row = await db.Carts.SingleAsync(x => x.Id == cart.Id);
            row.CheckoutState = CartCheckoutStates.Preparing;
            await db.SaveChangesAsync();
        }
        using var locked = await SendAsync(cart.Owner.Client, HttpMethod.Delete, path, null, null, selected.CartVersion);
        locked.StatusCode.Should().Be(HttpStatusCode.Conflict);
        Private(locked);
    }

    [Fact]
    public async Task PublicTaskSchedule_Should_IgnoreInternalStableTaskId_AndCreateDistinctWorkItem()
    {
        var tenant = Guid.NewGuid();
        using var admin = await factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithTenant(tenant)
            .WithRoles("TenantAdmin").WithPermissions("Tasks.Write"));
        var forbiddenId = Guid.NewGuid();
        using var response = await admin.PostAsJsonAsync("/tasks", new
        {
            title = "Public gift delivery task", kind = "ScheduledAction", actionType = GiftCardDeliveryService.ActionType,
            actionPayloadJson = "{}", assigneeType = "System", runAtUtc = DateTime.UtcNow.AddDays(1), taskId = forbiddenId
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var task = (await response.Content.ReadFromJsonAsync<TaskResponse>())!;
        task.Id.Should().NotBe(forbiddenId);
        await using var scope = Scope(tenant);
        var rows = scope.ServiceProvider.GetRequiredService<PlatformDbContext>().WorkItems;
        (await rows.AnyAsync(x => x.TenantId == tenant && x.Id == forbiddenId)).Should().BeFalse();
        (await rows.CountAsync(x => x.TenantId == tenant && x.Id == task.Id)).Should().Be(1);
    }

    private async Task AssertUnchangedAsync(SeededCart cart)
    {
        await using var scope = Scope(cart.TenantId);
        var row = await scope.ServiceProvider.GetRequiredService<CommerceDbContext>().Carts.Include(x => x.Items).SingleAsync(x => x.Id == cart.Id);
        row.GiftCardPurchaseJson.Should().BeNull();
        row.GiftCardTenderJson.Should().BeNull();
        row.Items.Should().BeEmpty();
        Convert.ToBase64String(row.RowVersion).Should().Be(cart.Version);
        row.LastActivityAtUtc.Should().Be(cart.LastActivity);
    }

    private async Task<SeededCart> SeedCartAsync(bool owned = false)
    {
        var owner = await CustomerAsync(Guid.NewGuid());
        await using var scope = Scope(owner.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var row = new Cart
        {
            TenantId = owner.TenantId, BuyerPartyId = owned ? owner.PartyId : null,
            AnonymousToken = CartAccess.MintToken(), Status = CartStatuses.Open, Currency = "GBP",
            RowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, LastActivityAtUtc = DateTime.UtcNow.AddDays(-1)
        };
        db.Carts.Add(row);
        await db.SaveChangesAsync();
        // InMemory does not advance SQL rowversion. These tests verify exact transport
        // preconditions and deliberately use a different token for stale requests.
        return new(row.Id, owner.TenantId, row.AnonymousToken!, Convert.ToBase64String(row.RowVersion), row.LastActivityAtUtc, owner);
    }

    private async Task<Customer> CustomerAsync(Guid tenant)
    {
        var auth = TestAuthOptions.Create().WithTenant(tenant).WithRoles("PersonalUser").WithPermissions("UserInfo.Read");
        var client = await factory.CreateAuthenticatedClientAsync(auth);
        return new(tenant, await WorkspaceTestSeeding.SeedPartyAsync(factory, tenant, auth.UserId, "Gift purchaser"), client);
    }

    private HttpClient Anonymous(Guid tenant)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenant.ToString());
        return client;
    }

    private AsyncServiceScope Scope(Guid tenant)
    {
        var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenant;
        return scope;
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path,
        object? body, string? token, string? version)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body != null) request.Content = JsonContent.Create(body);
        if (token != null) request.Headers.Add("X-Cart-Token", token);
        if (version != null) request.Headers.Add("X-Cart-Version", version);
        return await client.SendAsync(request);
    }

    private static GiftCardPurchaseSelection Selection() => new(25m, "Email", "Recipient", "unconfigured",
        RecipientEmail: "recipient@example.test", SendDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)));
    private static string PurchasePath(Guid id) => $"/commerce/carts/{id}/gift-card-purchase";
    private static string TenderPath(Guid id) => $"/commerce/carts/{id}/gift-card-tender";
    private static void Private(HttpResponseMessage response)
    {
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        response.Headers.GetValues("Referrer-Policy").Should().Contain("no-referrer");
    }

    private sealed record Customer(Guid TenantId, Guid PartyId, HttpClient Client);
    private sealed record SeededCart(Guid Id, Guid TenantId, string Token, string Version, DateTime? LastActivity, Customer Owner);
}
