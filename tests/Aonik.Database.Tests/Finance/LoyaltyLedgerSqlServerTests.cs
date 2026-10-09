using System.Text.Json;

using Aonik.Finance.Entities.Ledger;
using Aonik.Finance.Entities.Loyalty;
using Aonik.Finance.Entities.Orders;
using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Ledger;
using Aonik.Finance.Services.Loyalty;
using Aonik.Infrastructure.Persistence;
using Aonik.IntegrationTests.Support;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Loyalty;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.SharedKernel.Events.Integration;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;

namespace Aonik.Database.Tests.Finance;

public class LoyaltyLedgerSqlServerTests(SqlLocalDbFixture database) : IClassFixture<SqlLocalDbFixture>
{
    private static readonly FixedClock Clock = new();

    [SkippableFact]
    public async Task DuplicateConcurrentAdjustments_Should_PostOneBalancedEntry_AndOneOwnerLeg()
    {
        var test = await SeedAsync();
        await using (var seed = Context(test))
            await Service(seed, test).AdjustAsync(new(test.PartyId, Guid.NewGuid(), 1000, "Initial award"));
        var command = new LoyaltyAdjustment(test.PartyId, Guid.NewGuid(), 50, "Operator correction");
        var barrier = new TwoWriters();
        await using var first = Context(test, barrier);
        await using var second = Context(test, barrier);

        var results = await Task.WhenAll(Service(first, test).AdjustAsync(command), Service(second, test).AdjustAsync(command));

        results[0].Id.Should().Be(results[1].Id);
        await using var verify = Context(test);
        (await Service(verify, test).GetBalanceAsync(test.PartyId)).BalancePoints.Should().Be(1050);
        (await verify.LoyaltyOperations.CountAsync()).Should().Be(2);
        var entries = await verify.JournalEntries.Include(row => row.Lines).ToListAsync();
        entries.Should().HaveCount(2);
        foreach (var entry in entries)
            entry.Lines.Where(line => line.Direction == "Debit").Sum(line => line.Amount)
                .Should().Be(entry.Lines.Where(line => line.Direction == "Credit").Sum(line => line.Amount));
        (await verify.LoyaltyAccounts.SingleAsync()).RowVersion.Should().HaveCount(8);
    }

    [SkippableFact]
    public async Task FailedOperationSave_Should_RollBackTheJournalAndOwner_AndDetachFailedWrites()
    {
        var test = await SeedAsync();
        await using var failing = Context(test, new RejectOperation());
        var action = () => Service(failing, test).AdjustAsync(new(test.PartyId, Guid.NewGuid(), 200, "Failed write"));

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("Injected operation failure");
        await failing.SaveChangesAsync();

        await using var verify = Context(test);
        (await verify.JournalEntries.CountAsync()).Should().Be(0);
        (await verify.JournalEntryLines.CountAsync()).Should().Be(0);
        (await verify.LoyaltyAccounts.CountAsync()).Should().Be(0);
        (await verify.LoyaltyOperations.CountAsync()).Should().Be(0);
    }

    [SkippableFact]
    public async Task CompetingGuestClaims_Should_TransferTheOriginalAwardOnlyOnce()
    {
        var test = await SeedAsync();
        var paid = await EarnAsync(test, isGuest: true);
        var otherParty = await AddPartyAsync(test);
        var firstEvent = Verified(test, paid, test.AccountPartyId);
        var secondEvent = Verified(test, paid, otherParty);
        await using var first = Context(test);
        await using var second = Context(test);

        var outcomes = await Task.WhenAll(TryClaimAsync(Service(first, test), firstEvent), TryClaimAsync(Service(second, test), secondEvent));

        outcomes.Count(value => value is null).Should().Be(1);
        outcomes.Single(value => value is not null).Should().BeOfType<InvalidStateException>();
        await using var verify = Context(test);
        var service = Service(verify, test);
        (await service.GetBalanceAsync(test.PartyId)).BalancePoints.Should().Be(0);
        var one = await service.GetBalanceAsync(test.AccountPartyId);
        var two = await service.GetBalanceAsync(otherParty);
        (one.BalancePoints + two.BalancePoints).Should().Be(200);
        (await verify.LoyaltyOperations.CountAsync(row => row.Kind == "ClaimIn")).Should().Be(1);
        (await verify.LoyaltyOperations.CountAsync(row => row.Kind == "ClaimOut")).Should().Be(1);
        var winner = outcomes[0] is null ? firstEvent : secondEvent;
        await service.AttachVerifiedGuestAsync(winner with { ActionId = Guid.NewGuid() });
        (await verify.LoyaltyOperations.CountAsync(row => row.Kind == "ClaimIn")).Should().Be(1);
    }

