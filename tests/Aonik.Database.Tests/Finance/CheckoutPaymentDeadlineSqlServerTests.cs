using System.Collections.Concurrent;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

using Aonik.Finance.Contracts.Models.Payments;
using Aonik.Finance.Contracts.Services.Payments;
using Aonik.Finance.Entities.Orders;
using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Payments;
using Aonik.Finance.Services.Ledger;
using Aonik.Finance.Services.Loyalty;
using Aonik.Finance.Services.GiftCards;
using Microsoft.AspNetCore.DataProtection;
using Aonik.Infrastructure.Persistence;
using Aonik.IntegrationTests.Support;
using Aonik.Platform.Entities.Party;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ledgers;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.TestSupport.Multitenancy;

namespace Aonik.Database.Tests.Finance;

public sealed class CheckoutPaymentDeadlineSqlServerTests(SqlLocalDbFixture database) : IClassFixture<SqlLocalDbFixture>
{
    [SkippableTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExpiredCreate_Should_PreventDelayedCreatorFromStarting_WhenCancellationWins(bool pauseProviderClaim)
    {
        Skip.IfNot(database.IsAvailable, database.SkipReason ?? "SQL Server unavailable.");
        using var test = await CreateHarnessAsync();
        var pause = new PauseIntentSave(pauseProviderClaim);
        await using var delayedDb = test.NewContext(pause);
        var delayed = test.Service(delayedDb).CreateAsync(test.Request);
        try
        {
            await WaitForPauseAsync(pause.Reached.Task, delayed);
            test.Clock.UtcNow = test.Deadline;
            await using var resolverDb = test.NewContext();
            var resolver = test.Service(resolverDb);
            var prior = await resolver.GetStateAsync(test.Request.PaymentIntentId!.Value);
            if (pauseProviderClaim) prior!.Status.Should().Be("Pending");
            else prior.Should().BeNull();

            var closed = await resolver.CreateAsync(test.Request);

            closed.Status.Should().Be("Cancelled");
            (await resolver.GetStateAsync(closed.PaymentIntentId))!.CanNoLongerPay.Should().BeTrue();
        }
        finally { pause.Release.TrySetResult(); }

        (await delayed.WaitAsync(TimeSpan.FromSeconds(30))).Status.Should().Be("Cancelled");
        await using var verify = test.NewContext();
        var intent = (await verify.PaymentIntents.ToListAsync()).Should().ContainSingle().Subject;
        intent.RowVersion.Should().HaveCount(8);
        intent.ProviderStartDeadlineUtc.Should().Be(test.Deadline);
        intent.Status.Should().Be("Cancelled");
        intent.ProviderRequestStartedAtUtc.Should().BeNull();
        intent.ProviderCreateRequestJson.Should().BeNull();
        test.Gateway.Requests.Should().BeEmpty();
    }

    [SkippableFact]
    public async Task ExpiredCreate_Should_KeepStartedAttemptUnknown_WhenProviderStartWinsBeforeTimeout()
    {
        Skip.IfNot(database.IsAvailable, database.SkipReason ?? "SQL Server unavailable.");
        using var test = await CreateHarnessAsync();
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        test.Gateway.OnCreate = async (_, ct) =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                reached.TrySetResult();
                await release.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
            }
            throw new TimeoutException("The provider outcome is deliberately unknown.");
        };
        await using var originalDb = test.NewContext();
        var original = test.Service(originalDb).CreateAsync(test.Request);
        try
        {
            await WaitForPauseAsync(reached.Task, original);
            test.Clock.UtcNow = test.Deadline;
            await using var resolverDb = test.NewContext();
            var resolver = test.Service(resolverDb);

            await resolver.Invoking(s => s.CreateAsync(test.Request)).Should().ThrowAsync<TimeoutException>();

            var state = await resolver.GetStateAsync(test.Request.PaymentIntentId!.Value);
            state!.Status.Should().Be("Processing");
            state.CanNoLongerPay.Should().BeFalse();
        }
        finally { release.TrySetResult(); }
        Func<Task> originalAttempt = async () => { await original.WaitAsync(TimeSpan.FromSeconds(30)); };
        await originalAttempt.Should().ThrowAsync<TimeoutException>();

