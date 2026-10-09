using System.Text.Json;

using Aonik.Finance.Contracts.Models.Payments;
using Aonik.Finance.Contracts.Services.Payments;
using Aonik.Finance.Entities.Billing;
using Aonik.Finance.Entities.Ledger;
using Aonik.Finance.Entities.Orders;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.GiftCards;
using Aonik.Finance.Services.Ledger;
using Aonik.Finance.Services.Loyalty;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.GiftCards;
using Aonik.SharedKernel.Abstractions.Ledgers;
using Aonik.SharedKernel.Abstractions.Loyalty;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Events.Outbox;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Aonik.Application.Tests.Payments;

public sealed class CheckoutGiftCardPaymentTests
{
    [Fact]
    public async Task MixedGiftAndPoints_Should_EarnOnlyOnActualCashFundedGoods()
    {
        using var test = new CheckoutPaymentTestHarness();
        await test.SeedOrderAsync();
        var gift = await SeedGiftAsync(test, 20m);
        var loyalty = await SeedRewardInstructionAsync(test, gift.Checkout);
        test.Gateway.Requests.Clear();
        await test.RealService.CreateAsync(test.Request with { GiftCard = gift.Checkout, Loyalty = loyalty });
        await CaptureAsync(test, test.Gateway.Requests.Single());
        await using var scope = test.NewScope();
        (await scope.ServiceProvider.GetRequiredService<LoyaltyService>().GetBalanceAsync(test.PayerId)).BalancePoints.Should().Be(45);
    }

    [Fact]
    public async Task Checkout_Should_RejectRewardSnapshotThatIgnoresGiftFunding()
    {
        using var test = new CheckoutPaymentTestHarness();
        await test.SeedOrderAsync();
        var gift = await SeedGiftAsync(test, 20m);
        var loyalty = await SeedRewardInstructionAsync(test, gift.Checkout);
        loyalty = loyalty with { EarnedPoints = 85, Lines = [loyalty.Lines.Single() with
            { GiftFundedValue = 0m, NetPaidValue = 42.50m, EligibleEarnValue = 42.50m, EarnedPoints = 85 }] };
        test.Gateway.Requests.Clear();
        var create = () => test.RealService.CreateAsync(test.Request with { GiftCard = gift.Checkout, Loyalty = loyalty });
        await create.Should().ThrowAsync<InvalidStateException>().WithMessage("*reward calculation*");
        test.Gateway.Requests.Should().BeEmpty();
        (await test.Db.PaymentIntents.AnyAsync(p => p.Id == test.AttemptId)).Should().BeFalse();
        await using var scope = test.NewScope();
        (await scope.ServiceProvider.GetRequiredService<GiftCardService>().GetBalanceAsync(gift.Code))!.Reserved.Should().Be(0m);
    }

