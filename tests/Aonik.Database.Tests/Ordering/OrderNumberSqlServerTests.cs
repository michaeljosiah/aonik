using Aonik.Finance.Entities.Orders;
using Aonik.Infrastructure.Persistence;
using Aonik.IntegrationTests.Support;
using Aonik.Ordering;
using Aonik.Platform.Contracts.Services.Autonumbering;
using Aonik.Platform.Entities.Autonumbering;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Autonumbering;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Ordering;
using Aonik.SharedKernel.Events.Outbox;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Database.Tests.Ordering;

public sealed class OrderNumberSqlServerTests(SqlLocalDbFixture database) : IClassFixture<SqlLocalDbFixture>
{
    private static readonly FixedClock Clock = new();

    [SkippableFact]
    public async Task ConcurrentAllocations_Should_RetryNativeProfileVersion_AndNeverDuplicateReferences()
    {
        RequireSql();
        var tenantId = Guid.NewGuid();
        await SeedProfileAsync(tenantId);
        var gate = new ProfileSaveGate();
        await using var first = Provider(tenantId, gate);
        await using var second = Provider(tenantId, gate);
        var one = first.GetRequiredService<IOrderService>().CreateAsync(Command("first"));
        var two = second.GetRequiredService<IOrderService>().CreateAsync(Command("second"));
        try
        {
            await AwaitBothAsync(gate.BothArrived.Task, one, two);
            gate.Release.TrySetResult();
            var results = await Task.WhenAll(one, two).WaitAsync(TimeSpan.FromSeconds(30));

            results.Select(order => order.OrderNumber).Should().BeEquivalentTo("SHOP-0100", "SHOP-0101");
            results.Select(order => order.Id).Distinct().Should().HaveCount(2);
            await using var verify = Canonical(tenantId);
            var profile = await verify.AutonumberProfiles.SingleAsync(row => row.TenantId == tenantId);
            profile.LastIssuedValue.Should().Be(101);
            profile.RowVersion.Should().HaveCount(8);
            (await verify.Set<Order>().CountAsync(row => row.TenantId == tenantId)).Should().Be(2);
        }
        finally { gate.Release.TrySetResult(); }
    }

    [SkippableFact]
    public async Task ConcurrentIdempotentCreates_Should_ReturnWinningNumber_AndNeverReuseTheLosingAllocation()
    {
        RequireSql();
        var tenantId = Guid.NewGuid();
        await SeedProfileAsync(tenantId);
        var gate = new NumberGate();
        await using var first = Provider(tenantId, numberGate: gate);
        await using var second = Provider(tenantId, numberGate: gate);
        var one = first.GetRequiredService<IOrderService>().CreateAsync(Command("same"));
        var two = second.GetRequiredService<IOrderService>().CreateAsync(Command("same"));
        try
        {
            await AwaitBothAsync(gate.BothArrived.Task, one, two);
            gate.Release.TrySetResult();
            var results = await Task.WhenAll(one, two).WaitAsync(TimeSpan.FromSeconds(30));

            results[1].Id.Should().Be(results[0].Id);
            results[1].OrderNumber.Should().Be(results[0].OrderNumber);
            results[1].Items.Single().NameSnapshot.Should().Be("Purchased recipe");
            await using var replayScope = Provider(tenantId);
            var orders = replayScope.GetRequiredService<IOrderService>();
            (await orders.CreateAsync(Command("same"))).OrderNumber.Should().Be(results[0].OrderNumber);
            var next = await orders.CreateAsync(Command("next"));
            next.OrderNumber.Should().Be("SHOP-0102", "the losing allocation is a gap, never reused");
            await using var verify = Canonical(tenantId);
            (await verify.Set<Order>().CountAsync(row => row.TenantId == tenantId)).Should().Be(2);
            (await verify.Set<OutboxMessage>().CountAsync(row => row.TenantId == tenantId)).Should().Be(2);
            (await verify.AutonumberProfiles.SingleAsync(row => row.TenantId == tenantId)).LastIssuedValue.Should().Be(102);
        }
        finally { gate.Release.TrySetResult(); }
    }

    [SkippableFact]
    public async Task References_Should_BeUniqueWithinTenant_AllowOtherTenantSameReference_AndKeepLegacyNulls()
    {
        RequireSql();
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await SeedProfileAsync(tenantId);
        await SeedProfileAsync(otherTenantId);
        await using var first = Provider(tenantId);
        await using var second = Provider(otherTenantId);
        var created = await first.GetRequiredService<IOrderService>().CreateAsync(Command("same-key"));
        var other = await second.GetRequiredService<IOrderService>().CreateAsync(Command("same-key"));

        created.OrderNumber.Should().Be("SHOP-0100");
        other.OrderNumber.Should().Be(created.OrderNumber);
        other.Id.Should().NotBe(created.Id);
        (await second.GetRequiredService<IOrderService>().GetAsync(created.Id)).Should().BeNull();
        await using var verify = Canonical(tenantId);
        verify.Set<Order>().AddRange(LegacyOrder(tenantId), LegacyOrder(tenantId));
        await verify.SaveChangesAsync();
        (await verify.Set<Order>().CountAsync(order => order.TenantId == tenantId && order.OrderNumber == null)).Should().Be(2);
        var duplicate = LegacyOrder(tenantId);
        duplicate.OrderNumber = created.OrderNumber;
        verify.Set<Order>().Add(duplicate);
        var saveDuplicate = () => verify.SaveChangesAsync();
        await saveDuplicate.Should().ThrowAsync<DbUpdateException>();
    }

