using Aonik.Finance.Contracts.Services.Payments;
using Aonik.Finance.Contracts.Models.Payments;
using Aonik.Finance.Entities.Orders;
using Aonik.Finance.Entities.Ledger;
using Aonik.Finance.Entities.Partners;
using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Ledger;
using Aonik.Finance.Services.Loyalty;
using Aonik.Finance.Services.Payments;
using Aonik.Infrastructure.Persistence;
using Aonik.IntegrationTests.Support;
using Aonik.Platform.Entities.Party;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ledgers;
using Aonik.SharedKernel.Abstractions.Loyalty;
using Aonik.SharedKernel.Abstractions.Payments;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Events.Outbox;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Aonik.Database.Tests.Finance;

public sealed class CheckoutPaymentReconciliationSqlServerTests(SqlLocalDbFixture database) : IClassFixture<SqlLocalDbFixture>
{
    [SkippableFact]
    public async Task Apply_Should_CommitReceiptJournalStatusInboxAndOutbox_OnceUnderNativeVersions()
    {
        RequireSql();
        await using var harness = await BuildAsync();
        var inboxId = await harness.AddInboxAsync("evt_sql_once");

        await harness.ApplyAsync(inboxId);
        await harness.ApplyAsync(inboxId);

        await using var scope = harness.NewScope();
        var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var receipt = (await db.Payments.Where(p => p.TenantId == harness.Intent.TenantId).ToListAsync()).Should().ContainSingle().Subject;
        receipt.RowVersion.Should().HaveCount(8);
        receipt.Amount.Should().Be(23.45m);
        var journal = (await db.JournalEntries.Include(e => e.Lines).Where(e => e.TenantId == harness.Intent.TenantId).ToListAsync())
            .Should().ContainSingle().Subject;
        journal.Lines.Where(l => l.Direction == "Debit").Sum(l => l.Amount)
            .Should().Be(journal.Lines.Where(l => l.Direction == "Credit").Sum(l => l.Amount));
        (await db.PaymentIntents.SingleAsync(p => p.Id == harness.Intent.Id)).Status.Should().Be("Captured");
        (await db.PartnerWebhookEvents.SingleAsync(e => e.Id == inboxId)).ProcessedAt.Should().NotBeNull();
        (await db.Set<OutboxMessage>().CountAsync(e => e.TenantId == harness.Intent.TenantId
            && e.EventType == typeof(PaymentCompletedEvent).FullName)).Should().Be(1);
    }

