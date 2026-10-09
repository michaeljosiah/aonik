using System.Text.Json;

using Aonik.Finance.Contracts.Models.Payments;
using Aonik.Finance.Contracts.Services.Payments;
using Aonik.Finance.Entities.Ledger;
using Aonik.Finance.Entities.Orders;
using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.GiftCards;
using Aonik.Finance.Services.Payments;
using Aonik.SharedKernel.Abstractions.GiftCards;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Events.Outbox;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Aonik.Database.Tests.Finance;

public sealed partial class CheckoutPaymentReconciliationSqlServerTests
{
    [SkippableFact]
    public async Task GiftCheckout_Should_AdmitOnlyOneOfTwoCartsUsingLastValue()
    {
        RequireSql();
        var gate = new PauseRewardClaims();
        await using var harness = await BuildAsync(gate, seedPayment: false);
        var first = await SeedGiftRequestAsync(harness, 23.45m);
        var secondOrder = Guid.NewGuid();
        var secondItem = Guid.NewGuid();
        await using (var scope = harness.NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
            db.Orders.Add(new Order { Id = secondOrder, TenantId = harness.Intent.TenantId, PayerPartyId = harness.Intent.PayerPartyId,
                OrderType = "ProductPurchase", Status = "Draft", AmountIn = 23.45m, CurrencyIn = "GBP",
                Items = [new OrderItem { Id = secondItem, TenantId = harness.Intent.TenantId, OrderId = secondOrder,
                    ItemIndex = 0, ItemType = "ProductPurchase", AmountIn = 23.45m, Quantity = 1, UnitPrice = 23.45m, CurrencyIn = "GBP" }] });
            await db.SaveChangesAsync();
        }
        var secondGift = await QuoteGiftAsync(harness, first.Code, secondItem, 23.45m);
        var secondId = Guid.NewGuid();
        var second = first.Request with { OrderId = secondOrder, PaymentIntentId = secondId,
            IdempotencyKey = $"gift:{secondId:N}", GiftCard = secondGift };
        gate.Armed = true;
        var a = CreateGiftAsync(harness, first.Request);
        var b = CreateGiftAsync(harness, second);
        try
        {
            var completed = await Task.WhenAny(gate.Reached.Task, a, b).WaitAsync(TimeSpan.FromSeconds(30));
            if (completed != gate.Reached.Task) { await completed; throw new InvalidOperationException("Gift checkout ended before contention."); }
        }
        finally { gate.Release.TrySetResult(); }
        var results = await Task.WhenAll(a, b).WaitAsync(TimeSpan.FromSeconds(30));
        results.Select(r => r.Status).Should().BeEquivalentTo(["Captured", "Cancelled"]);
        harness.Gateway.Requests.Should().BeEmpty();
        await using var verify = harness.NewScope();
        var dbVerify = verify.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var balance = await verify.ServiceProvider.GetRequiredService<GiftCardService>().GetBalanceAsync(first.Code);
        balance!.Balance.Should().Be(0m);
        balance.Reserved.Should().Be(0m);
        (await dbVerify.Payments.CountAsync(p => p.Provider == "GiftCard")).Should().Be(1);
        (await dbVerify.GiftCards.SingleAsync()).RowVersion.Should().HaveCount(8);
    }