    [SkippableFact]
    public async Task PendingReplacement_Should_PersistNameSnapshotAndKeepReference_WhileLegacyNamesRemainNull()
    {
        RequireSql();
        var tenantId = Guid.NewGuid();
        await SeedProfileAsync(tenantId);
        await using var provider = Provider(tenantId);
        var orders = provider.GetRequiredService<IOrderService>();
        var created = await orders.CreateAsync(Command("revised"));
        var refreshed = await orders.RefreshPendingItemsAsync(created.Id, Guid.NewGuid(), null, "GBP",
            [new(OrderTypeCodes.ProductPurchase, 0, 20m, "GBP", NameSnapshot: "Replacement recipe", DetailsJson: "{\"selection\":true}")]);

        refreshed.OrderNumber.Should().Be(created.OrderNumber);
        await using var readProvider = Provider(tenantId);
        var stored = (await readProvider.GetRequiredService<IOrderService>().GetAsync(created.Id))!;
        stored.Items.Single().NameSnapshot.Should().Be("Replacement recipe");
        stored.Items.Single().DetailsJson.Should().Be("{\"selection\":true}");
        await using var canonical = Canonical(tenantId);
        (await canonical.Set<Order>().SingleAsync(row => row.TenantId == tenantId && row.Id == created.Id)).RowVersion.Should().HaveCount(8);
        var legacy = LegacyOrder(tenantId);
        legacy.Items.Add(new OrderItem { TenantId = tenantId, OrderId = legacy.Id, ItemType = OrderTypeCodes.ProductPurchase,
            CurrencyIn = "GBP", CurrencyOut = "GBP", Status = "Valid" });
        canonical.Set<Order>().Add(legacy);
        await canonical.SaveChangesAsync();
        var legacyDto = (await readProvider.GetRequiredService<IOrderService>().GetAsync(legacy.Id))!;
        legacyDto.OrderNumber.Should().BeNull();
        legacyDto.Items.Single().NameSnapshot.Should().BeNull();
    }

    private ServiceProvider Provider(Guid tenantId, ProfileSaveGate? profileGate = null, NumberGate? numberGate = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITenantProvider>(new TestTenantProvider(tenantId));
        services.AddSingleton<ICurrentUserProvider>(new TestCurrentUserProvider());
        services.AddSingleton<IClock>(Clock);
        services.AddDbContext<PlatformDbContext>(options =>
        {
            options.UseSqlServer(database.ConnectionString, sql => sql.EnableRetryOnFailure());
            if (profileGate is not null) options.AddInterceptors(profileGate);
        });
        services.AddScoped<IAutonumberingService, AutonumberingService>();
        services.AddScoped<IOrderNumberGenerator>(sp =>
        {
            IOrderNumberGenerator generator = new OrderNumberGenerator(sp.GetRequiredService<IAutonumberingService>(),
                sp.GetRequiredService<ITenantProvider>(), Clock);
            return numberGate is null ? generator : new GatedGenerator(generator, numberGate);
        });
        services.AddOrderingModule(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = database.ConnectionString
        }).Build());
        return services.BuildServiceProvider();
    }

    private async Task SeedProfileAsync(Guid tenantId)
    {
        await using var db = Canonical(tenantId);
        db.AutonumberProfiles.Add(new AutonumberProfile
        {
            TenantId = tenantId, EntityType = "Order", PrefixTemplate = "SHOP-", PaddingLength = 4,
            MinValue = 100, MaxValue = 9999, LastIssuedValue = 99, IsActive = true,
            Strategy = AutonumberStrategy.Sequential, ResetPolicy = AutonumberResetPolicy.None
        });
        await db.SaveChangesAsync();
    }

    private AonikDbContext Canonical(Guid tenantId)
        => new(database.CreateOptions<AonikDbContext>(), new TestTenantProvider(tenantId), new TestCurrentUserProvider(), Clock);
    private static CreateOrderCommand Command(string key) => new(OrderTypeCodes.ProductPurchase, null, "GBP",
        [new(OrderTypeCodes.ProductPurchase, 0, 25m, "GBP", NameSnapshot: "Purchased recipe")], key);
    private static Order LegacyOrder(Guid tenantId) => new()
    {
        TenantId = tenantId, OrderType = OrderTypeCodes.ProductPurchase, Status = OrderStatusCodes.Complete, CurrencyIn = "GBP"
    };
    private void RequireSql() => Skip.IfNot(database.IsAvailable, database.SkipReason ?? "SQL Server unavailable.");
    private static async Task AwaitBothAsync(Task gate, Task first, Task second)
    {
        var completed = await Task.WhenAny(gate, first, second).WaitAsync(TimeSpan.FromSeconds(30));
        if (completed != gate) await completed;
        await gate.WaitAsync(TimeSpan.FromSeconds(30));
    }
    private sealed class FixedClock : IClock { public DateTime UtcNow => new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc); }
    private sealed class NumberGate
    {
        private int _arrivals;
        public TaskCompletionSource BothArrived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task WaitAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _arrivals) == 2) BothArrived.TrySetResult();
            await Release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        }
    }
    private sealed class GatedGenerator(IOrderNumberGenerator inner, NumberGate gate) : IOrderNumberGenerator
    {
        public async Task<string> GenerateAsync(CancellationToken cancellationToken = default)
        {
            var reference = await inner.GenerateAsync(cancellationToken);
            await gate.WaitAsync(cancellationToken);
            return reference;
        }
    }
    private sealed class ProfileSaveGate : SaveChangesInterceptor
    {
        private readonly NumberGate _gate = new();
        public TaskCompletionSource BothArrived => _gate.BothArrived;
        public TaskCompletionSource Release => _gate.Release;
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!Release.Task.IsCompleted && eventData.Context!.ChangeTracker.Entries<AutonumberProfile>()
                    .Any(entry => entry.State == EntityState.Modified))
                await _gate.WaitAsync(cancellationToken);
            return result;
        }
    }
}
