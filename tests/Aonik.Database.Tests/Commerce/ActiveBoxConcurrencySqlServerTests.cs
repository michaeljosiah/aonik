using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;

using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Catalog;
using Aonik.Commerce.Services.Checkout;
using Aonik.Commerce.Services.Inventory;
using Aonik.Database.Tests.Support;
using Aonik.Infrastructure.Multitenancy;
using Aonik.IntegrationTests.Support;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;

namespace Aonik.Database.Tests.Commerce;

public class ActiveBoxConcurrencySqlServerTests(SqlLocalDbFixture database) : IClassFixture<SqlLocalDbFixture>
{
    [SkippableFact]
    public async Task ConcurrentAdoptionsOfDifferentGuests_Should_LeaveOneActiveBoxForTheParty()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var partyId = Guid.NewGuid();
        var bundleId = await SeedBundleAsync(tenantId);
        var first = await CreateBoxAsync(tenantId, bundleId);
        var second = await CreateBoxAsync(tenantId, bundleId);
        var barrier = new PartyRangeBarrier();
        await using var contextA = CreateContext(tenantId, barrier);
        await using var contextB = CreateContext(tenantId, barrier);

        var outcomes = await Task.WhenAll(
            Capture(NewCarts(contextA, tenantId).AdoptAsync(first.Box.CartId, partyId, CartAccessContext.ForGuest(first.CartToken))),
            Capture(NewCarts(contextB, tenantId).AdoptAsync(second.Box.CartId, partyId, CartAccessContext.ForGuest(second.CartToken))));

