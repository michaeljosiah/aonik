using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Aonik.Finance.Entities.Ledger;
using Aonik.Finance.Entities.Orders;
using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Payments;

using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aonik.Api.Tests;

public sealed class CommerceRefundEndpointTests(CommerceRefundEndpointTests.Factory factory) : IClassFixture<CommerceRefundEndpointTests.Factory>
{
    [Fact]
    public async Task RefundRoutes_Should_EnforceAuthenticationStaffPermissionsAndTenant()
    {
        var original = await SeedAsync();
        using var anonymous = factory.CreateClient();
        anonymous.DefaultRequestHeaders.Add("X-Tenant-Id", original.TenantId.ToString());
        using var unauthenticated = await anonymous.GetAsync(original.Path);
        unauthenticated.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        Private(unauthenticated);
        using var denied = await ClientAsync(original.TenantId, "Operations", "UserInfo.Read");
        using var forbidden = await denied.GetAsync(original.Path);
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var reader = await ClientAsync(original.TenantId, "ReadOnly", "Payment.Read");
        (await reader.GetAsync(original.Path)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await reader.PostAsJsonAsync(original.Path + "/preview", Draft(original))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var customer = await ClientAsync(original.TenantId, "PersonalUser", "Payment.Read", "Payment.Refund");
        (await customer.GetAsync(original.Path)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var foreign = await ClientAsync(Guid.NewGuid(), "Operations", "Payment.Read", "Payment.Refund");
        (await foreign.GetAsync(original.Path)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await foreign.PostAsJsonAsync(original.Path + "/preview", Draft(original))).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Request_Should_UseReviewedServerAmounts_ReplayOriginalId_AndKeepFundingImmutable()
    {
        var original = await SeedAsync();
        using var writer = await ClientAsync(original.TenantId, "Operations", "Payment.Read", "Payment.Refund");
        var preview = await PreviewAsync(writer, original);
        preview.CashAmount.Should().Be(20m);
        var request = new RefundRequest(Guid.NewGuid(), "Damaged delivery", preview.Selections, preview.Version);
        using var accepted = await writer.PostAsJsonAsync(original.Path, request);
        accepted.StatusCode.Should().Be(HttpStatusCode.Accepted);
        Private(accepted);
        var row = (await accepted.Content.ReadFromJsonAsync<RefundDto>())!;
        row.Status.Should().Be("Requested");
        row.EffectsAppliedAtUtc.Should().BeNull();
        using var replay = await writer.PostAsJsonAsync(original.Path, request);
        replay.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await replay.Content.ReadFromJsonAsync<RefundDto>())!.RefundId.Should().Be(request.RefundId);
        using var changed = await writer.PostAsJsonAsync(original.Path, request with { Reason = "Different reason" });
        changed.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var next = await writer.PostAsJsonAsync(original.Path, request with { RefundId = Guid.NewGuid() });
        next.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var context = (await writer.GetFromJsonAsync<RefundContextDto>(original.Path))!;
        context.Status.Should().Be("RefundPending");
        context.CanRequest.Should().BeFalse();
        context.History.Should().ContainSingle();
        using var detail = await writer.GetAsync(original.Path + "/" + request.RefundId);
        var json = await detail.Content.ReadAsStringAsync();
        json.Should().NotContain("requestSnapshotJson").And.NotContain("providerAccountId").And.NotContain("pi_private_original")
            .And.NotContain("captureJournalId").And.NotContain("rawResponseJson");
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = original.TenantId;
        var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        (await db.PaymentIntents.SingleAsync(x => x.Id == original.IntentId)).Status.Should().Be("Captured");
        (await db.Orders.SingleAsync(x => x.Id == original.OrderId)).Status.Should().Be("Completed");
        (await db.Payments.SingleAsync(x => x.PaymentIntentId == original.IntentId)).Amount.Should().Be(20m);
        (await db.Refunds.CountAsync()).Should().Be(1);
        (await db.OrderHistoryEvents.CountAsync(x => x.OrderId == original.OrderId && x.EventType == "RefundRequested")).Should().Be(1);
    }

    [Fact]
    public async Task Request_Should_RejectStalePreviewAndInvalidPartialBeforeAdmission()
    {
        var original = await SeedAsync();
        using var writer = await ClientAsync(original.TenantId, "Operations", "Payment.Read", "Payment.Refund");
        using var invalid = await writer.PostAsJsonAsync(original.Path + "/preview", new RefundDraft("Test", [new(original.ComponentId, 0.001m)]));
        invalid.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var preview = await PreviewAsync(writer, original);
        using var stale = await writer.PostAsJsonAsync(original.Path,
            new RefundRequest(Guid.NewGuid(), "Damaged delivery", preview.Selections, new string('0', 64)));
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await writer.GetFromJsonAsync<RefundContextDto>(original.Path))!.History.Should().BeEmpty();
    }

    [Fact]
    public async Task Preview_Should_RejectMissingOriginalCashProof()
    {
        var original = await SeedAsync(journal: false);
        using var writer = await ClientAsync(original.TenantId, "Operations", "Payment.Read", "Payment.Refund");
        using var response = await writer.PostAsJsonAsync(original.Path + "/preview", Draft(original));
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain("original cash journal");
    }

    private static RefundDraft Draft(Original original) => new("Damaged delivery", [new(original.ComponentId, FullRemaining: true)]);
    private static async Task<RefundPreviewDto> PreviewAsync(HttpClient client, Original original)
    {
        using var response = await client.PostAsJsonAsync(original.Path + "/preview", Draft(original));
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<RefundPreviewDto>())!;
    }
    private Task<HttpClient> ClientAsync(Guid tenant, string role, params string[] permissions) => factory.CreateAuthenticatedClientAsync(
        TestAuthOptions.Create().WithTenant(tenant).WithRoles(role).WithPermissions(permissions));
    private async Task<Original> SeedAsync(bool journal = true)
    {
        var tenantId = Guid.NewGuid();
        using var client = await ClientAsync(tenantId, "Operations", "Payment.Read", "Payment.Refund");
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;
        var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var order = new Order { TenantId = tenantId, OrderType = "ProductPurchase", Status = "Completed", AmountIn = 20m, CurrencyIn = "GBP" };
        var item = new OrderItem { TenantId = tenantId, OrderId = order.Id, ItemType = "Product", ItemIndex = 0, AmountIn = 20m, CurrencyIn = "GBP" };
        var intent = new PaymentIntent { TenantId = tenantId, OrderId = order.Id, Amount = 20m, Currency = "GBP", Status = "Captured",
            ProviderCode = "Stripe", ProviderAccountId = "acct_original", ProviderLiveMode = false, ConnectorId = Guid.NewGuid(),
            ProviderPaymentIntentReference = "pi_private_original", ProviderReference = "cs_original" };
        var receipt = new Payment { TenantId = tenantId, PaymentIntentId = intent.Id, Amount = 20m, Currency = "GBP", Provider = "Stripe",
            ProviderReference = intent.ProviderPaymentIntentReference, ConnectorId = intent.ConnectorId, OutcomeStatus = "Captured", CapturedAt = DateTime.UtcNow };
        var ledger = new Ledger { TenantId = tenantId, BaseCurrency = "GBP" };
        var cash = new LedgerAccount { TenantId = tenantId, LedgerId = ledger.Id, Code = "1000", Name = "Cash", AccountType = "Asset" };
        var clearing = new LedgerAccount { TenantId = tenantId, LedgerId = ledger.Id, Code = "2100", Name = "Clearing", AccountType = "Liability" };
        db.Orders.Add(order); db.OrderItems.Add(item); db.PaymentIntents.Add(intent); db.Payments.Add(receipt);
        db.Ledgers.Add(ledger); db.LedgerAccounts.AddRange(cash, clearing);
        if (journal)
        {
            var entry = new JournalEntry { TenantId = tenantId, LedgerId = ledger.Id, SourceType = "PaymentCapture", SourceId = intent.Id,
                Status = "Posted", Timestamp = DateTime.UtcNow };
            entry.Lines.Add(new JournalEntryLine { TenantId = tenantId, JournalEntryId = entry.Id, LedgerAccountId = cash.Id, Direction = "Debit", Amount = 20m, Currency = "GBP" });
            entry.Lines.Add(new JournalEntryLine { TenantId = tenantId, JournalEntryId = entry.Id, LedgerAccountId = clearing.Id, Direction = "Credit", Amount = 20m, Currency = "GBP" });
            db.JournalEntries.Add(entry);
        }
        await db.SaveChangesAsync();
        factory.Sources[(tenantId, order.Id)] = new(order.Id, intent.Id, null, "GBP", 20m, 0m, 20m,
            [new(item.Id.ToString("N"), item.Id, "Product", "Original box", 20m, 0m, 0, 0)]);
        return new(tenantId, order.Id, intent.Id, item.Id.ToString("N"));
    }
    private static void Private(HttpResponseMessage response)
    {
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        response.Headers.GetValues("Referrer-Policy").Should().Contain("no-referrer");
    }
    private sealed record Original(Guid TenantId, Guid OrderId, Guid IntentId, string ComponentId)
    {
        public string Path => $"/commerce/admin/orders/{OrderId}/refunds";
    }
    public sealed class Factory : CustomWebApplicationFactory
    {
        public ConcurrentDictionary<(Guid, Guid), CheckoutRefundSource> Sources { get; } = new();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICheckoutRefundSourceReader>();
                services.AddScoped<ICheckoutRefundSourceReader>(sp => new Source(this, sp.GetRequiredService<ITenantProvider>()));
            });
        }
    }
    private sealed class Source(Factory factory, ITenantProvider tenant) : ICheckoutRefundSourceReader
    {
        public Task<CheckoutRefundSource?> ReadAsync(Guid orderId, CancellationToken cancellationToken = default)
            => Task.FromResult(factory.Sources.GetValueOrDefault((tenant.GetCurrentTenantId(), orderId)));
    }
}