    [Fact]
    public async Task RewardPolicyRejection_Should_CancelAndReleaseAlreadyPreparedGiftTender()
    {
        using var test = new CheckoutPaymentTestHarness();
        await test.SeedOrderAsync();
        var gift = await SeedGiftAsync(test, 20m);
        var loyalty = await SeedRewardInstructionAsync(test, gift.Checkout);
        test.Settings.Setup(s => s.GetTenantValueAsync(LoyaltySettings.Policy, test.TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        test.Gateway.Requests.Clear();
        var response = await test.RealService.CreateAsync(test.Request with { GiftCard = gift.Checkout, Loyalty = loyalty });
        response.Status.Should().Be("Cancelled");
        test.Gateway.Requests.Should().BeEmpty();
        await using var scope = test.NewScope();
        var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        (await db.GiftCardCheckoutAttempts.SingleAsync(a => a.PaymentIntentId == test.AttemptId)).Status.Should().Be("Released");
        (await db.LoyaltyCheckoutAttempts.SingleAsync(a => a.PaymentIntentId == test.AttemptId)).Status.Should().Be("Released");
        (await scope.ServiceProvider.GetRequiredService<GiftCardService>().GetBalanceAsync(gift.Code))!.Reserved.Should().Be(0m);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(42.50)]
    public async Task Checkout_Should_ProveExactGiftAndCashFunding_AndCompleteOnce(decimal giftAmount)
    {
        using var test = new CheckoutPaymentTestHarness();
        await test.SeedOrderAsync();
        var setup = await SeedGiftAsync(test, giftAmount);
        test.Gateway.Requests.Clear();
        var response = await test.RealService.CreateAsync(test.Request with { GiftCard = setup.Checkout });
        if (giftAmount < test.Request.Amount)
        {
            response.Status.Should().Be("Pending");
            test.Gateway.Requests.Should().ContainSingle().Which.Amount.Should().Be(42.50m - giftAmount);
            await CaptureAsync(test, test.Gateway.Requests.Single());
        }
        else
        {
            response.Status.Should().Be("Captured");
            response.Provider.Should().Be("GiftCard");
            response.ProviderReference.Should().BeEmpty();
            test.Gateway.Requests.Should().BeEmpty();
        }
        await test.RealService.CreateAsync(test.Request with { GiftCard = setup.Checkout });
        await using var scope = test.NewScope();
        var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var receipts = await db.Payments.Where(p => p.PaymentIntentId == test.AttemptId).ToListAsync();
        receipts.Sum(p => p.Amount).Should().Be(42.50m);
        receipts.Single(p => p.Provider == "GiftCard").Amount.Should().Be(giftAmount);
        receipts.Where(p => p.Provider == "Stripe").Sum(p => p.Amount).Should().Be(42.50m - giftAmount);
        var intent = await db.PaymentIntents.SingleAsync(p => p.Id == test.AttemptId);
        intent.Amount.Should().Be(42.50m);
        intent.Status.Should().Be("Captured");
        var cash = await db.JournalEntries.Where(e => e.SourceType == "PaymentCapture" && e.SourceId == test.AttemptId)
            .SelectMany(e => e.Lines).Where(l => l.Direction == JournalDirections.Debit).SumAsync(l => l.Amount);
        cash.Should().Be(42.50m - giftAmount);
        (await scope.ServiceProvider.GetRequiredService<GiftCardService>().GetBalanceAsync(setup.Code))!.Balance.Should().Be(100m - giftAmount);
        var events = await db.Set<OutboxMessage>().Where(e => e.EventType == typeof(PaymentCompletedEvent).FullName).ToListAsync();
        events.Select(e => JsonSerializer.Deserialize<PaymentCompletedEvent>(e.Payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)))
            .Where(e => e!.PaymentId == test.AttemptId).Should().ContainSingle().Which!.Amount.Should().Be(42.50m);
    }

    [Fact]
    public async Task MixedCheckout_Should_RetainGiftUntilConfirmedUnpaidClosure()
    {
        using var test = new CheckoutPaymentTestHarness();
        await test.SeedOrderAsync();
        var setup = await SeedGiftAsync(test, 20m);
        test.Gateway.Requests.Clear();
        await test.RealService.CreateAsync(test.Request with { GiftCard = setup.Checkout });
        var request = test.Gateway.Requests.Single();
        var snapshot = test.Gateway.Result(request).Checkout!;
        await test.RealReconciler.ApplyAsync(test.AttemptId, snapshot with { Status = "Processing", CheckoutUrl = null });
        await using (var scope = test.NewScope())
            (await scope.ServiceProvider.GetRequiredService<GiftCardService>().GetBalanceAsync(setup.Code))!.Reserved.Should().Be(20m);
        await test.RealReconciler.ApplyAsync(test.AttemptId, snapshot with { Status = "Cancelled", CanNoLongerPay = true, CheckoutUrl = null });
        await using var verify = test.NewScope();
        var balance = await verify.ServiceProvider.GetRequiredService<GiftCardService>().GetBalanceAsync(setup.Code);
        balance!.Balance.Should().Be(100m);
        balance.Reserved.Should().Be(0m);
        (await verify.ServiceProvider.GetRequiredService<FinanceDbContext>().Payments.CountAsync(p => p.PaymentIntentId == test.AttemptId)).Should().Be(0);
    }

    [Fact]
    public async Task Checkout_Should_RejectChangedOrRemovedFrozenTender()
    {
        using var test = new CheckoutPaymentTestHarness();
        await test.SeedOrderAsync();
        var setup = await SeedGiftAsync(test, 20m);
        var request = test.Request with { GiftCard = setup.Checkout };
        await test.RealService.CreateAsync(request);
        var changed = () => test.RealService.CreateAsync(request with { GiftCard = setup.Checkout with
            { Tender = setup.Checkout.Tender! with { Amount = 19m, ExpectedCardAmount = 23.50m } } });
        await changed.Should().ThrowAsync<InvalidStateException>();
        var removed = () => test.RealService.CreateAsync(request with { GiftCard = null });
        await removed.Should().ThrowAsync<InvalidStateException>();
    }

    [Fact]
    public async Task ExpiredFirstGiftCheckout_Should_CloseWithoutProviderOrLivePolicy()
    {
        using var test = new CheckoutPaymentTestHarness();
        await test.SeedOrderAsync();
        var setup = await SeedGiftAsync(test, 42.50m);
        test.Gateway.Requests.Clear();
        test.Connectors.Invocations.Clear();
        test.Settings.Setup(s => s.GetTenantValueAsync(GiftCardSettings.Policy, test.TenantId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Policy must not be needed for closure"));
        var result = await test.RealService.CreateAsync(test.Request with
            { GiftCard = setup.Checkout, ProviderStartDeadlineUtc = test.Now.AddSeconds(-1) });
        result.Status.Should().Be("Cancelled");
        test.Gateway.Requests.Should().BeEmpty();
        test.Connectors.Verify(c => c.ResolveSelectedAsync(It.IsAny<CancellationToken>()), Times.Never);
        (await test.Db.GiftCardCheckoutAttempts.AsNoTracking().SingleAsync(a => a.PaymentIntentId == test.AttemptId)).Status.Should().Be("Released");
    }

    [Fact]
    public async Task MixedCheckout_Should_RejectFullSaleAmountAsExternalCaptureProof()
    {
        using var test = new CheckoutPaymentTestHarness();
        await test.SeedOrderAsync();
        var setup = await SeedGiftAsync(test, 20m);
        test.Gateway.Requests.Clear();
        await test.RealService.CreateAsync(test.Request with { GiftCard = setup.Checkout });
        var snapshot = test.Gateway.Result(test.Gateway.Requests.Single()).Checkout!;
        var apply = () => test.RealReconciler.ApplyAsync(test.AttemptId, snapshot with
            { Status = "Captured", Amount = 42.50m, ReceivedAmount = 42.50m, ProviderPaymentIntentId = "pi_wrong" });
        await apply.Should().ThrowAsync<InvalidStateException>();
        (await test.Db.Payments.CountAsync(p => p.PaymentIntentId == test.AttemptId)).Should().Be(0);
    }

    [Fact]
    public async Task GiftInvoiceSettlement_Should_NotRecognizeIssuedValueAgain()
    {
        using var test = new CheckoutPaymentTestHarness();
        await test.SeedOrderAsync();
        var setup = await SeedGiftAsync(test, 20m);
        await using var scope = test.NewScope();
        var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var invoice = new Invoice { TenantId = test.TenantId, OrderId = setup.Source.OrderId, Total = 100m, Currency = "GBP" };
        var poster = scope.ServiceProvider.GetRequiredService<LedgerPostingService>();
        await poster.PostInvoiceSettlementAsync(invoice);
        await poster.PostInvoiceSettlementAsync(invoice);
        var journal = await db.JournalEntries.Include(e => e.Lines).SingleAsync(e => e.SourceType == "InvoiceSettlement" && e.SourceId == invoice.Id);
        journal.Lines.Should().HaveCount(2).And.OnlyContain(l => l.LedgerAccountId == setup.Checkout.Ledger.ClearingAccountId);
        journal.Lines.Where(l => l.Direction == "Debit").Sum(l => l.Amount).Should().Be(100m);
        journal.Lines.Where(l => l.Direction == "Credit").Sum(l => l.Amount).Should().Be(100m);
    }

    private static async Task CaptureAsync(CheckoutPaymentTestHarness test, PaymentProviderIntentRequest request)
    {
        var snapshot = test.Gateway.Result(request).Checkout!;
        await test.RealReconciler.ApplyAsync(request.PaymentIntentId, snapshot with
        {
            Status = "Captured", ReceivedAmount = request.Amount, CheckoutUrl = null,
            ProviderPaymentIntentId = "pi_" + request.PaymentIntentId.ToString("N")
        });
    }

    private static async Task<SeededGift> SeedGiftAsync(CheckoutPaymentTestHarness test, decimal requestedAmount)
    {
        var ledger = new Aonik.Finance.Entities.Ledger.Ledger { TenantId = test.TenantId, BaseCurrency = "GBP", IsCanonical = true };
        var cash = new LedgerAccount { TenantId = test.TenantId, LedgerId = ledger.Id, Code = "1000", Name = "Cash", AccountType = "Asset" };
        var clearing = new LedgerAccount { TenantId = test.TenantId, LedgerId = ledger.Id, Code = "2100", Name = "Clearing", AccountType = "Liability" };
        var liability = new LedgerAccount { TenantId = test.TenantId, LedgerId = ledger.Id, Code = "2202", Name = "Gift liability", AccountType = "Liability" };
        test.Db.Ledgers.Add(ledger);
        test.Db.LedgerAccounts.AddRange(cash, clearing, liability);
        var purchaseId = Guid.NewGuid();
        var item = new OrderItem { TenantId = test.TenantId, OrderId = purchaseId, ItemIndex = 0,
            ItemType = "GiftCardValue", Quantity = 1, UnitPrice = 100m, AmountIn = 100m, CurrencyIn = "GBP", Status = "Valid" };
        test.Db.Orders.Add(new Order { Id = purchaseId, TenantId = test.TenantId, PayerPartyId = test.PayerId,
            OrderType = "ProductPurchase", Status = "Draft", CurrencyIn = "GBP", AmountIn = 100m, Items = [item] });
        var target = new OrderItem { TenantId = test.TenantId, OrderId = test.OrderId, ItemIndex = 0,
            ItemType = "ProductPurchase", Quantity = 1, UnitPrice = 42.50m, AmountIn = 42.50m, CurrencyIn = "GBP", Status = "Valid" };
        test.Db.OrderItems.Add(target);
        await test.Db.SaveChangesAsync();
        test.Db.ChangeTracker.Clear();
        var binding = new GiftCardLedgerBinding(ledger.Id, cash.Id, clearing.Id, liability.Id);
        test.Settings.Setup(s => s.GetTenantValueAsync(GiftCardSettings.Policy, test.TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.Serialize(new GiftCardPolicy(true, "test", "GBP", binding,
                new(true), "terms-v1", GiftCardSettings.Proportional), new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        await using var scope = test.NewScope();
        var gifts = scope.ServiceProvider.GetRequiredService<GiftCardService>();
        var policy = await gifts.GetPolicyAsync();
        var id = Guid.NewGuid();
        var cart = Guid.NewGuid();
        var purchase = new GiftCardCheckout(cart, policy.Version, binding, policy.Validity!, policy.TermsVersion!, policy.FundingAllocation!,
            Purchase: new(0, item.Id, 100m));
        await test.RealService.CreateAsync(test.Request with { OrderId = purchaseId, PaymentIntentId = id,
            Amount = 100m, IdempotencyKey = $"gift-seed:{id:N}", GiftCard = purchase });
        await CaptureAsync(test, test.Gateway.Requests.Last());
        var source = new GiftCardPurchaseSource(cart, purchaseId, id, item.Id, 0);
        var secret = await gifts.GetFulfilmentSecretAsync(source);
        var targetCart = Guid.NewGuid();
        var authorization = await gifts.AuthorizeForCartAsync(secret.Code, targetCart);
        var quote = await gifts.QuoteAsync(new(targetCart, authorization.PrivateCartGrant!, "GBP", 42.50m, 0,
            requestedAmount, [new(0, target.Id, "ProductPurchase", 42.50m, 0, 0)]));
        quote.ReasonCode.Should().BeNull();
        return new(secret.Code, quote.Checkout!, source);
    }

    private sealed record SeededGift(string Code, GiftCardCheckout Checkout, GiftCardPurchaseSource Source);

    private static async Task<LoyaltyCheckout> SeedRewardInstructionAsync(CheckoutPaymentTestHarness test, GiftCardCheckout gift)
    {
        var liability = new LedgerAccount { TenantId = test.TenantId, LedgerId = gift.Ledger.LedgerId,
            Code = "2201", Name = "Reward liability", AccountType = "Liability" };
        var expense = new LedgerAccount { TenantId = test.TenantId, LedgerId = gift.Ledger.LedgerId,
            Code = "6201", Name = "Reward expense", AccountType = "Expense" };
        test.Db.LedgerAccounts.AddRange(liability, expense);
        await test.Db.SaveChangesAsync();
        var binding = new LoyaltyLedgerBinding(gift.Ledger.LedgerId, liability.Id, expense.Id, expense.Id);
        test.Settings.Setup(s => s.GetTenantValueAsync(LoyaltySettings.Policy, test.TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.Serialize(new LoyaltyPolicy(true, "test", binding), new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        await using var scope = test.NewScope();
        var policy = await scope.ServiceProvider.GetRequiredService<LoyaltyService>().GetPolicyAsync();
        var line = gift.Tender!.Lines.Single();
        return new(gift.CartId, test.PayerId, false, policy.Version, binding, 0, 45, 0m, 42.50m,
            [new(line.ItemIndex, line.OrderItemId, line.ItemType, null, 42.50m, 0m, 0m, 20m, 22.50m, 22.50m, 45, 0, true, true)], 42.50m);
    }
}