    [SkippableTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Apply_Should_RollBackLedgerAndRetryCleanly_When_FinalReceiptSaveFails(bool loyalty)
    {
        RequireSql();
        var failure = new FailFinalReceiptSave();
        await using var harness = await BuildAsync(failure, loyalty);
        var inboxId = await harness.AddInboxAsync("evt_sql_rollback");
        failure.Armed = true;

        var apply = () => harness.ApplyAsync(inboxId);
        await apply.Should().ThrowAsync<InjectedReceiptFailure>();

        await using (var scope = harness.NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
            (await db.Payments.CountAsync(p => p.TenantId == harness.Intent.TenantId)).Should().Be(0);
            (await db.JournalEntries.CountAsync(e => e.TenantId == harness.Intent.TenantId)).Should().Be(loyalty ? 1 : 0);
            (await db.LedgerAccounts.CountAsync(a => a.TenantId == harness.Intent.TenantId && a.Code == "2100")).Should().Be(0);
            (await db.PaymentIntents.SingleAsync(p => p.Id == harness.Intent.Id)).Status.Should().Be("Processing");
            (await db.PartnerWebhookEvents.SingleAsync(e => e.Id == inboxId)).ProcessedAt.Should().BeNull();
            (await db.Set<OutboxMessage>().CountAsync(e => e.TenantId == harness.Intent.TenantId)).Should().Be(0);
            if (loyalty)
            {
                var balance = await scope.ServiceProvider.GetRequiredService<LoyaltyService>().GetBalanceAsync(harness.Intent.PayerPartyId!.Value);
                balance.BalancePoints.Should().Be(100);
                balance.ReservedPoints.Should().Be(100);
                (await db.LoyaltyCheckoutAttempts.SingleAsync(x => x.PaymentIntentId == harness.Intent.Id)).Status.Should().Be("Reserved");
            }
        }
        await harness.ApplyAsync(inboxId);
        await using var verify = harness.NewScope();
        (await verify.ServiceProvider.GetRequiredService<FinanceDbContext>().Payments
            .CountAsync(p => p.TenantId == harness.Intent.TenantId)).Should().Be(1);
        if (loyalty)
        {
            await harness.ApplyAsync(inboxId);
            var balance = await verify.ServiceProvider.GetRequiredService<LoyaltyService>().GetBalanceAsync(harness.Intent.PayerPartyId!.Value);
            balance.BalancePoints.Should().Be(46);
            balance.ReservedPoints.Should().Be(0);
            (await verify.ServiceProvider.GetRequiredService<FinanceDbContext>().JournalEntries
                .CountAsync(x => x.TenantId == harness.Intent.TenantId)).Should().Be(4);
        }
    }

    [SkippableFact]
    public async Task Apply_Should_ConvergeDifferentConcurrentEvents_WithoutDuplicateFinancialEffects()
    {
        RequireSql();
        await using var harness = await BuildAsync();
        var first = await harness.AddInboxAsync("evt_sql_first");
        var second = await harness.AddInboxAsync("evt_sql_second");
        async Task Attempt(Guid id)
        {
            try { await harness.ApplyAsync(id); }
            catch (DbUpdateException) { } // The durable notification is retried in a fresh worker scope below.
        }

        await Task.WhenAll(Attempt(first), Attempt(second));
        await harness.ApplyAsync(first);
        await harness.ApplyAsync(second);

        await using var scope = harness.NewScope();
        var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        (await db.Payments.CountAsync(p => p.TenantId == harness.Intent.TenantId)).Should().Be(1);
        (await db.JournalEntries.CountAsync(e => e.TenantId == harness.Intent.TenantId)).Should().Be(1);
        (await db.Set<OutboxMessage>().CountAsync(e => e.TenantId == harness.Intent.TenantId)).Should().Be(1);
        (await db.PartnerWebhookEvents.Where(e => e.ConnectorId == harness.Intent.ConnectorId).ToListAsync())
            .Should().OnlyContain(e => e.ProcessedAt != null);
    }

    private void RequireSql() => Skip.IfNot(database.IsAvailable, database.SkipReason ?? "SQL Server unavailable.");

    [SkippableFact]
    public async Task Create_Should_AdmitOnlyOneOfTwoCartsSpendingTheSamePoints()
    {
        RequireSql();
        var gate = new PauseRewardClaims();
        await using var harness = await BuildAsync(gate, loyalty: true, seedPayment: false);
        var secondOrderId = Guid.NewGuid();
        var secondItemId = Guid.NewGuid();
        await using (var seed = harness.NewScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<FinanceDbContext>();
            db.Orders.Add(new Order { Id = secondOrderId, TenantId = harness.Intent.TenantId, OrderType = "ProductPurchase",
                Status = "Draft", PayerPartyId = harness.Intent.PayerPartyId, AmountIn = 24.45m, CurrencyIn = "GBP",
                Items = [new OrderItem { Id = secondItemId, TenantId = harness.Intent.TenantId, OrderId = secondOrderId,
                    ItemType = "ProductPurchase", ItemIndex = 0, AmountIn = 24.45m, CurrencyIn = "GBP", CurrencyOut = "GBP", DetailsJson = "{}" }] });
            await db.SaveChangesAsync();
        }
        var first = new CreateCommerceGuestPaymentIntentRequest(harness.Intent.OrderId, 23.45m, "GBP", "Stripe", "Card",
            null, null, harness.Intent.Id, $"reward:{harness.Intent.Id:N}", Loyalty: harness.Loyalty);
        var secondId = Guid.NewGuid();
        var second = first with { OrderId = secondOrderId, PaymentIntentId = secondId, IdempotencyKey = $"reward:{secondId:N}",
            Loyalty = harness.Loyalty! with { CartId = Guid.NewGuid(), Lines = [harness.Loyalty.Lines[0] with { OrderItemId = secondItemId }] } };
        gate.Armed = true;
        async Task<GuestPaymentIntentResponse> Create(CreateCommerceGuestPaymentIntentRequest request)
        {
            await using var scope = harness.NewScope();
            return await scope.ServiceProvider.GetRequiredService<CheckoutPaymentService>().CreateAsync(request);
        }
        var a = Create(first);
        var b = Create(second);
        try
        {
            var firstFinished = await Task.WhenAny(gate.Reached.Task, a, b).WaitAsync(TimeSpan.FromSeconds(30));
            if (firstFinished != gate.Reached.Task) { await firstFinished; throw new InvalidOperationException("Payment creation ended before contention."); }
        }
        finally { gate.Release.TrySetResult(); }
        var results = await Task.WhenAll(a, b).WaitAsync(TimeSpan.FromSeconds(30));

        results.Select(x => x.Status).Should().BeEquivalentTo(["Pending", "Cancelled"]);
        harness.Gateway.Requests.Should().ContainSingle();
        await using var verify = harness.NewScope();
        var context = verify.ServiceProvider.GetRequiredService<FinanceDbContext>();
        (await context.LoyaltyCheckoutAttempts.Where(x => x.TenantId == harness.Intent.TenantId && x.Status == "Reserved").ToListAsync())
            .Should().ContainSingle();
        var balance = await verify.ServiceProvider.GetRequiredService<LoyaltyService>().GetBalanceAsync(harness.Intent.PayerPartyId!.Value);
        balance.BalancePoints.Should().Be(100);
        balance.ReservedPoints.Should().Be(100);
        (await context.LoyaltyAccounts.SingleAsync(x => x.TenantId == harness.Intent.TenantId)).RowVersion.Should().HaveCount(8);
    }

    [SkippableFact]
    public async Task Apply_Should_RetainUnknownRewards_ThenReleaseOnlyOnConfirmedClosure()
    {
        RequireSql();
        await using var harness = await BuildAsync(loyalty: true);
        await harness.ObserveAsync("Processing");
        await using (var unknown = harness.NewScope())
            (await unknown.ServiceProvider.GetRequiredService<LoyaltyService>().GetBalanceAsync(harness.Intent.PayerPartyId!.Value))
                .ReservedPoints.Should().Be(100);

        await harness.ObserveAsync("Cancelled", closed: true);
        await harness.ObserveAsync("Cancelled", closed: true);

        await using var verify = harness.NewScope();
        var db = verify.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var balance = await verify.ServiceProvider.GetRequiredService<LoyaltyService>().GetBalanceAsync(harness.Intent.PayerPartyId!.Value);
        balance.BalancePoints.Should().Be(100);
        balance.ReservedPoints.Should().Be(0);
        (await db.LoyaltyCheckoutAttempts.SingleAsync(x => x.PaymentIntentId == harness.Intent.Id)).Status.Should().Be("Released");
        (await db.Payments.CountAsync(x => x.TenantId == harness.Intent.TenantId)).Should().Be(0);
        (await db.JournalEntries.CountAsync(x => x.TenantId == harness.Intent.TenantId)).Should().Be(1);
    }

    [SkippableFact]
    public async Task Apply_Should_KeepCaptureAndRewardCommitTogether_WhenCancellationCompetes()
    {
        RequireSql();
        await using var harness = await BuildAsync(loyalty: true);
        async Task Observe(string status, bool closed)
        {
            try { await harness.ObserveAsync(status, closed); }
            catch (DbUpdateException) { }
            catch (InvalidStateException) { } // Contradictory capture after proven closure cannot reuse released points.
        }
        await Task.WhenAll(Observe("Captured", false), Observe("Cancelled", true));
        await using var verify = harness.NewScope();
        var db = verify.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var intent = await db.PaymentIntents.AsNoTracking().SingleAsync(x => x.Id == harness.Intent.Id);
        if (intent.Status == "Processing") await harness.ObserveAsync("Captured"); // Retry a transient losing claim.
        intent = await db.PaymentIntents.AsNoTracking().SingleAsync(x => x.Id == harness.Intent.Id);
        var captured = intent.Status == "Captured";
        intent.Status.Should().BeOneOf("Captured", "Cancelled");
        var balance = await verify.ServiceProvider.GetRequiredService<LoyaltyService>().GetBalanceAsync(harness.Intent.PayerPartyId!.Value);
        balance.BalancePoints.Should().Be(captured ? 46 : 100);
        balance.ReservedPoints.Should().Be(0);
        (await db.Payments.CountAsync(x => x.TenantId == harness.Intent.TenantId)).Should().Be(captured ? 1 : 0);
        (await db.Set<OutboxMessage>().CountAsync(x => x.TenantId == harness.Intent.TenantId)).Should().Be(captured ? 1 : 0);
    }

    [SkippableFact]
    public async Task Reconcile_Should_ReleasePointsWithNeverStartedCancellation()
    {
        RequireSql();
        await using var harness = await BuildAsync(loyalty: true);
        await using (var arrange = harness.NewScope())
        {
            var db = arrange.ServiceProvider.GetRequiredService<FinanceDbContext>();
            var intent = await db.PaymentIntents.SingleAsync(x => x.Id == harness.Intent.Id);
            intent.Status = "Pending";
            intent.ProviderReference = null;
            intent.ProviderRequestStartedAtUtc = null;
            await db.SaveChangesAsync();
        }
        await using (var cancel = harness.NewScope())
            (await cancel.ServiceProvider.GetRequiredService<ICheckoutPaymentReconciler>()
                .ReconcileAsync(harness.Intent.Id, expire: true)).CanNoLongerPay.Should().BeTrue();

        await using var verify = harness.NewScope();
        var balance = await verify.ServiceProvider.GetRequiredService<LoyaltyService>().GetBalanceAsync(harness.Intent.PayerPartyId!.Value);
        balance.BalancePoints.Should().Be(100);
        balance.ReservedPoints.Should().Be(0);
        harness.Gateway.Requests.Should().BeEmpty();
        var context = verify.ServiceProvider.GetRequiredService<FinanceDbContext>();
        (await context.PaymentIntents.SingleAsync(x => x.Id == harness.Intent.Id)).Status.Should().Be("Cancelled");
        (await context.LoyaltyCheckoutAttempts.SingleAsync(x => x.PaymentIntentId == harness.Intent.Id)).Status.Should().Be("Released");
    }

    private async Task<Harness> BuildAsync(IInterceptor? failure = null, bool loyalty = false, bool seedPayment = true)
    {
        var tenantId = Guid.NewGuid();
        var settings = new Mock<ITenantSettingStore>();
        var gateway = new RecordingGateway(tenantId);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<Tenant>();
        services.AddScoped<ITenantContext>(p => p.GetRequiredService<Tenant>());
        services.AddScoped<ITenantProvider>(p => p.GetRequiredService<Tenant>());
        services.AddSingleton<IClock, FixedClock>();
        services.AddDbContext<FinanceDbContext>(options =>
        {
            options.UseSqlServer(database.ConnectionString, sql => sql.EnableRetryOnFailure());
            if (failure is not null) options.AddInterceptors(failure);
        });
        services.AddScoped<LedgerPostingService>();
        services.AddSingleton(settings.Object);
        services.AddScoped<IJournalWriter, JournalWriter>();
        services.AddScoped<LoyaltyService>();
        services.AddScoped<ICheckoutPaymentReconciler, CheckoutPaymentReconciler>();
        services.AddScoped<CheckoutPaymentService>();
        services.AddSingleton<IPaymentProviderGateway>(gateway);
        var connectors = new Mock<IStripeConnectorResolver>();
        connectors.Setup(x => x.ResolveSelectedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new StripeConnectorBinding
        {
            TenantId = tenantId, ConnectorId = Guid.NewGuid(), ProviderAccountId = "acct_test", LiveMode = false,
            ReturnOrigin = "https://shop.example", SecretKey = "sk_test_fixture", SigningSecrets = ["whsec_fixture"]
        });
        services.AddSingleton(connectors.Object);
        var root = services.BuildServiceProvider();
        var payerId = Guid.NewGuid();
        // Party is Platform-owned; seed the actual entity so the runtime read model is exercised.
        await using (var canonical = new AonikDbContext(database.CreateOptions<AonikDbContext>(), new TestTenantProvider(tenantId)))
        {
            canonical.Parties.Add(new Party { Id = payerId, TenantId = tenantId, PartyType = "Person", DisplayName = "SQL guest", Status = "Active" });
            await canonical.SaveChangesAsync();
        }
        var intent = new PaymentIntent { TenantId = tenantId, OrderId = Guid.NewGuid(), Amount = 23.45m, Currency = "GBP",
            PayerPartyId = payerId, PaymentMethodType = "Card", Status = "Processing", ProviderCode = "Stripe", ConnectorId = Guid.NewGuid(),
            ProviderAccountId = "acct_test", ProviderLiveMode = false, ProviderReference = "cs_" + Guid.NewGuid().ToString("N"),
            ProviderRequestStartedAtUtc = DateTime.UtcNow };
        var harness = new Harness(root, intent, gateway);
        await using var scope = harness.NewScope();
        var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var ledger = new Ledger { TenantId = tenantId, BaseCurrency = "GBP", IsCanonical = true };
        db.Ledgers.Add(ledger);
        db.LedgerAccounts.Add(new LedgerAccount { TenantId = tenantId, LedgerId = ledger.Id, Code = "1000", Name = "Cash", AccountType = "Asset" });
        await db.SaveChangesAsync();
        if (loyalty)
        {
            var liability = new LedgerAccount { TenantId = tenantId, LedgerId = ledger.Id, Code = "2201", Name = "Reward liability", AccountType = "Liability" };
            var expense = new LedgerAccount { TenantId = tenantId, LedgerId = ledger.Id, Code = "6201", Name = "Reward expense", AccountType = "Expense" };
            db.LedgerAccounts.AddRange(liability, expense);
            var item = new OrderItem { TenantId = tenantId, OrderId = intent.OrderId, ItemIndex = 0,
                ItemType = "ProductPurchase", AmountIn = 24.45m, CurrencyIn = "GBP", CurrencyOut = "GBP", DetailsJson = "{}" };
            db.Orders.Add(new Order { Id = intent.OrderId, TenantId = tenantId, OrderType = "ProductPurchase", Status = "Draft",
                PayerPartyId = payerId, AmountIn = 24.45m, CurrencyIn = "GBP", Items = [item] });
            await db.SaveChangesAsync();
            var binding = new LoyaltyLedgerBinding(ledger.Id, liability.Id, expense.Id, expense.Id);
            settings.Setup(x => x.GetTenantValueAsync(LoyaltySettings.Policy, tenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(JsonSerializer.Serialize(new LoyaltyPolicy(true, "payment-test", binding), new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            await using (var funding = harness.NewScope())
                await funding.ServiceProvider.GetRequiredService<LoyaltyService>().AdjustAsync(new(payerId, Guid.NewGuid(), 100, "Fixture opening points"));
            var rewards = scope.ServiceProvider.GetRequiredService<LoyaltyService>();
            var policy = await rewards.GetPolicyAsync();
            harness.Loyalty = new(Guid.NewGuid(), payerId, false, policy.Version, binding, 100, 46, 1m, 24.45m,
                [new(0, item.Id, "ProductPurchase", Guid.NewGuid(), 24.45m, 0, 1m, 0, 23.45m, 23.45m, 46, 100, true, true)], PayableTotal: 23.45m);
            if (seedPayment)
            {
                intent.Status = "Pending";
                (await rewards.PrepareTrackedAsync(intent, harness.Loyalty)).Should().BeNull();
                intent.Status = "Processing";
            }
        }
        if (seedPayment) db.PaymentIntents.Add(intent);
        await db.SaveChangesAsync();
        return harness;
    }

    private sealed class Harness(ServiceProvider root, PaymentIntent intent, RecordingGateway gateway) : IAsyncDisposable
    {
        public PaymentIntent Intent => intent;
        public LoyaltyCheckout? Loyalty { get; set; }
        public RecordingGateway Gateway => gateway;
        public AsyncServiceScope NewScope()
        {
            var scope = root.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = intent.TenantId;
            return scope;
        }
        public async Task ApplyAsync(Guid inbox)
        {
            await using var scope = NewScope();
            await scope.ServiceProvider.GetRequiredService<ICheckoutPaymentReconciler>().ApplyAsync(intent.Id,
                new(intent.TenantId, intent.Id, intent.OrderId, intent.ConnectorId!.Value, "acct_test", false,
                    intent.ProviderReference!, "pi_" + intent.Id.ToString("N"), intent.Amount, "GBP", "Captured", false, null, intent.Amount), inbox);
        }
        public async Task<PaymentIntentStateRef> ObserveAsync(string status, bool closed = false)
        {
            await using var scope = NewScope();
            return await scope.ServiceProvider.GetRequiredService<ICheckoutPaymentReconciler>().ApplyAsync(intent.Id,
                new(intent.TenantId, intent.Id, intent.OrderId, intent.ConnectorId!.Value, "acct_test", false,
                    intent.ProviderReference!, "pi_" + intent.Id.ToString("N"), intent.Amount, "GBP", status, closed, null,
                    status == "Captured" ? intent.Amount : null));
        }
        public async Task<Guid> AddInboxAsync(string eventId)
        {
            await using var scope = NewScope();
            var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
            var inbox = new PartnerWebhookEvent { TenantId = intent.TenantId, ConnectorId = intent.ConnectorId,
                ProviderCode = "Stripe", Category = "Collection", EventType = "checkout.session.completed", ProviderEventId = eventId,
                ProviderReference = intent.ProviderReference!, ClientReference = intent.Id.ToString("N"), PayloadHash = "test",
                RawPayload = "{}", SignatureValid = true, ProcessingStatus = "Received", ReceivedAt = DateTime.UtcNow };
            db.PartnerWebhookEvents.Add(inbox);
            await db.SaveChangesAsync();
            return inbox.Id;
        }
        public ValueTask DisposeAsync() => root.DisposeAsync();
    }

    private sealed class RecordingGateway(Guid tenantId) : IPaymentProviderGateway
    {
        public string ProviderCode => "Stripe";
        public ConcurrentQueue<PaymentProviderIntentRequest> Requests { get; } = new();
        public Task<PaymentProviderIntentResult> CreateIntentAsync(PaymentProviderIntentRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Enqueue(request);
            var reference = "cs_" + request.PaymentIntentId.ToString("N");
            return Task.FromResult(new PaymentProviderIntentResult("Stripe", reference, "Pending", null, "https://checkout.stripe.com/test",
                new PaymentProviderCheckoutSnapshot(tenantId, request.PaymentIntentId, request.OrderId, request.ConnectorId!.Value,
                    request.ProviderAccountId!, request.LiveMode!.Value, reference, null, request.Amount, request.Currency,
                    "Pending", false, "https://checkout.stripe.com/test")));
        }
        public Task<PaymentProviderSetupIntentResult> CreateSetupIntentAsync(PaymentProviderSetupIntentRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class InjectedReceiptFailure : Exception { }
    private sealed class PauseRewardClaims : SaveChangesInterceptor
    {
        private int _arrivals;
        public bool Armed { get; set; }
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Armed && eventData.Context!.ChangeTracker.Entries<PaymentIntent>().Any(x => x.State == EntityState.Added && x.Entity.Status == "Pending"))
            {
                if (Interlocked.Increment(ref _arrivals) == 2) Reached.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return result;
        }
    }
    private sealed class FailFinalReceiptSave : SaveChangesInterceptor
    {
        public bool Armed { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Armed && eventData.Context!.ChangeTracker.Entries<Payment>().Any(e => e.State == EntityState.Added))
            {
                Armed = false;
                throw new InjectedReceiptFailure();
            }
            return ValueTask.FromResult(result);
        }
    }
    private sealed class FixedClock : IClock { public DateTime UtcNow => DateTime.UtcNow; }
    private sealed class Tenant : ITenantContext, ITenantProvider
    {
        public Guid? TenantId { get; set; }
        public string? ResolutionSource { get; set; }
        public bool IsResolved => TenantId.HasValue;
        public Guid GetCurrentTenantId() => TenantId!.Value;
        public bool TryGetCurrentTenantId(out Guid tenantId) { tenantId = TenantId ?? Guid.Empty; return TenantId.HasValue; }
    }
}
