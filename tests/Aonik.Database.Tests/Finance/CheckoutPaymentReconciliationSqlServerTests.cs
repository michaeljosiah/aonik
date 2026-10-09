using Aonik.Finance.Contracts.Services.Payments;
using Aonik.Finance.Entities.Ledger;
using Aonik.Finance.Entities.Partners;
using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Ledger;
using Aonik.Finance.Services.Payments;
using Aonik.Infrastructure.Persistence;
using Aonik.IntegrationTests.Support;
using Aonik.Platform.Entities.Party;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Events.Outbox;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

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

    [SkippableFact]
    public async Task Apply_Should_RollBackLedgerAndRetryCleanly_When_FinalReceiptSaveFails()
    {
        RequireSql();
        var failure = new FailFinalReceiptSave();
        await using var harness = await BuildAsync(failure);
        var inboxId = await harness.AddInboxAsync("evt_sql_rollback");
        failure.Armed = true;

        var apply = () => harness.ApplyAsync(inboxId);
        await apply.Should().ThrowAsync<InjectedReceiptFailure>();

        await using (var scope = harness.NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
            (await db.Payments.CountAsync(p => p.TenantId == harness.Intent.TenantId)).Should().Be(0);
            (await db.JournalEntries.CountAsync(e => e.TenantId == harness.Intent.TenantId)).Should().Be(0);
            (await db.LedgerAccounts.CountAsync(a => a.TenantId == harness.Intent.TenantId && a.Code == "2100")).Should().Be(0);
            (await db.PaymentIntents.SingleAsync(p => p.Id == harness.Intent.Id)).Status.Should().Be("Processing");
            (await db.PartnerWebhookEvents.SingleAsync(e => e.Id == inboxId)).ProcessedAt.Should().BeNull();
            (await db.Set<OutboxMessage>().CountAsync(e => e.TenantId == harness.Intent.TenantId)).Should().Be(0);
        }
        await harness.ApplyAsync(inboxId);
        await using var verify = harness.NewScope();
        (await verify.ServiceProvider.GetRequiredService<FinanceDbContext>().Payments
            .CountAsync(p => p.TenantId == harness.Intent.TenantId)).Should().Be(1);
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

    private async Task<Harness> BuildAsync(FailFinalReceiptSave? failure = null)
    {
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
        services.AddScoped<ICheckoutPaymentReconciler, CheckoutPaymentReconciler>();
        var root = services.BuildServiceProvider();
        var tenantId = Guid.NewGuid();
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
        var harness = new Harness(root, intent);
        await using var scope = harness.NewScope();
        var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var ledger = new Ledger { TenantId = tenantId, BaseCurrency = "GBP", IsCanonical = true };
        db.Ledgers.Add(ledger);
        db.LedgerAccounts.Add(new LedgerAccount { TenantId = tenantId, LedgerId = ledger.Id, Code = "1000", Name = "Cash", AccountType = "Asset" });
        db.PaymentIntents.Add(intent);
        await db.SaveChangesAsync();
        return harness;
    }

    private sealed class Harness(ServiceProvider root, PaymentIntent intent) : IAsyncDisposable
    {
        public PaymentIntent Intent => intent;
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

    private sealed class InjectedReceiptFailure : Exception { }
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