    [SkippableFact]
    public async Task RefundAfterClaimAndSpending_Should_ReverseTheSavedAward_AllowNegative_AndCapFurtherReversal()
    {
        var test = await SeedAsync();
        var paid = await EarnAsync(test, isGuest: true);
        await using var db = Context(test);
        var service = Service(db, test);
        await service.AttachVerifiedGuestAsync(Verified(test, paid, test.AccountPartyId));
        await service.AdjustAsync(new(test.AccountPartyId, Guid.NewGuid(), -150, "Reward already used"));
        var refund = new LoyaltyRefund(Guid.NewGuid(), paid.OrderId, paid.IntentId, [new(paid.LineId, 200, 0)]);

        await service.ReverseRefundAsync(refund);
        await service.ReverseRefundAsync(refund);

        var balance = await service.GetBalanceAsync(test.AccountPartyId);
        balance.BalancePoints.Should().Be(-150);
        balance.AvailablePoints.Should().Be(0);
        (await service.GetBalanceAsync(test.PartyId)).BalancePoints.Should().Be(0);
        var excess = () => service.ReverseRefundAsync(refund with { RefundId = Guid.NewGuid(), Lines = [new(paid.LineId, 1, 0)] });
        await excess.Should().ThrowAsync<InvalidStateException>();
        (await service.GetBalanceAsync(test.AccountPartyId)).BalancePoints.Should().Be(-150);
    }

    [SkippableFact]
    public async Task HistoryAndMilestones_Should_UseSignedJournalAmounts_AndKeepTheSeenMarkerMonotonic()
    {
        var test = await SeedAsync();
        await using var db = Context(test);
        var service = Service(db, test);
        await service.AdjustAsync(new(test.PartyId, Guid.NewGuid(), 1200, "Award"));
        await service.MarkSeenAsync(test.PartyId, 2);
        await service.AdjustAsync(new(test.PartyId, Guid.NewGuid(), -1100, "Redemption correction"));
        await service.MarkSeenAsync(test.PartyId, 1);

        var balance = await service.GetBalanceAsync(test.PartyId);
        var firstPage = await service.GetActivityAsync(test.PartyId, 1, 1);
        var secondPage = await service.GetActivityAsync(test.PartyId, 2, 1);

        balance.BalancePoints.Should().Be(100);
        balance.HighestFivePoundMarkSeen.Should().Be(2);
        firstPage.Items.Single().RunningBalancePoints.Should().Be(100);
        secondPage.Items.Single().RunningBalancePoints.Should().Be(100 - firstPage.Items.Single().Points);
        (firstPage.Items.Single().Points + secondPage.Items.Single().Points).Should().Be(100);
    }