        AssertOneWinner(outcomes, barrier);
        await using var verification = CreateContext(tenantId);
        var carts = await verification.Carts.AsNoTracking().ToListAsync();
        carts.Should().HaveCount(2);
        carts.Should().OnlyContain(x => x.Status == CartStatuses.Open && x.OrderId == null);
        carts.Count(x => x.BuyerPartyId == partyId && x.AnonymousToken == null).Should().Be(1);
        carts.Count(x => x.BuyerPartyId == null && x.AnonymousToken != null).Should().Be(1);
    }

    [SkippableFact]
    public async Task ConcurrentCreates_Should_CommitOnlyOnePartyBox()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var partyId = Guid.NewGuid();
        var bundleId = await SeedBundleAsync(tenantId);
        var barrier = new PartyRangeBarrier();
        await using var contextA = CreateContext(tenantId, barrier);
        await using var contextB = CreateContext(tenantId, barrier);
        var command = new CreateBoxCartCommand(bundleId, 6, BuyerPartyId: partyId);

        var outcomes = await Task.WhenAll(
            Capture(NewBoxes(contextA, tenantId).CreateAsync(command)),
            Capture(NewBoxes(contextB, tenantId).CreateAsync(command)));

        AssertOneWinner(outcomes, barrier);
        await using var verification = CreateContext(tenantId);
        var cart = await verification.Carts.AsNoTracking().SingleAsync();
        cart.BuyerPartyId.Should().Be(partyId);
        cart.Status.Should().Be(CartStatuses.Open);
        cart.RowVersion.Should().NotBeEmpty();
    }

    [SkippableFact]
    public async Task CreateRacingWithAdopt_Should_LeaveOnlyOneActivePartyBox()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var partyId = Guid.NewGuid();
        var bundleId = await SeedBundleAsync(tenantId);
        var guest = await CreateBoxAsync(tenantId, bundleId);
        var barrier = new PartyRangeBarrier();
        await using var createContext = CreateContext(tenantId, barrier);
        await using var adoptContext = CreateContext(tenantId, barrier);

        var outcomes = await Task.WhenAll(
            Capture(NewBoxes(createContext, tenantId).CreateAsync(new CreateBoxCartCommand(bundleId, 6, BuyerPartyId: partyId))),
            Capture(NewCarts(adoptContext, tenantId).AdoptAsync(guest.Box.CartId, partyId, CartAccessContext.ForGuest(guest.CartToken))));

        AssertOneWinner(outcomes, barrier);
        await using var verification = CreateContext(tenantId);
        var owned = await verification.Carts.AsNoTracking().Where(x => x.BuyerPartyId == partyId).ToListAsync();
        owned.Should().ContainSingle().Which.Status.Should().Be(CartStatuses.Open);
        (await verification.Carts.CountAsync(x => x.Status == CartStatuses.Abandoned)).Should().Be(0);
    }

    [SkippableFact]
    public async Task CreateAsync_Should_RecognizeItsCommittedCart_WhenCommitAcknowledgementIsLost()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var partyId = Guid.NewGuid();
        var bundleId = await SeedBundleAsync(tenantId);
        var interruptedCommit = new LostCommitAcknowledgement();
        await using var context = CreateContext(tenantId, interruptedCommit);

        var result = await NewBoxes(context, tenantId).CreateAsync(
            new CreateBoxCartCommand(bundleId, 6, BuyerPartyId: partyId));

        interruptedCommit.Interrupted.Should().BeTrue();
        await using var verification = CreateContext(tenantId);
        var persisted = await verification.Carts.AsNoTracking().SingleAsync();
        persisted.Id.Should().Be(result.Box.CartId);
        persisted.BuyerPartyId.Should().Be(partyId);
        result.CartVersion.Should().Be(Convert.ToBase64String(persisted.RowVersion));
    }

    [SkippableFact]
    public async Task OppositeChoices_Should_CommitOneOutcome_AndNeverArchiveBothBoxes()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var partyId = Guid.NewGuid();
        var bundleId = await SeedBundleAsync(tenantId);
        var saved = await CreateBoxAsync(tenantId, bundleId, partyId);
        var guest = await CreateBoxAsync(tenantId, bundleId);
        var barrier = new PartyRangeBarrier();
        await using var keepContext = CreateContext(tenantId, barrier);
        await using var savedContext = CreateContext(tenantId, barrier);
        var choice = new AdoptCartChoice(CartAdoptionDecisions.KeepGuest, saved.Box.CartId,
            saved.CartVersion, guest.CartVersion);
        var access = CartAccessContext.ForGuest(guest.CartToken);

        var outcomes = await Task.WhenAll(
            Capture(NewCarts(keepContext, tenantId).AdoptAsync(guest.Box.CartId, partyId, access, choice)),
            Capture(NewCarts(savedContext, tenantId).AdoptAsync(guest.Box.CartId, partyId, access,
                choice with { Decision = CartAdoptionDecisions.UseSaved })));

        AssertOneWinner(outcomes, barrier);
        await using var verification = CreateContext(tenantId);
        var rows = await verification.Carts.AsNoTracking().ToListAsync();
        rows.Should().HaveCount(2).And.OnlyContain(x => x.BuyerPartyId == partyId && x.OrderId == null);
        rows.Count(x => x.Status == CartStatuses.Open).Should().Be(1);
        rows.Count(x => x.Status == CartStatuses.Abandoned).Should().Be(1);
        rows.Single(x => x.Id == guest.Box.CartId).AnonymousToken.Should().BeNull();
    }

    [SkippableFact]
    public async Task DifferentPartiesClaimingOneBox_Should_LeaveOneOwner_AndHideTheWinnerFromTheLoser()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var bundleId = await SeedBundleAsync(tenantId);
        var guest = await CreateBoxAsync(tenantId, bundleId);
        var barrier = new PartyRangeBarrier();
        await using var contextA = CreateContext(tenantId, barrier);
        await using var contextB = CreateContext(tenantId, barrier);
        var partyA = Guid.NewGuid();
        var partyB = Guid.NewGuid();

        var outcomes = await Task.WhenAll(
            Capture(NewCarts(contextA, tenantId).AdoptAsync(guest.Box.CartId, partyA, CartAccessContext.ForGuest(guest.CartToken))),
            Capture(NewCarts(contextB, tenantId).AdoptAsync(guest.Box.CartId, partyB, CartAccessContext.ForGuest(guest.CartToken))));

        outcomes.Count(x => x is null).Should().Be(1);
        outcomes.Single(x => x is not null).Should().BeOfType<NotFoundException>();
        barrier.Arrivals.Should().Be(2);
        barrier.IsolationLevels.Should().OnlyContain(level => level == IsolationLevel.Serializable);
        await using var verification = CreateContext(tenantId);
        var cart = await verification.Carts.AsNoTracking().SingleAsync();
        new[] { partyA, partyB }.Should().Contain(cart.BuyerPartyId!.Value);
        cart.AnonymousToken.Should().BeNull();
    }

    [SkippableTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Choice_Should_RejectAnEditOrCheckoutThatCommittedAfterThePrompt(
        bool checkout, bool changeGuest)
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var partyId = Guid.NewGuid();
        var bundleId = await SeedBundleAsync(tenantId);
        var saved = await CreateBoxAsync(tenantId, bundleId, partyId);
        var guest = await CreateBoxAsync(tenantId, bundleId);
        var choice = new AdoptCartChoice(CartAdoptionDecisions.KeepGuest, saved.Box.CartId,
            saved.CartVersion, guest.CartVersion);
        var pause = new BeforeFirstCartRead();
        await using var adoptContext = CreateContext(tenantId, pause);
        var adoption = Capture(NewCarts(adoptContext, tenantId).AdoptAsync(guest.Box.CartId,
            partyId, CartAccessContext.ForGuest(guest.CartToken), choice));
        await pause.Reached.Task.WaitAsync(TimeSpan.FromSeconds(30));
        var changedId = changeGuest ? guest.Box.CartId : saved.Box.CartId;
        try
        {
            await using var writer = CreateContext(tenantId);
            if (checkout)
            {
                // This is checkout's durable fence while payment is pending; Status stays Open.
                var cart = await writer.Carts.SingleAsync(x => x.Id == changedId);
                cart.OrderId = Guid.NewGuid();
                await writer.SaveChangesAsync();
            }
            else
            {
                await NewBoxes(writer, tenantId).ChangeSizeAsync(changedId, 12,
                    changeGuest ? CartAccessContext.ForGuest(guest.CartToken) : CartAccessContext.ForParty(partyId));
            }
        }
        finally
        {
            pause.Release.TrySetResult();
        }

        var failure = await adoption;

        if (checkout && changeGuest) failure.Should().BeOfType<StorefrontValidationException>();
        else failure.Should().BeOfType<ActiveBoxConflictException>();
        await using var verification = CreateContext(tenantId);
        var rows = await verification.Carts.AsNoTracking().ToListAsync();
        rows.Should().OnlyContain(x => x.Status == CartStatuses.Open);
        var source = rows.Single(x => x.Id == guest.Box.CartId);
        source.BuyerPartyId.Should().BeNull();
        source.AnonymousToken.Should().Be(guest.CartToken);
        var changed = rows.Single(x => x.Id == changedId);
        Convert.ToBase64String(changed.RowVersion).Should().NotBe(changeGuest ? choice.ExpectedGuestCartVersion : choice.ExpectedSavedCartVersion);
        if (checkout) changed.OrderId.Should().NotBeNull();
        else changed.BoxSize.Should().Be(12);
    }

    private static void AssertOneWinner(Exception?[] outcomes, PartyRangeBarrier barrier)
    {
        outcomes.Count(x => x is null).Should().Be(1);
        outcomes.Single(x => x is not null).Should().BeOfType<ActiveBoxConflictException>();
        barrier.Arrivals.Should().Be(2);
        barrier.IsolationLevels.Should().OnlyContain(level => level == IsolationLevel.Serializable,
            "the active-party range must be protected before its first read");
    }

    private void RequireSqlServer() => Skip.IfNot(database.IsAvailable,
        database.SkipReason ?? "SQL Server LocalDB unavailable.");

    private CommerceDbContext CreateContext(Guid tenantId, IInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<CommerceDbContext>(database.CreateOptions<CommerceDbContext>());
        if (interceptor is not null) options.AddInterceptors(interceptor);
        return new(options.Options, new TestTenantProvider(tenantId), new TestCurrentUserProvider());
    }

    private static CartService NewCarts(CommerceDbContext context, Guid tenantId) => new(context,
        new TestTenantProvider(tenantId), new ProductPricingService(context, new TestTenantProvider(tenantId), new WallClock()));

    private static BoxCartService NewBoxes(CommerceDbContext context, Guid tenantId)
    {
        var tenant = new TestTenantProvider(tenantId);
        var clock = new WallClock();
        var currency = new Mock<ITenantCurrencyProvider>();
        currency.Setup(x => x.GetTenantDefaultCurrencyAsync(tenantId, It.IsAny<CancellationToken>())).ReturnsAsync("GBP");
        currency.Setup(x => x.GetTenantCurrencyCodesAsync(tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(["GBP"]);
        return new(context, tenant,
            new OptionSelectionService(context, CommerceSqlServerHarness.CreateOptionService(context, tenantId), tenant),
            new InventoryService(context, tenant, new TenantContext { TenantId = tenantId }, clock),
            Mock.Of<ITenantSettingStore>(), Mock.Of<ISettingProvider>(), currency.Object,
            new ProductPricingService(context, tenant, clock));
    }

    private async Task<BoxCartDto> CreateBoxAsync(Guid tenantId, Guid bundleId, Guid? partyId = null)
    {
        await using var context = CreateContext(tenantId);
        return await NewBoxes(context, tenantId).CreateAsync(new CreateBoxCartCommand(bundleId, 6, BuyerPartyId: partyId));
    }

    private async Task<Guid> SeedBundleAsync(Guid tenantId)
    {
        await using var context = CreateContext(tenantId);
        var bundleId = Guid.NewGuid();
        context.Products.Add(new Product
        {
            Id = bundleId, TenantId = tenantId, Slug = "meal-box", Name = "Meal Box",
            Kind = ProductKinds.Bundle, Status = ProductStatuses.Active, BundlePricingMode = BundlePricingModes.SizeTiered
        });
        context.BundleSlots.Add(new BundleSlot
        {
            TenantId = tenantId, BundleProductId = bundleId, Name = "Pick dishes", MinItems = 0, MaxItems = 99, AllowDuplicates = true
        });
        context.BundleSizePlans.Add(new BundleSizePlan
        {
            TenantId = tenantId, BundleProductId = bundleId, MinSize = 6, MaxSize = 30,
            BaseSize = 6, BasePrice = 95m, PerSpacePrice = 15m, Currency = "GBP"
        });
        await context.SaveChangesAsync();
        return bundleId;
    }

    private static async Task<Exception?> Capture(Task task)
    {
        try { await task; return null; }
        catch (Exception error) { return error; }
    }

    private sealed class WallClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }

    private sealed class PartyRangeBarrier : DbCommandInterceptor
    {
        private readonly ConcurrentDictionary<Guid, byte> _contexts = new();
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ConcurrentBag<IsolationLevel?> IsolationLevels { get; } = [];
        public int Arrivals => _contexts.Count;

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            var where = command.CommandText.IndexOf("WHERE", StringComparison.Ordinal);
            if (command.CommandText.Contains("[AnkCarts]", StringComparison.Ordinal) && where >= 0
                && command.CommandText[where..].Contains("[BuyerPartyId] =", StringComparison.Ordinal)
                && _contexts.TryAdd(eventData.Context!.ContextId.InstanceId, 0))
            {
                // Synchronize before SQL takes range locks; waiting after a locked read would deadlock the test itself.
                IsolationLevels.Add(command.Transaction?.IsolationLevel);
                if (_contexts.Count == 2) _ready.TrySetResult();
                await _ready.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return result;
        }
    }

    private sealed class BeforeFirstCartRead : DbCommandInterceptor
    {
        private int _seen;
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("[AnkCarts]", StringComparison.Ordinal)
                && Interlocked.CompareExchange(ref _seen, 1, 0) == 0)
            {
                Reached.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return result;
        }
    }

    private sealed class LostCommitAcknowledgement : DbTransactionInterceptor
    {
        private int _commits;
        public bool Interrupted => _commits > 0;

        public override Task TransactionCommittedAsync(DbTransaction transaction,
            TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _commits) == 1)
                throw new TimeoutException("Test: SQL committed, but acknowledgement was lost.");
            return Task.CompletedTask;
        }
    }
}
