using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Contracts.Models.GiftCards;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.GiftCards;
using Aonik.SharedKernel.Abstractions.GiftCards;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Events.Integration;

using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aonik.Api.Tests;

public sealed class CommerceGiftCardDeliveryEndpointTests(CommerceGiftCardDeliveryEndpointTests.Factory factory)
    : IClassFixture<CommerceGiftCardDeliveryEndpointTests.Factory>
{
    private const string CustomerPath = "/commerce/storefront/gift-cards";
    private const string AdminPath = "/commerce/admin/gift-card-deliveries";
    private const string Code = "PRIVATE-PRINT-CODE-42";

    [Fact]
    public async Task PurchaserList_Should_UsePrincipalOwnership_AndNeverExposeRecipientEmailOrSecret()
    {
        var own = await SeedAsync(GiftCardDeliveryMethods.Email);
        var other = await CustomerAsync(own.TenantId);
        var foreign = await CustomerAsync(Guid.NewGuid());
        using var response = await own.Client.GetAsync(CustomerPath + "?partyId=" + other.PartyId);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        Private(response);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().NotContain(Code).And.NotContain("recipient@example.test").And.NotContain("purchaseSnapshotJson")
            .And.NotContain("paymentIntentId").And.NotContain("private message");
        var list = JsonSerializer.Deserialize<PagedResult<SentGiftCardDto>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        list.Items.Should().ContainSingle().Which.DeliveryId.Should().Be(own.DeliveryId);
        foreach (var person in new[] { other, foreign })
        {
            (await person.Client.GetFromJsonAsync<PagedResult<SentGiftCardDto>>(CustomerPath))!.Items.Should().BeEmpty();
            using var rejected = await person.Client.PostAsync($"{CustomerPath}/{own.DeliveryId}/resend", null);
            rejected.StatusCode.Should().Be(HttpStatusCode.NotFound);
            Private(rejected);
        }
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=101")]
    [InlineData("?page=2147483647&pageSize=100")]
    public async Task PurchaserList_Should_RejectInvalidPaging(string query)
    {
        var own = await CustomerAsync(Guid.NewGuid());
        using var response = await own.Client.GetAsync(CustomerPath + query);
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        Private(response);
    }

    [Fact]
    public async Task GiftRoutes_Should_RequireAuthenticationAndProfile_AndKeepEarlyResponsesPrivate()
    {
        var tenant = Guid.NewGuid();
        using var anonymous = factory.CreateClient();
        anonymous.DefaultRequestHeaders.Add("X-Tenant-Id", tenant.ToString());
        using var authResponse = await anonymous.GetAsync(CustomerPath);
        authResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        Private(authResponse);
        using var noProfile = await factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithTenant(tenant)
            .WithRoles("PersonalUser").WithPermissions("UserInfo.Read"));
        using var missing = await noProfile.GetAsync(CustomerPath);
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        Private(missing);
    }

    [Fact]
    public async Task Resend_Should_AcceptOnlyIssuedDueOwnedEmail_AndScheduleSameRecipientWithoutReturningCode()
    {
        var own = await SeedAsync(GiftCardDeliveryMethods.Email, sent: true);
        using var accepted = await own.Client.PostAsJsonAsync($"{CustomerPath}/{own.DeliveryId}/resend",
            new { recipientEmail = "attacker@example.test", partyId = Guid.NewGuid() });
        accepted.StatusCode.Should().Be(HttpStatusCode.Accepted);
        Private(accepted);
        (await accepted.Content.ReadAsStringAsync()).Should().BeEmpty();
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = own.TenantId;
        var row = await scope.ServiceProvider.GetRequiredService<CommerceDbContext>().OrderGiftCardDeliveries.SingleAsync();
        row.SendSequence.Should().Be(2);
        GiftCardDeliveryData.Read(row).RecipientEmail.Should().Be("recipient@example.test");
        var tasks = scope.ServiceProvider.GetRequiredService<Aonik.Platform.Persistence.PlatformDbContext>().WorkItems;
        var scheduled = await tasks.SingleAsync(task => task.TenantId == own.TenantId && task.SubjectId == row.Id);
        scheduled.ActionPayloadJson.Should().NotContain(Code).And.NotContain("example.test");
        using var repeat = await own.Client.PostAsync($"{CustomerPath}/{own.DeliveryId}/resend", null);
        repeat.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await tasks.CountAsync(task => task.TenantId == own.TenantId && task.SubjectId == row.Id)).Should().Be(1);
    }

    [Fact]
    public async Task Resend_Should_RejectFutureAndPhysicalDeliveries()
    {
        foreach (var method in new[] { GiftCardDeliveryMethods.Email, GiftCardDeliveryMethods.Post })
        {
            var own = await SeedAsync(method);
            using var rejected = await own.Client.PostAsync($"{CustomerPath}/{own.DeliveryId}/resend", null);
            rejected.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            Private(rejected);
        }
    }

    [Fact]
    public async Task PhysicalRoutes_Should_EnforceStaffPermissionAndTenant_ThenAuditPrivatePrintAndCompletion()
    {
        var own = await SeedAsync(GiftCardDeliveryMethods.Post);
        using var deniedStaff = await factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithTenant(own.TenantId)
            .WithRoles("Operations").WithPermissions("UserInfo.Read"));
        using var readOnly = await factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithTenant(own.TenantId)
            .WithRoles("ReadOnly").WithPermissions("Customers.Read"));
        using var writer = await factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithTenant(own.TenantId)
            .WithRoles("Operations").WithPermissions("Customers.Read", "Customers.Write"));
        using var foreign = await factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithTenant(Guid.NewGuid())
            .WithRoles("Operations").WithPermissions("Customers.Read", "Customers.Write"));
        foreach (var denied in new[] { own.Client, deniedStaff, readOnly })
        {
            using var forbidden = await denied.PostAsync($"{AdminPath}/{own.DeliveryId}/print", null);
            forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            Private(forbidden);
        }
        using var missing = await foreign.PostAsync($"{AdminPath}/{own.DeliveryId}/print", null);
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        Private(missing);
        using var listResponse = await readOnly.GetAsync(AdminPath);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await listResponse.Content.ReadAsStringAsync()).Should().NotContain(Code).And.NotContain("Street");
        Private(listResponse);

        using var printResponse = await writer.PostAsync($"{AdminPath}/{own.DeliveryId}/print", null);
        printResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        Private(printResponse);
        var print = (await printResponse.Content.ReadFromJsonAsync<GiftCardPrintDto>())!;
        print.Code.Should().Be(Code);
        print.PostalAddress!.Line1.Should().Be("1 Street");
        (await printResponse.Content.ReadAsStringAsync()).Should().NotContain("paymentIntentId").And.NotContain("purchaserEmail");
        using var stale = await writer.PostAsJsonAsync($"{AdminPath}/{own.DeliveryId}/complete", new { expectedVersion = Convert.ToBase64String(new byte[] { 42 }) });
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        Private(stale);
        using var completed = await writer.PostAsJsonAsync($"{AdminPath}/{own.DeliveryId}/complete", new { expectedVersion = print.Version });
        completed.StatusCode.Should().Be(HttpStatusCode.OK);
        Private(completed);
        (await completed.Content.ReadFromJsonAsync<PhysicalGiftCardDto>())!.Status.Should().Be("Posted");
        (await readOnly.GetFromJsonAsync<PagedResult<PhysicalGiftCardDto>>(AdminPath))!.Items.Should().BeEmpty();
        using var missingVersion = await writer.PostAsJsonAsync($"{AdminPath}/{own.DeliveryId}/complete", new { });
        missingVersion.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Private(missingVersion);
    }

    private async Task<Person> CustomerAsync(Guid tenant)
    {
        var auth = TestAuthOptions.Create().WithTenant(tenant).WithRoles("PersonalUser").WithPermissions("UserInfo.Read");
        var client = await factory.CreateAuthenticatedClientAsync(auth);
        var party = await WorkspaceTestSeeding.SeedPartyAsync(factory, tenant, auth.UserId, "Gift purchaser");
        return new(tenant, party, client);
    }

    private async Task<Purchase> SeedAsync(string method, bool sent = false)
    {
        var owner = await CustomerAsync(Guid.NewGuid());
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = owner.TenantId;
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var orderId = Guid.NewGuid();
        var intentId = Guid.NewGuid();
        var cart = new Cart { TenantId = owner.TenantId, OrderId = orderId, BuyerPartyId = owner.PartyId, Currency = "GBP", Status = CartStatuses.CheckedOut };
        db.Carts.Add(cart);
        var now = DateTime.UtcNow;
        var purchase = new GiftCardPurchaseSnapshot(1, Guid.NewGuid(), 25m, "GBP", method, "Recipient",
            method == GiftCardDeliveryMethods.Email ? "recipient@example.test" : null,
            method == GiftCardDeliveryMethods.Email ? (sent ? now.AddHours(-1) : now.AddDays(1)) : null,
            method == GiftCardDeliveryMethods.Post ? DateOnly.FromDateTime(now) : null,
            method == GiftCardDeliveryMethods.Post ? new DeliveryAddressDto("1 Street", null, "London", null, "SW1A 1AA", "GB") : null,
            Message: "private message", SenderName: "Buyer");
        await GiftCardDeliveryData.StageTrackedAsync(db, owner.TenantId, cart.Id, orderId, intentId, purchase);
        await db.SaveChangesAsync();
        var row = await db.OrderGiftCardDeliveries.SingleAsync();
        var source = GiftCardDeliveryData.Source(row);
        var card = new GiftCardIssuedInfo(Guid.NewGuid(), source, 25m, "GBP", "****0042", now, null, "v1", "Active");
        factory.Proofs[(owner.TenantId, source)] = card;
        // Existing issued proof is supplied at the Finance seam; all ownership, storage,
        // operational transitions and scheduling below execute through real DI services.
        row.GiftCardId = card.GiftCardId;
        row.MaskedCode = card.MaskedCode;
        row.Status = sent ? "Sent" : "Ready";
        if (method == GiftCardDeliveryMethods.Email)
        {
            row.SendSequence = 1;
            row.SequenceDueAtUtc = purchase.SendAtUtc;
            if (sent) { row.SentSequence = 1; row.LastSentAtUtc = now.AddMinutes(-10); }
        }
        await db.SaveChangesAsync();
        return new(owner.TenantId, owner.PartyId, owner.Client, row.Id);
    }

    private static void Private(HttpResponseMessage response)
    {
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        response.Headers.GetValues("Referrer-Policy").Should().Contain("no-referrer");
    }

    private sealed record Person(Guid TenantId, Guid PartyId, HttpClient Client);
    private sealed record Purchase(Guid TenantId, Guid PartyId, HttpClient Client, Guid DeliveryId);

    public sealed class Factory : CustomWebApplicationFactory
    {
        public System.Collections.Concurrent.ConcurrentDictionary<(Guid, GiftCardPurchaseSource), GiftCardIssuedInfo> Proofs { get; } = new();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IGiftCardService>();
                services.AddScoped<IGiftCardService>(provider => new IssuedProofs(this, provider.GetRequiredService<ITenantProvider>()));
            });
        }
    }

    private sealed class IssuedProofs(Factory factory, ITenantProvider tenant) : IGiftCardService
    {
        public Task<GiftCardIssuedInfo?> GetIssuedAsync(GiftCardPurchaseSource source, CancellationToken cancellationToken = default)
            => Task.FromResult(factory.Proofs.GetValueOrDefault((tenant.GetCurrentTenantId(), source)));
        public async Task<GiftCardFulfilmentSecret> GetFulfilmentSecretAsync(GiftCardPurchaseSource source, CancellationToken cancellationToken = default)
            => new((await GetIssuedAsync(source, cancellationToken)) ?? throw new InvalidOperationException("Missing test issuance proof."), Code);
        public Task<GiftCardPolicy> GetPolicyAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<GiftCardBalance?> GetBalanceAsync(string code, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<GiftCardAuthorization> AuthorizeForCartAsync(string code, Guid cartId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<GiftCardFundingQuote> QuoteAsync(GiftCardQuoteRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