        await using var verify = test.NewContext();
        var intent = (await verify.PaymentIntents.ToListAsync()).Should().ContainSingle().Subject;
        intent.RowVersion.Should().HaveCount(8);
        intent.ProviderRequestStartedAtUtc.Should().Be(test.StartedAt);
        intent.ProviderStartDeadlineUtc.Should().Be(test.Deadline);
        intent.Status.Should().Be("Processing");
        var attempts = test.Gateway.Requests.ToArray();
        attempts.Should().HaveCount(2);
        attempts[1].Should().Be(attempts[0]);
    }

    private async Task<Harness> CreateHarnessAsync()
    {
        var test = new Harness(database);
        await using var canonical = new AonikDbContext(database.CreateOptions<AonikDbContext>(), new TestTenantProvider(test.TenantId));
        var payerId = Guid.NewGuid();
        canonical.Parties.Add(new Party { Id = payerId, TenantId = test.TenantId, PartyType = "Person", DisplayName = "Deadline purchaser", Status = "Active" });
        canonical.Set<Order>().Add(new Order { Id = test.Request.OrderId, TenantId = test.TenantId, OrderType = "ProductPurchase",
            Status = "Draft", PayerPartyId = payerId, AmountIn = test.Request.Amount, CurrencyIn = "GBP", ProvenanceJson = "{}" });
        await canonical.SaveChangesAsync();
        return test;
    }

    private static async Task WaitForPauseAsync(Task reached, Task operation)
    {
        if (await Task.WhenAny(reached, operation).WaitAsync(TimeSpan.FromSeconds(30)) == operation)
        {
            await operation;
            throw new InvalidOperationException("Payment initiation completed before the expected race boundary.");
        }
        await reached;
    }

    private sealed class Harness : IDisposable
    {
        private readonly SqlLocalDbFixture _database;
        private readonly Mock<IStripeConnectorResolver> _connectors = new(MockBehavior.Strict);
        private readonly Mock<ICheckoutPaymentReconciler> _reconciler = new(MockBehavior.Strict);
        private readonly List<ServiceProvider> _services = [];
        public Guid TenantId { get; } = Guid.NewGuid();
        public DateTime StartedAt { get; } = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);
        public DateTime Deadline => StartedAt.AddMinutes(10);
        public MutableClock Clock { get; } = new();
        public RecordingGateway Gateway { get; } = new();
        public CreateCommerceGuestPaymentIntentRequest Request { get; }

        public Harness(SqlLocalDbFixture database)
        {
            _database = database;
            Clock.UtcNow = StartedAt;
            var attempt = Guid.NewGuid();
            Request = new(Guid.NewGuid(), 23.45m, "GBP", "Stripe", "Card", null, null, attempt,
                $"deadline:{attempt:N}", Deadline);
            _connectors.Setup(c => c.ResolveSelectedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new StripeConnectorBinding
            {
                TenantId = TenantId, ConnectorId = Guid.NewGuid(), ProviderAccountId = "acct_deadline", LiveMode = false,
                ReturnOrigin = "https://shop.example", SecretKey = "sk_test_fixture", SigningSecrets = ["whsec_fixture"]
            });
        }

        public FinanceDbContext NewContext(IInterceptor? interceptor = null)
        {
            var options = new DbContextOptionsBuilder<FinanceDbContext>()
                .UseSqlServer(_database.ConnectionString, sql => sql.EnableRetryOnFailure());
            if (interceptor is not null) options.AddInterceptors(interceptor);
            return new FinanceDbContext(options.Options, new TestTenantProvider(TenantId), null, Clock);
        }

        public CheckoutPaymentService Service(FinanceDbContext db)
        {
            var services = new ServiceCollection();
            services.AddScoped<Tenant>();
            services.AddScoped<ITenantProvider>(p => p.GetRequiredService<Tenant>());
            services.AddScoped<ITenantContext>(p => p.GetRequiredService<Tenant>());
            services.AddSingleton<IClock>(Clock);
            services.AddSingleton(Mock.Of<ITenantSettingStore>());
            var options = (DbContextOptions<FinanceDbContext>)db.GetService<IDbContextOptions>();
            services.AddScoped(p => new FinanceDbContext(options, p.GetRequiredService<ITenantProvider>(), null, Clock));
            services.AddScoped<IJournalWriter, JournalWriter>();
            services.AddScoped<LoyaltyService>();
            services.AddScoped<GiftCardService>();
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            var root = services.BuildServiceProvider();
            _services.Add(root);
            return new(db, new TestTenantProvider(TenantId), _connectors.Object, [Gateway], _reconciler.Object,
                Clock, NullLogger<CheckoutPaymentService>.Instance, root.GetRequiredService<IServiceScopeFactory>());
        }

        public void Dispose() { foreach (var service in _services) service.Dispose(); }
    }

    private sealed class MutableClock : IClock { public DateTime UtcNow { get; set; } }

    private sealed class Tenant : ITenantContext, ITenantProvider
    {
        public Guid? TenantId { get; set; }
        public string? ResolutionSource { get; set; }
        public bool IsResolved => TenantId.HasValue;
        public Guid GetCurrentTenantId() => TenantId!.Value;
        public bool TryGetCurrentTenantId(out Guid tenantId) { tenantId = TenantId ?? Guid.Empty; return TenantId.HasValue; }
    }

    private sealed class RecordingGateway : IPaymentProviderGateway
    {
        public string ProviderCode => "Stripe";
        public ConcurrentQueue<PaymentProviderIntentRequest> Requests { get; } = new();
        public Func<PaymentProviderIntentRequest, CancellationToken, Task<PaymentProviderIntentResult>>? OnCreate { get; set; }
        public Task<PaymentProviderIntentResult> CreateIntentAsync(PaymentProviderIntentRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Enqueue(request);
            return OnCreate?.Invoke(request, cancellationToken) ?? throw new InvalidOperationException("No provider request was expected.");
        }
        public Task<PaymentProviderSetupIntentResult> CreateSetupIntentAsync(PaymentProviderSetupIntentRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class PauseIntentSave(bool providerClaim) : SaveChangesInterceptor
    {
        private int _paused;
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var target = eventData.Context!.ChangeTracker.Entries<PaymentIntent>().Any(e =>
                providerClaim ? e.State == EntityState.Modified && e.Entity.Status == "Processing"
                    : e.State == EntityState.Added && e.Entity.Status == "Pending");
            if (target && Interlocked.CompareExchange(ref _paused, 1, 0) == 0)
            {
                Reached.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return result;
        }
    }
}