    [SkippableTheory]
    [InlineData(10)]
    [InlineData(23.45)]
    public async Task GiftCapture_Should_RollBackAllEffectsThenConvergeAfterReceiptFailure(decimal giftAmount)
    {
        RequireSql();
        var failure = new FailFinalReceiptSave();
        await using var harness = await BuildAsync(failure, seedPayment: false);
        var setup = await SeedGiftRequestAsync(harness, giftAmount);
        if (giftAmount < 23.45m) await CreateGiftAsync(harness, setup.Request);
        failure.Armed = true;
        Func<Task> fail = giftAmount == 23.45m
            ? async () => { await CreateGiftAsync(harness, setup.Request); }
            : () => CaptureGiftAsync(harness, harness.Gateway.Requests.Single());
        await fail.Should().ThrowAsync<InjectedReceiptFailure>();
        await using (var verify = harness.NewScope())
        {
            var db = verify.ServiceProvider.GetRequiredService<FinanceDbContext>();
            (await db.Payments.CountAsync(p => p.PaymentIntentId == harness.Intent.Id)).Should().Be(0);
            (await db.JournalEntries.CountAsync(e => e.SourceId == harness.Intent.Id)).Should().Be(0);
            var balance = await verify.ServiceProvider.GetRequiredService<GiftCardService>().GetBalanceAsync(setup.Code);
            balance!.Balance.Should().Be(23.45m);
            balance.Reserved.Should().Be(giftAmount);
            (await db.PaymentIntents.SingleAsync(p => p.Id == harness.Intent.Id)).Status.Should().Be("Pending");
        }
        if (giftAmount == 23.45m) await CreateGiftAsync(harness, setup.Request);
        else await CaptureGiftAsync(harness, harness.Gateway.Requests.Single());
        await using var final = harness.NewScope();
        var finalDb = final.ServiceProvider.GetRequiredService<FinanceDbContext>();
        (await finalDb.Payments.Where(p => p.PaymentIntentId == harness.Intent.Id).SumAsync(p => p.Amount)).Should().Be(23.45m);
        (await final.ServiceProvider.GetRequiredService<GiftCardService>().GetBalanceAsync(setup.Code))!.Balance.Should().Be(23.45m - giftAmount);
        var events = await finalDb.Set<OutboxMessage>().Where(e => e.EventType == typeof(PaymentCompletedEvent).FullName).ToListAsync();
        events.Select(e => JsonSerializer.Deserialize<PaymentCompletedEvent>(e.Payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)))
            .Where(e => e!.PaymentId == harness.Intent.Id).Should().ContainSingle();
    }

    [SkippableFact]
    public async Task GiftOnlyCompletion_Should_LoseToCommittedCancellationWithoutDebitingValue()
    {
        RequireSql();
        var gate = new PauseGiftCompletion();
        await using var harness = await BuildAsync(gate, seedPayment: false);
        var setup = await SeedGiftRequestAsync(harness, 23.45m);
        gate.Armed = true;
        var completion = CreateGiftAsync(harness, setup.Request);
        try
        {
            var completed = await Task.WhenAny(gate.Reached.Task, completion).WaitAsync(TimeSpan.FromSeconds(30));
            if (completed != gate.Reached.Task) { await completed; throw new InvalidOperationException("Gift checkout ended before its completion claim."); }
            await using var cancel = harness.NewScope();
            (await cancel.ServiceProvider.GetRequiredService<ICheckoutPaymentReconciler>()
                .ReconcileAsync(harness.Intent.Id, expire: true)).Status.Should().Be("Cancelled");
        }
        finally { gate.Release.TrySetResult(); }
        (await completion.WaitAsync(TimeSpan.FromSeconds(30))).Status.Should().Be("Cancelled");
        await using var verify = harness.NewScope();
        var balance = await verify.ServiceProvider.GetRequiredService<GiftCardService>().GetBalanceAsync(setup.Code);
        balance!.Balance.Should().Be(23.45m);
        balance.Reserved.Should().Be(0m);
        (await verify.ServiceProvider.GetRequiredService<FinanceDbContext>().Payments.CountAsync(p => p.PaymentIntentId == harness.Intent.Id)).Should().Be(0);
        harness.Gateway.Requests.Should().BeEmpty();
    }

    private static async Task<GuestPaymentIntentResponse> CreateGiftAsync(Harness harness, CreateCommerceGuestPaymentIntentRequest request)
    {
        await using var scope = harness.NewScope();
        return await scope.ServiceProvider.GetRequiredService<CheckoutPaymentService>().CreateAsync(request);
    }

    private static async Task CaptureGiftAsync(Harness harness, PaymentProviderIntentRequest request)
    {
        await using var scope = harness.NewScope();
        await scope.ServiceProvider.GetRequiredService<ICheckoutPaymentReconciler>().ApplyAsync(request.PaymentIntentId,
            new(harness.Intent.TenantId, request.PaymentIntentId, request.OrderId, request.ConnectorId!.Value,
                request.ProviderAccountId!, request.LiveMode!.Value, "cs_" + request.PaymentIntentId.ToString("N"),
                "pi_" + request.PaymentIntentId.ToString("N"), request.Amount, request.Currency, "Captured", false, null, request.Amount));
    }

    private static async Task<GiftCardCheckout> QuoteGiftAsync(Harness harness, string code, Guid itemId, decimal amount)
    {
        await using var scope = harness.NewScope();
        var gifts = scope.ServiceProvider.GetRequiredService<GiftCardService>();
        var cartId = Guid.NewGuid();
        var authorization = await gifts.AuthorizeForCartAsync(code, cartId);
        var quote = await gifts.QuoteAsync(new(cartId, authorization.PrivateCartGrant!, "GBP", 23.45m, 0m,
            amount, [new(0, itemId, "ProductPurchase", 23.45m, 0m, 0m)]));
        quote.ReasonCode.Should().BeNull();
        return quote.Checkout!;
    }

    private static async Task<GiftSetup> SeedGiftRequestAsync(Harness harness, decimal amount)
    {
        await using var scope = harness.NewScope();
        var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var tenantId = harness.Intent.TenantId;
        var ledger = await db.Ledgers.SingleAsync(l => l.TenantId == tenantId);
        var cash = await db.LedgerAccounts.SingleAsync(a => a.TenantId == tenantId && a.Code == "1000");
        var clearing = new LedgerAccount { TenantId = tenantId, LedgerId = ledger.Id, Code = "2100", Name = "Clearing", AccountType = "Liability" };
        var liability = new LedgerAccount { TenantId = tenantId, LedgerId = ledger.Id, Code = "2202", Name = "Gift liability", AccountType = "Liability" };
        db.LedgerAccounts.AddRange(clearing, liability);
        var purchaseId = Guid.NewGuid();
        var item = new OrderItem { TenantId = tenantId, OrderId = purchaseId, ItemIndex = 0, ItemType = "GiftCardValue",
            Quantity = 1, UnitPrice = 23.45m, AmountIn = 23.45m, CurrencyIn = "GBP", Status = "Valid" };
        db.Orders.Add(new Order { Id = purchaseId, TenantId = tenantId, PayerPartyId = harness.Intent.PayerPartyId,
            OrderType = "ProductPurchase", Status = "Draft", AmountIn = 23.45m, CurrencyIn = "GBP", Items = [item] });
        var target = new OrderItem { TenantId = tenantId, OrderId = harness.Intent.OrderId, ItemIndex = 0, ItemType = "ProductPurchase",
            Quantity = 1, UnitPrice = 23.45m, AmountIn = 23.45m, CurrencyIn = "GBP", Status = "Valid" };
        db.Orders.Add(new Order { Id = harness.Intent.OrderId, TenantId = tenantId, PayerPartyId = harness.Intent.PayerPartyId,
            OrderType = "ProductPurchase", Status = "Draft", AmountIn = 23.45m, CurrencyIn = "GBP", Items = [target] });
        await db.SaveChangesAsync();
        var binding = new GiftCardLedgerBinding(ledger.Id, cash.Id, clearing.Id, liability.Id);
        harness.Settings.Setup(s => s.GetTenantValueAsync(GiftCardSettings.Policy, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.Serialize(new GiftCardPolicy(true, "native-test", "GBP", binding,
                new(true), "terms-v1", GiftCardSettings.Proportional), new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var gifts = scope.ServiceProvider.GetRequiredService<GiftCardService>();
        var policy = await gifts.GetPolicyAsync();
        var purchaseAttempt = Guid.NewGuid();
        var cartId = Guid.NewGuid();
        var purchase = new GiftCardCheckout(cartId, policy.Version, binding, policy.Validity!, policy.TermsVersion!, policy.FundingAllocation!,
            Purchase: new(0, item.Id, 23.45m));
        var request = new CreateCommerceGuestPaymentIntentRequest(purchaseId, 23.45m, "GBP", "Stripe", "Card", null, null,
            purchaseAttempt, $"gift-purchase:{purchaseAttempt:N}", GiftCard: purchase);
        await CreateGiftAsync(harness, request);
        await CaptureGiftAsync(harness, harness.Gateway.Requests.Single());
        var secret = await gifts.GetFulfilmentSecretAsync(new(cartId, purchaseId, purchaseAttempt, item.Id, 0));
        var checkout = await QuoteGiftAsync(harness, secret.Code, target.Id, amount);
        harness.Gateway.Requests.Clear();
        return new(secret.Code, request with { OrderId = harness.Intent.OrderId, PaymentIntentId = harness.Intent.Id,
            IdempotencyKey = $"gift:{harness.Intent.Id:N}", GiftCard = checkout });
    }

    private sealed record GiftSetup(string Code, CreateCommerceGuestPaymentIntentRequest Request);

    private sealed class PauseGiftCompletion : SaveChangesInterceptor
    {
        private int _paused;
        public bool Armed { get; set; }
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Armed && eventData.Context!.ChangeTracker.Entries<PaymentIntent>().Any(e => e.State == EntityState.Modified
                && e.Entity.ProviderCode == "GiftCard" && e.Entity.Status == "Captured") && Interlocked.CompareExchange(ref _paused, 1, 0) == 0)
            {
                Reached.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return result;
        }
    }
}
