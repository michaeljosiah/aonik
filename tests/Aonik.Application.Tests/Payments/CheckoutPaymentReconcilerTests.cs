using Aonik.Finance.Contracts.Services.Payments;
using Aonik.Finance.Entities;
using Aonik.Finance.Entities.Ledger;
using Aonik.Finance.Entities.Partners;
using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Ledger;
using Aonik.Finance.Services.Loyalty;
using Aonik.Finance.Services.GiftCards;
using Microsoft.AspNetCore.DataProtection;
using Aonik.Finance.Services.Payments;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ledgers;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Events.Outbox;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Aonik.Application.Tests.Payments;

public sealed class CheckoutPaymentReconcilerTests
{
    [Fact]
    public async Task Apply_Should_RecordReceiptBalancedJournalAndCompletionOnce_When_ReplayedThroughDifferentEvents()
    {
        await using var harness = await Harness.CreateAsync();
        var firstInbox = await harness.AddInboxAsync("evt_first");
        var secondInbox = await harness.AddInboxAsync("evt_second");

        await harness.Reconciler.ApplyAsync(harness.Intent.Id, harness.Success, firstInbox);
        await harness.Reconciler.ApplyAsync(harness.Intent.Id, harness.Success, secondInbox);
        await harness.Reconciler.ApplyAsync(harness.Intent.Id, harness.Success, firstInbox);

        await using var verifyScope = harness.NewScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var receipt = (await db.Payments.ToListAsync()).Should().ContainSingle().Subject;
        receipt.Amount.Should().Be(23.45m);
        receipt.ProviderReference.Should().Be("pi_verified");
        var journal = (await db.JournalEntries.Include(e => e.Lines).ToListAsync()).Should().ContainSingle().Subject;
        journal.SourceId.Should().Be(harness.Intent.Id);
        journal.Lines.Where(l => l.Direction == "Debit").Sum(l => l.Amount).Should().Be(23.45m);
        journal.Lines.Where(l => l.Direction == "Credit").Sum(l => l.Amount).Should().Be(23.45m);
        (await db.Set<OutboxMessage>().CountAsync(e => e.EventType == typeof(PaymentCompletedEvent).FullName)).Should().Be(1);
        (await db.PartnerWebhookEvents.ToListAsync()).Should().OnlyContain(e => e.ProcessedAt != null);
        (await db.PaymentIntents.SingleAsync()).Status.Should().Be(nameof(PaymentStatus.Captured));
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("intent")]
    [InlineData("order")]
    [InlineData("connector")]
    [InlineData("account")]
    [InlineData("mode")]
    [InlineData("amount")]
    [InlineData("currency")]
    [InlineData("received")]
    [InlineData("session")]
    public async Task Apply_Should_RejectMismatchedEvidence_BeforeAnyFinancialWrite(string mismatch)
    {
        await using var harness = await Harness.CreateAsync();
        var good = harness.Success;
        var snapshot = mismatch switch
        {
            "tenant" => good with { TenantId = Guid.NewGuid() },
            "intent" => good with { PaymentIntentId = Guid.NewGuid() },
            "order" => good with { OrderId = Guid.NewGuid() },
            "connector" => good with { ConnectorId = Guid.NewGuid() },
            "account" => good with { ProviderAccountId = "acct_other" },
            "mode" => good with { LiveMode = true },
            "amount" => good with { Amount = 24m },
            "currency" => good with { Currency = "USD" },
            "received" => good with { ReceivedAmount = 23m },
            _ => good with { SessionId = "cs_other" }
        };

        var apply = () => harness.Reconciler.ApplyAsync(harness.Intent.Id, snapshot);

        await apply.Should().ThrowAsync<InvalidStateException>();
        await using var verifyScope = harness.NewScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        (await db.Payments.CountAsync()).Should().Be(0);
        (await db.JournalEntries.CountAsync()).Should().Be(0);
        (await db.Set<OutboxMessage>().CountAsync()).Should().Be(0);
        (await db.PaymentIntents.SingleAsync()).Status.Should().Be(nameof(PaymentStatus.Processing));
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("RequiresAction")]
    [InlineData("Processing")]
    [InlineData("Failed")]
    public async Task Apply_Should_KeepNonterminalAttemptLocked_WithoutReceipt(string status)
    {
        await using var harness = await Harness.CreateAsync();

        var result = await harness.Reconciler.ApplyAsync(harness.Intent.Id,
            harness.Success with { Status = status, ReceivedAmount = null });

        result.CanNoLongerPay.Should().BeFalse();
        result.Status.Should().Be(status);
        await using var verifyScope = harness.NewScope();
        (await verifyScope.ServiceProvider.GetRequiredService<FinanceDbContext>().Payments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Apply_Should_PreserveCaptured_When_OlderUnpaidObservationArrives()
    {
        await using var harness = await Harness.CreateAsync();
        await harness.Reconciler.ApplyAsync(harness.Intent.Id, harness.Success);

        var result = await harness.Reconciler.ApplyAsync(harness.Intent.Id, harness.Success with
        {
            Status = nameof(PaymentStatus.Cancelled), CanNoLongerPay = true, ReceivedAmount = null
        });

        result.Status.Should().Be(nameof(PaymentStatus.Captured));
        result.CanNoLongerPay.Should().BeFalse();
    }

    [Fact]
    public async Task Reconcile_Should_CancelOnlyUnstartedPendingAttempt_WithoutProviderCall()
    {
        await using var harness = await Harness.CreateAsync();
        var db = harness.Scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var intent = await db.PaymentIntents.SingleAsync();
        intent.Status = nameof(PaymentStatus.Pending);
        intent.ProviderReference = null;
        intent.ProviderRequestStartedAtUtc = null;
        await db.SaveChangesAsync();

        var result = await harness.Reconciler.ReconcileAsync(intent.Id, expire: true);

        result.Status.Should().Be(nameof(PaymentStatus.Cancelled));
        result.CanNoLongerPay.Should().BeTrue();
        harness.Gateway.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Reconcile_Should_KeepUnknownCreationLocked_When_RequestAlreadyStarted()
    {
        await using var harness = await Harness.CreateAsync();
        var db = harness.Scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var intent = await db.PaymentIntents.SingleAsync();
        intent.ProviderReference = null;
        await db.SaveChangesAsync();

        var reconcile = () => harness.Reconciler.ReconcileAsync(intent.Id, expire: true);

        await reconcile.Should().ThrowAsync<InvalidStateException>().WithMessage("*unknown*");
        harness.Gateway.VerifyNoOtherCalls();
    }

    private sealed class Harness : IAsyncDisposable
    {
        public ServiceProvider Root { get; private init; } = null!;
        public AsyncServiceScope Scope { get; private init; }
        public PaymentIntent Intent { get; private init; } = null!;
        public Mock<IPaymentProviderGateway> Gateway { get; private init; } = null!;
        public ICheckoutPaymentReconciler Reconciler => Scope.ServiceProvider.GetRequiredService<ICheckoutPaymentReconciler>();
        public PaymentProviderCheckoutSnapshot Success => new(Intent.TenantId, Intent.Id, Intent.OrderId, Intent.ConnectorId!.Value,
            "acct_test", false, "cs_test", "pi_verified", 23.45m, "GBP", "Captured", false, null, 23.45m);

        public AsyncServiceScope NewScope()
        {
            var scope = Root.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = Intent.TenantId;
            return scope;
        }

        public async Task<Guid> AddInboxAsync(string eventId)
        {
            await using var scope = NewScope();
            var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
            var inbox = new PartnerWebhookEvent { TenantId = Intent.TenantId, ConnectorId = Intent.ConnectorId,
                SignatureValid = true, ProviderCode = "Stripe", ProviderEventId = eventId, ClientReference = Intent.Id.ToString("N"),
                ProcessingStatus = "Received", ProviderReference = "cs_test" };
            db.PartnerWebhookEvents.Add(inbox);
            await db.SaveChangesAsync();
            return inbox.Id;
        }

        public static async Task<Harness> CreateAsync()
        {
            var services = new ServiceCollection();
            var name = "reconcile_" + Guid.NewGuid();
            services.AddLogging();
            services.AddScoped<Tenant>();
            services.AddScoped<ITenantContext>(p => p.GetRequiredService<Tenant>());
            services.AddScoped<ITenantProvider>(p => p.GetRequiredService<Tenant>());
            services.AddSingleton<IClock, FixedClock>();
            services.AddDbContext<FinanceDbContext>(o => o.UseInMemoryDatabase(name));
            services.AddScoped<LedgerPostingService>();
            services.AddSingleton(Mock.Of<ITenantSettingStore>());
            services.AddScoped<IJournalWriter, JournalWriter>();
            services.AddScoped<LoyaltyService>();
            services.AddScoped<GiftCardService>();
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            services.AddScoped<ICheckoutPaymentReconciler, CheckoutPaymentReconciler>();
            var gateway = new Mock<IPaymentProviderGateway>(MockBehavior.Strict);
            services.AddSingleton(gateway.Object);
            var root = services.BuildServiceProvider();
            var scope = root.CreateAsyncScope();
            var tenantId = Guid.NewGuid();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;
            var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
            var payer = new PartyReadModel { TenantId = tenantId, DisplayName = "Guest payer", Status = "Active" };
            var ledger = new Aonik.Finance.Entities.Ledger.Ledger { TenantId = tenantId, BaseCurrency = "GBP", IsCanonical = true };
            db.Parties.Add(payer);
            db.Ledgers.Add(ledger);
            db.LedgerAccounts.Add(new LedgerAccount { TenantId = tenantId, LedgerId = ledger.Id, Code = "1000", Name = "Cash", AccountType = "Asset" });
            var intent = new PaymentIntent { TenantId = tenantId, OrderId = Guid.NewGuid(), Amount = 23.45m, Currency = "GBP",
                PayerPartyId = payer.Id, PaymentMethodType = "Card", Status = "Processing", ProviderCode = "Stripe",
                ConnectorId = Guid.NewGuid(), ProviderAccountId = "acct_test", ProviderLiveMode = false,
                ProviderReference = "cs_test", ProviderRequestStartedAtUtc = DateTime.UtcNow };
            db.PaymentIntents.Add(intent);
            await db.SaveChangesAsync();
            return new Harness { Root = root, Scope = scope, Intent = intent, Gateway = gateway };
        }

        public async ValueTask DisposeAsync() { await Scope.DisposeAsync(); await Root.DisposeAsync(); }
    }

    private sealed class FixedClock : IClock { public DateTime UtcNow => new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc); }
    private sealed class Tenant : ITenantContext, ITenantProvider
    {
        public Guid? TenantId { get; set; }
        public string? ResolutionSource { get; set; }
        public bool IsResolved => TenantId.HasValue;
        public Guid GetCurrentTenantId() => TenantId!.Value;
        public bool TryGetCurrentTenantId(out Guid tenantId) { tenantId = TenantId ?? Guid.Empty; return TenantId.HasValue; }
    }
}