    private async Task<Seed> SeedAsync()
    {
        Skip.IfNot(database.IsAvailable, database.SkipReason ?? "SQL Server unavailable.");
        var tenant = Guid.NewGuid();
        var binding = new LoyaltyLedgerBinding(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var test = new Seed(tenant, Guid.NewGuid(), Guid.NewGuid(), binding,
            JsonSerializer.Serialize(new LoyaltyPolicy(true, "sql-v1", binding), new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        await AddPartyAsync(test, test.PartyId);
        await AddPartyAsync(test, test.AccountPartyId);
        await using var db = Context(test);
        db.Ledgers.Add(new Ledger { Id = binding.LedgerId, TenantId = tenant, BaseCurrency = "GBP", IsCanonical = true });
        db.LedgerAccounts.AddRange(
            new LedgerAccount { Id = binding.LiabilityAccountId, TenantId = tenant, LedgerId = binding.LedgerId, Code = "LOY-L", Name = "Rewards liability", AccountType = "Liability" },
            new LedgerAccount { Id = binding.EarnExpenseAccountId, TenantId = tenant, LedgerId = binding.LedgerId, Code = "LOY-E", Name = "Rewards expense", AccountType = "Expense" },
            new LedgerAccount { Id = binding.RedeemExpenseAccountId, TenantId = tenant, LedgerId = binding.LedgerId, Code = "LOY-R", Name = "Rewards release", AccountType = "Expense" });
        await db.SaveChangesAsync();
        return test;
    }

    private async Task<Guid> AddPartyAsync(Seed test, Guid? id = null)
    {
        var partyId = id ?? Guid.NewGuid();
        await using var db = new AonikDbContext(database.CreateOptions<AonikDbContext>(), new TestTenantProvider(test.TenantId));
        db.Parties.Add(new Aonik.Platform.Entities.Party.Party { Id = partyId, TenantId = test.TenantId, PartyType = "Person", DisplayName = "Loyalty customer", Status = "Active" });
        await db.SaveChangesAsync();
        return partyId;
    }

    private async Task<Paid> EarnAsync(Seed test, bool isGuest)
    {
        await using var db = Context(test);
        var service = Service(db, test);
        var policy = await service.GetPolicyAsync();
        var order = new Order { TenantId = test.TenantId, OrderType = "ProductPurchase", PayerPartyId = test.PartyId, CurrencyIn = "GBP", AmountIn = 100, Status = "PendingFunding" };
        var line = new OrderItem { TenantId = test.TenantId, OrderId = order.Id, ItemIndex = 0, ItemType = "ProductPurchase", CurrencyIn = "GBP", AmountIn = 100 };
        var intent = new PaymentIntent { TenantId = test.TenantId, OrderId = order.Id, PayerPartyId = test.PartyId, Currency = "GBP", Amount = 100, Status = "Pending", ProviderCode = "Stripe", PaymentMethodType = "Card" };
        db.Orders.Add(order);
        db.OrderItems.Add(line);
        db.PaymentIntents.Add(intent);
        await db.SaveChangesAsync();
        var cartId = Guid.NewGuid();
        var snapshot = new LoyaltyCheckout(cartId, test.PartyId, isGuest, policy.Version, test.Binding, 0, 200, 0, 100,
            [new(0, line.Id, "ProductPurchase", null, 100, 0, 0, 0, 100, 100, 200, 0, true, true)], 100);
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            (await service.PrepareTrackedAsync(intent, snapshot)).Should().BeNull();
            await db.SaveChangesAsync();
            intent.Status = "Captured";
            await service.CompleteTrackedAsync(intent);
            db.Payments.Add(new Payment { TenantId = test.TenantId, PaymentIntentId = intent.Id, Amount = 100, Currency = "GBP", Provider = "Stripe", OutcomeStatus = "Captured", CapturedAt = Clock.UtcNow });
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        });
        return new(cartId, order.Id, intent.Id, line.Id);
    }

    private FinanceDbContext Context(Seed test, IInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>(database.CreateOptions<FinanceDbContext>());
        if (interceptor is not null) options.AddInterceptors(interceptor);
        return new(options.Options, new TestTenantProvider(test.TenantId), clock: Clock);
    }

    private static LoyaltyService Service(FinanceDbContext db, Seed test)
    {
        var settings = new Mock<ITenantSettingStore>();
        settings.Setup(store => store.GetTenantValueAsync(LoyaltySettings.Policy, test.TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(test.PolicyJson);
        var tenant = new TestTenantProvider(test.TenantId);
        return new(db, tenant, Clock, settings.Object, new JournalWriter(db, tenant, Clock));
    }

    private static AccountAccessVerifiedEvent Verified(Seed test, Paid paid, Guid target) =>
        new(test.TenantId, Guid.NewGuid(), paid.CartId, paid.OrderId, paid.IntentId, test.PartyId, target);

    private static async Task<Exception?> TryClaimAsync(LoyaltyService service, AccountAccessVerifiedEvent verified)
    {
        try { await service.AttachVerifiedGuestAsync(verified); return null; }
        catch (Exception error) { return error; }
    }

    private sealed record Seed(Guid TenantId, Guid PartyId, Guid AccountPartyId, LoyaltyLedgerBinding Binding, string PolicyJson);
    private sealed record Paid(Guid CartId, Guid OrderId, Guid IntentId, Guid LineId);
    private sealed class FixedClock : IClock { public DateTime UtcNow => new(2026, 10, 9, 8, 0, 0, DateTimeKind.Utc); }

    private sealed class TwoWriters : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource _arrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _count;
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<LoyaltyAccount>().Any(entry => entry.State == EntityState.Modified))
            {
                var count = Interlocked.Increment(ref _count);
                if (count == 2) _arrived.TrySetResult();
                if (count <= 2) await _arrived.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            }
            return result;
        }
    }

    private sealed class RejectOperation : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<LoyaltyOperation>().Any(entry => entry.State == EntityState.Added))
                throw new InvalidOperationException("Injected operation failure");
            return ValueTask.FromResult(result);
        }
    }
}
