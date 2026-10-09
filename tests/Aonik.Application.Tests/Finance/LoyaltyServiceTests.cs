using System.Text.Json;

using Aonik.Finance.Entities;
using Aonik.Finance.Entities.Ledger;
using Aonik.Finance.Entities.Orders;
using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Ledger;
using Aonik.Finance.Services.Loyalty;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Loyalty;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.SharedKernel.Events.Integration;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

using LedgerEntity = Aonik.Finance.Entities.Ledger.Ledger;

namespace Aonik.Application.Tests.Finance;

public sealed class LoyaltyServiceTests
{
    [Fact]
    public async Task Policy_Should_DefaultDisabled_AndFingerprintChangesWithoutVersionLabelChange()
    {
        using var h = await Harness.CreateAsync();
        var original = await h.Service.GetPolicyAsync();
        h.Configure(new LoyaltyPolicy(true, "v1", h.Binding, EarnExcludedProductIds: [Guid.NewGuid()]));
        (await h.Service.GetPolicyAsync()).Version.Should().NotBe(original.Version);
        h.Raw = null;
        (await h.Service.GetPolicyAsync()).Enabled.Should().BeFalse();
        h.Settings.Verify(x => x.GetTenantValueAsync(LoyaltySettings.Policy, h.TenantId, It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown-field")]
    [InlineData("foreign-account")]
    [InlineData("wrong-type")]
    public async Task Policy_Should_FailClosedOnInvalidConfiguration(string defect)
    {
        using var h = await Harness.CreateAsync();
        switch (defect)
        {
            case "malformed": h.Raw = "{"; break;
            case "unknown-field": h.Raw = """{"enabled":true,"version":"v1","secretFallback":"yes"}"""; break;
            case "foreign-account":
                h.Configure(new(true, "v1", h.Binding with { LiabilityAccountId = Guid.NewGuid() })); break;
            case "wrong-type":
                (await h.Db.LedgerAccounts.SingleAsync(x => x.Id == h.Binding.LiabilityAccountId)).AccountType = "Asset";
                await h.Db.SaveChangesAsync(); break;
        }
        await h.Service.Invoking(s => s.GetPolicyAsync()).Should().ThrowAsync<InvalidStateException>();
        h.Db.JournalEntries.Should().BeEmpty();
    }

    [Fact]
    public async Task Adjust_Should_PostBalancedOnce_AndReplayAfterPolicyRemoval()
    {
        using var h = await Harness.CreateAsync();
        var command = new LoyaltyAdjustment(h.PartyId, Guid.NewGuid(), 725, " Service recovery ");
        var result = await h.Service.AdjustAsync(command);
        h.Raw = null;

        (await h.Service.AdjustAsync(command)).Should().Be(result);
        await h.Service.Invoking(s => s.AdjustAsync(command with { Points = 726 })).Should().ThrowAsync<InvalidStateException>();

        var entries = await h.Db.JournalEntries.Include(x => x.Lines).ToListAsync();
        entries.Should().ContainSingle();
        entries[0].Lines.Should().HaveCount(2);
        entries[0].Lines.Where(x => x.Direction == "Debit").Sum(x => x.Amount).Should().Be(7.25m);
        entries[0].Lines.Where(x => x.Direction == "Credit").Sum(x => x.Amount).Should().Be(7.25m);
        (await h.Service.GetBalanceAsync(h.PartyId)).BalancePoints.Should().Be(725);
    }

    [Fact]
    public async Task Balance_Should_UseActualJournalLeg_NotOperationMetadataOrOtherOwners()
    {
        using var h = await Harness.CreateAsync();
        await h.FundAsync(400);
        var operation = await h.Db.LoyaltyOperations.SingleAsync();
        operation.Points = 99999; // A projection value is not financial authority.
        await h.Db.SaveChangesAsync();

        (await h.Service.GetBalanceAsync(h.PartyId)).Should().Be(new LoyaltyBalance(400, 0, 400, 4m, 0));
        (await h.Service.GetBalanceAsync(Guid.NewGuid())).BalancePoints.Should().Be(0);
        var other = new LoyaltyService(h.Db, new TestTenantProvider(Guid.NewGuid()), h.Clock, h.Settings.Object,
            new JournalWriter(h.Db, new TestTenantProvider(Guid.NewGuid()), h.Clock));
        (await other.GetBalanceAsync(h.PartyId)).BalancePoints.Should().Be(0);
    }

    [Fact]
    public async Task Activity_Should_ShowActualRunningBalancesAcrossPages_AndMonotonicSeenMarker()
    {
        using var h = await Harness.CreateAsync();
        await h.FundAsync(600);
        h.Clock.UtcNow = h.Clock.UtcNow.AddSeconds(1);
        await h.FundAsync(-200);
        h.Clock.UtcNow = h.Clock.UtcNow.AddSeconds(1);
        await h.FundAsync(-500);

        var first = await h.Service.GetActivityAsync(h.PartyId, 1, 2);
        var second = await h.Service.GetActivityAsync(h.PartyId, 2, 2);
        first.Items.Select(x => x.Points).Should().Equal(-500, -200);
        first.Items.Select(x => x.RunningBalancePoints).Should().Equal(-100, 400);
        second.Items.Single().RunningBalancePoints.Should().Be(600);
        first.TotalCount.Should().Be(3);
        (await h.Service.MarkSeenAsync(h.PartyId, 2)).HighestFivePoundMarkSeen.Should().Be(2);
        var marked = await h.Service.MarkSeenAsync(h.PartyId, 1);
        marked.HighestFivePoundMarkSeen.Should().Be(2);
        marked.BalancePoints.Should().Be(-100);
        marked.AvailablePoints.Should().Be(0);
        await h.Service.Invoking(s => s.MarkSeenAsync(h.PartyId, -1)).Should().ThrowAsync<InvalidStateException>();
    }

    [Fact]
    public async Task Prepare_Should_ReserveAvailablePoints_AndReleaseOnlyConfirmedCancellation()
    {
        using var h = await Harness.CreateAsync();
        await h.FundAsync(100);
        var (intent, instruction) = await h.CheckoutAsync(redeemed: 100);
        (await h.Service.PrepareTrackedAsync(intent, instruction)).Should().BeNull();
        await h.Db.SaveChangesAsync();
        var (other, second) = await h.CheckoutAsync(redeemed: 1);

        (await h.Service.PrepareTrackedAsync(other, second)).Should().Be("loyalty.insufficient_points");
        await h.Db.SaveChangesAsync();
        (await h.Service.GetBalanceAsync(h.PartyId)).ReservedPoints.Should().Be(100);
        await h.Service.Invoking(s => s.ReleaseTrackedAsync(intent)).Should().ThrowAsync<InvalidStateException>();
        intent.Status = "Cancelled";
        await h.Service.ReleaseTrackedAsync(intent);
        await h.Db.SaveChangesAsync();
        (await h.Service.GetBalanceAsync(h.PartyId)).AvailablePoints.Should().Be(100);
    }

    [Fact]
    public async Task Prepare_Should_FreezeExpiredRequest_WithoutReadingCurrentPolicy()
    {
        using var h = await Harness.CreateAsync();
        var (intent, instruction) = await h.CheckoutAsync();
        h.Settings.Invocations.Clear();
        h.Raw = "{";
        intent.Status = "Cancelled";
        (await h.Service.PrepareTrackedAsync(intent, instruction)).Should().BeNull();
        await h.Db.SaveChangesAsync();

        await h.Service.ValidateReplayAsync(intent, instruction);
        await h.Service.Invoking(s => s.ValidateReplayAsync(intent, instruction with { PayableTotal = 1m })).Should().ThrowAsync<InvalidStateException>();
        await h.Service.Invoking(s => s.ValidateReplayAsync(intent, null)).Should().ThrowAsync<InvalidStateException>();
        h.Settings.Invocations.Should().BeEmpty();
        (await h.Db.LoyaltyCheckoutAttempts.SingleAsync()).Status.Should().Be("Released");
        h.Db.LoyaltyAccounts.Should().BeEmpty();
    }

    [Fact]
    public async Task Complete_Should_UseFrozenPolicy_AndNeverAwardOrRedeemTwice()
    {
        using var h = await Harness.CreateAsync();
        await h.FundAsync(100);
        var (intent, instruction) = await h.CheckoutAsync(redeemed: 100);
        await h.Service.PrepareTrackedAsync(intent, instruction);
        await h.Db.SaveChangesAsync();
        h.Raw = null;

        intent.Status = "Captured";
        await h.Service.CompleteTrackedAsync(intent);
        await h.Db.SaveChangesAsync();
        await h.Service.CompleteTrackedAsync(intent);
        await h.Db.SaveChangesAsync();

        (await h.Service.GetBalanceAsync(h.PartyId)).Should().Be(new LoyaltyBalance(198, 0, 198, 1.98m, 0));
        h.Db.LoyaltyOperations.Should().HaveCount(3);
        h.Db.JournalEntries.Should().HaveCount(3);
    }

    [Theory]
    [InlineData("item-id")]
    [InlineData("index")]
    [InlineData("type")]
    [InlineData("amount")]
    [InlineData("payer")]
    public async Task Prepare_Should_RejectLineOrOwnerFactsNotInTheOrder(string defect)
    {
        using var h = await Harness.CreateAsync();
        var (intent, instruction) = await h.CheckoutAsync();
        var item = await h.Db.OrderItems.SingleAsync();
        switch (defect)
        {
            case "item-id": instruction = instruction with { Lines = [instruction.Lines[0] with { OrderItemId = Guid.NewGuid() }] }; break;
            case "index": item.ItemIndex++; break;
            case "type": item.ItemType = "Delivery"; break;
            case "amount": item.AmountIn++; break;
            case "payer": intent.PayerPartyId = Guid.NewGuid(); break;
        }
        await h.Db.SaveChangesAsync();
        await h.Service.Invoking(s => s.PrepareTrackedAsync(intent, instruction)).Should().ThrowAsync<InvalidStateException>();
        h.Db.LoyaltyCheckoutAttempts.Should().BeEmpty();
    }

    [Fact]
    public async Task Prepare_Should_AcceptFourDecimalLineAllocation_AndRecalculatedTax()
    {
        using var h = await Harness.CreateAsync();
        await h.FundAsync(1);
        var (intent, instruction) = await h.CheckoutAsync(gross: 1m);
        var first = await h.Db.OrderItems.SingleAsync();
        first.AmountIn = .5m;
        var second = new OrderItem { TenantId = h.TenantId, OrderId = intent.OrderId, ItemIndex = 1, ItemType = first.ItemType, AmountIn = .5m, CurrencyIn = "GBP" };
        h.Db.OrderItems.Add(second);
        intent.Amount = 1.19m; // £0.99 net plus rounded VAT; the cap used £1.20 pre-benefit total.
        instruction = instruction with
        {
            RedeemedPoints = 1, EarnedPoints = 1, PointsAppliedValue = .01m, OrderValueBeforePoints = 1.20m, PayableTotal = intent.Amount,
            Lines =
            [
                new(0, first.Id, first.ItemType, null, .5m, 0, .005m, 0, .495m, .495m, 1, 1, true, true),
                new(1, second.Id, second.ItemType, null, .5m, 0, .005m, 0, .495m, .495m, 0, 0, true, true)
            ]
        };
        await h.Db.SaveChangesAsync();
        (await h.Service.PrepareTrackedAsync(intent, instruction)).Should().BeNull();
        await h.Db.SaveChangesAsync();
        (await h.Service.GetBalanceAsync(h.PartyId)).ReservedPoints.Should().Be(1);
    }

    [Fact]
    public async Task Refund_Should_CapCumulativeOriginalLineAllocations_AndReplayExactly()
    {
        using var h = await Harness.CreateAsync();
        await h.FundAsync(100);
        var (intent, instruction) = await h.CaptureAsync(redeemed: 100);
        var refund = new LoyaltyRefund(Guid.NewGuid(), intent.OrderId, intent.Id, [new(instruction.Lines[0].OrderItemId, 99, 50)]);

        await h.Service.ReverseRefundAsync(refund);
        await h.Service.ReverseRefundAsync(refund);
        (await h.Service.GetBalanceAsync(h.PartyId)).BalancePoints.Should().Be(149);
        await h.Service.Invoking(s => s.ReverseRefundAsync(refund with { Lines = [new(instruction.Lines[0].OrderItemId, 100, 50)] }))
            .Should().ThrowAsync<InvalidStateException>();
        await h.Service.Invoking(s => s.ReverseRefundAsync(refund with { RefundId = Guid.NewGuid(), Lines = [new(instruction.Lines[0].OrderItemId, 100, 51)] }))
            .Should().ThrowAsync<InvalidStateException>();
        (await h.Service.GetBalanceAsync(h.PartyId)).BalancePoints.Should().Be(149);
        h.Db.LoyaltyOperations.Where(x => x.Kind == "EarnReverse").Should().ContainSingle();
    }

    [Fact]
    public async Task GuestClaim_Should_TransferRemainingAwardOnce_AndReverseLaterRefundOnVerifiedOwner()
    {
        using var h = await Harness.CreateAsync();
        var (intent, instruction) = await h.CaptureAsync(guest: true);
        var originalJournalIds = await h.Db.JournalEntries.Select(x => x.Id).ToArrayAsync();
        var accountParty = await h.AddPartyAsync();
        var refund = new LoyaltyRefund(Guid.NewGuid(), intent.OrderId, intent.Id, [new(instruction.Lines[0].OrderItemId, 50, 0)]);
        await h.Service.ReverseRefundAsync(refund);
        var verified = new AccountAccessVerifiedEvent(h.TenantId, Guid.NewGuid(), instruction.CartId, intent.OrderId, intent.Id, h.PartyId, accountParty);

        await h.Service.AttachVerifiedGuestAsync(verified);
        await h.Service.AttachVerifiedGuestAsync(verified with { ActionId = Guid.NewGuid() });
        (await h.Service.GetBalanceAsync(h.PartyId)).BalancePoints.Should().Be(0);
        (await h.Service.GetBalanceAsync(accountParty)).BalancePoints.Should().Be(150);
        await h.Service.ReverseRefundAsync(refund with { RefundId = Guid.NewGuid(), Lines = [new(instruction.Lines[0].OrderItemId, 100, 0)] });

        (await h.Service.GetBalanceAsync(accountParty)).BalancePoints.Should().Be(50);
        (await h.Service.GetBalanceAsync(h.PartyId)).BalancePoints.Should().Be(0);
        h.Db.JournalEntries.Select(x => x.Id).Should().Contain(originalJournalIds);
        (await h.Db.PaymentIntents.SingleAsync()).PayerPartyId.Should().Be(h.PartyId);
        (await h.Db.LoyaltyOperations.SingleAsync(x => x.Kind == "ClaimOut")).DetailsJson.Should().Contain(verified.ActionId.ToString());
        await h.Service.Invoking(s => s.AttachVerifiedGuestAsync(verified with { AccountPartyId = Guid.NewGuid() })).Should().ThrowAsync<InvalidStateException>();
    }

    [Fact]
    public async Task GuestClaim_Should_NoOpHistoricalPayment_ButRetryMissingExpectedAward()
    {
        using var h = await Harness.CreateAsync();
        var account = await h.AddPartyAsync();
        var (intent, instruction) = await h.CheckoutAsync(guest: true);
        var verified = new AccountAccessVerifiedEvent(h.TenantId, Guid.NewGuid(), instruction.CartId, intent.OrderId, intent.Id, h.PartyId, account);
        await h.Service.AttachVerifiedGuestAsync(verified);
        h.Db.LoyaltyOperations.Should().BeEmpty();
        await h.Service.PrepareTrackedAsync(intent, instruction);
        await h.Db.SaveChangesAsync();
        await h.Service.Invoking(s => s.AttachVerifiedGuestAsync(verified)).Should().ThrowAsync<InvalidStateException>();
        h.Db.JournalEntries.Should().BeEmpty();
    }

    private sealed class Harness : IDisposable
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid PartyId { get; } = Guid.NewGuid();
        public TestClock Clock { get; } = new();
        public FinanceDbContext Db { get; }
        public Mock<ITenantSettingStore> Settings { get; } = new();
        public LoyaltyLedgerBinding Binding { get; } = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        public LoyaltyService Service { get; }
        public string? Raw { get; set; }

        private Harness()
        {
            var tenant = new TestTenantProvider(TenantId);
            Db = new FinanceDbContext(new DbContextOptionsBuilder<FinanceDbContext>().UseInMemoryDatabase($"Loyalty_{Guid.NewGuid()}").Options, tenant, null, Clock);
            Settings.Setup(x => x.GetTenantValueAsync(LoyaltySettings.Policy, TenantId, It.IsAny<CancellationToken>())).Returns(() => Task.FromResult(Raw));
            Service = new(Db, tenant, Clock, Settings.Object, new JournalWriter(Db, tenant, Clock));
        }

        public static async Task<Harness> CreateAsync()
        {
            var h = new Harness();
            h.Db.Parties.Add(new PartyReadModel { Id = h.PartyId, TenantId = h.TenantId, DisplayName = "Owner", Status = "Active" });
            h.Db.Ledgers.Add(new LedgerEntity { Id = h.Binding.LedgerId, TenantId = h.TenantId, BaseCurrency = "GBP" });
            h.Db.LedgerAccounts.AddRange(
                new LedgerAccount { Id = h.Binding.LiabilityAccountId, TenantId = h.TenantId, LedgerId = h.Binding.LedgerId, Code = "reward-liability", AccountType = "Liability" },
                new LedgerAccount { Id = h.Binding.EarnExpenseAccountId, TenantId = h.TenantId, LedgerId = h.Binding.LedgerId, Code = "earn-expense", AccountType = "Expense" },
                new LedgerAccount { Id = h.Binding.RedeemExpenseAccountId, TenantId = h.TenantId, LedgerId = h.Binding.LedgerId, Code = "redeem-expense", AccountType = "Expense" });
            await h.Db.SaveChangesAsync();
            h.Configure(new(true, "v1", h.Binding));
            return h;
        }

        public void Configure(LoyaltyPolicy policy) => Raw = JsonSerializer.Serialize(policy, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        public Task<LoyaltyOperationRef> FundAsync(long points) => Service.AdjustAsync(new(PartyId, Guid.NewGuid(), points, "Test adjustment"));
        public async Task<Guid> AddPartyAsync()
        {
            var party = new PartyReadModel { TenantId = TenantId, DisplayName = "Verified", Status = "Active" };
            Db.Parties.Add(party);
            await Db.SaveChangesAsync();
            return party.Id;
        }

        public async Task<(PaymentIntent Intent, LoyaltyCheckout Instruction)> CheckoutAsync(long redeemed = 0, bool guest = false, decimal gross = 100m)
        {
            var order = new Order { TenantId = TenantId, PayerPartyId = PartyId, OrderType = "ProductPurchase", CurrencyIn = "GBP", AmountIn = gross };
            var item = new OrderItem { TenantId = TenantId, OrderId = order.Id, ItemIndex = 0, ItemType = "ProductPurchase", AmountIn = gross, CurrencyIn = "GBP" };
            var intent = new PaymentIntent { TenantId = TenantId, OrderId = order.Id, PayerPartyId = PartyId, Currency = "GBP", Amount = gross - redeemed / 100m, Status = "Pending" };
            Db.Orders.Add(order);
            Db.OrderItems.Add(item);
            Db.PaymentIntents.Add(intent);
            await Db.SaveChangesAsync();
            var policy = await Service.GetPolicyAsync();
            var net = intent.Amount;
            var earned = (long)decimal.Floor(net * 2m);
            var instruction = new LoyaltyCheckout(Guid.NewGuid(), PartyId, guest, policy.Version, Binding, redeemed, earned, redeemed / 100m, gross,
                [new(0, item.Id, item.ItemType, null, gross, 0, redeemed / 100m, 0, net, net, earned, redeemed, true, true)], intent.Amount);
            return (intent, instruction);
        }

        public async Task<(PaymentIntent Intent, LoyaltyCheckout Instruction)> CaptureAsync(long redeemed = 0, bool guest = false)
        {
            var result = await CheckoutAsync(redeemed, guest);
            (await Service.PrepareTrackedAsync(result.Intent, result.Instruction)).Should().BeNull();
            await Db.SaveChangesAsync();
            result.Intent.Status = "Captured";
            await Service.CompleteTrackedAsync(result.Intent);
            await Db.SaveChangesAsync();
            return result;
        }

        public void Dispose() => Db.Dispose();
    }

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);
    }
}
