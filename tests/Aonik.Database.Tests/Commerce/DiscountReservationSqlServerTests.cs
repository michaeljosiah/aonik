using Aonik.Commerce.Entities.Promotions;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Promotions;
using Aonik.IntegrationTests.Support;
using Aonik.SharedKernel.Abstractions;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Aonik.Database.Tests.Commerce;

public sealed class DiscountReservationSqlServerTests(SqlLocalDbFixture database) : IClassFixture<SqlLocalDbFixture>
{
    [SkippableFact]
    public async Task Reserve_Should_RejectConcurrentAuthoringEvenForUnlimitedCampaign()
    {
        RequireSql();
        var test = new Harness();
        var discount = await SeedAsync(test);
        var gate = new ClaimSaveGate();
        await using var claimDb = Context(test, gate);
        var service = Service(claimDb, test);
        var quote = await service.ComputeAsync("SAVE", Lines(test), "GBP");
        var claim = WriteAsync(claimDb, test, () => service.ReserveTrackedAsync(test.CartId, test.AttemptId,
            "SAVE", Lines(test), "GBP", quote));
        try
        {
            await AwaitGateAsync(gate.Arrived.Task, claim);
            await using var authorDb = Context(test);
            var edited = await Service(authorDb, test).UpdateAsync(discount.Id,
                new("Percentage", 20, true, null, null, null, null, discount.Version));
            edited.Version.Should().NotBe(discount.Version);
            gate.Release.TrySetResult();
            var action = async () => await claim;
            await action.Should().ThrowAsync<DbUpdateConcurrencyException>();
            await claimDb.SaveChangesAsync();
            var changedQuote = () => WriteAsync(claimDb, test, () => service.ReserveTrackedAsync(test.CartId,
                test.AttemptId, "SAVE", Lines(test), "GBP", quote));
            (await changedQuote.Should().ThrowAsync<DiscountException>()).Which.Code.Should().Be(DiscountException.PriceChanged);
            await using var verify = Context(test);
            (await verify.DiscountReservations.CountAsync()).Should().Be(0);
            (await verify.Discounts.SingleAsync()).Value.Should().Be(20m);
        }
        finally { gate.Release.TrySetResult(); }
    }

    [SkippableFact]
    public async Task AdminUpdate_Should_RejectStaleVersionAfterAUseIsReserved()
    {
        RequireSql();
        var test = new Harness();
        var original = await SeedAsync(test, maximum: 1);
        await using var reserveDb = Context(test);
        var service = Service(reserveDb, test);
        var quote = await service.ComputeAsync("SAVE", Lines(test), "GBP");
        var reservation = await WriteAsync(reserveDb, test, () => service.ReserveTrackedAsync(test.CartId,
            test.AttemptId, "SAVE", Lines(test), "GBP", quote));
        reservation.Should().NotBeNull();
        await using var authorDb = Context(test);
        var author = Service(authorDb, test);
        var stale = () => author.UpdateAsync(original.Id, new("Percentage", 10, false, null, 1, null, null, original.Version));
        (await stale.Should().ThrowAsync<DiscountException>()).Which.Code.Should().Be(DiscountException.Conflict);
        var current = (await author.ListAsync(new())).Items.Single();
        current.ReservedCount.Should().Be(1);
        current.Version.Should().NotBe(original.Version);
        var lower = () => author.UpdateAsync(original.Id, new("Percentage", 10, true, null, 0, null, null, current.Version));
        await lower.Should().ThrowAsync<InvalidStateException>();
        (await author.ListAsync(new())).Items.Single().IsActive.Should().BeTrue();
    }

    [SkippableFact]
    public async Task FailedCapture_Should_NotReflushRolledBackClaimOrCounterFromSameContext()
    {
        RequireSql();
        var test = new Harness();
        var original = await SeedAsync(test);
        await using var db = Context(test);
        var service = Service(db, test);
        var quote = await service.ComputeAsync("SAVE", Lines(test), "GBP");
        var reservation = (await WriteAsync(db, test, () => service.ReserveTrackedAsync(test.CartId,
            test.AttemptId, "SAVE", Lines(test), "GBP", quote)))!.Value;
        var act = async () => await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            service.Detach(test.CartId, original.Id);
            await using var transaction = await db.Database.BeginTransactionAsync();
            try
            {
                await service.CommitTrackedAsync(test.CartId, reservation, test.AttemptId, original.Id);
                await db.SaveChangesAsync();
                throw new InjectedFailure();
            }
            finally
            {
                await transaction.RollbackAsync();
                service.Detach(test.CartId, original.Id);
            }
        });
        await act.Should().ThrowAsync<InjectedFailure>();
        await service.CreateAsync(new("UNRELATED", DiscountKinds.Percentage, 5));
        await using (var verify = Context(test))
        {
            (await verify.Discounts.SingleAsync(row => row.Id == original.Id)).TimesRedeemed.Should().Be(0);
            var held = await verify.DiscountReservations.SingleAsync();
            held.Status.Should().Be(DiscountReservationStatuses.Reserved);
            held.RowVersion.Should().HaveCount(8);
        }
        await WriteAsync(db, test, async () =>
        {
            await service.CommitTrackedAsync(test.CartId, reservation, test.AttemptId, original.Id);
            return true;
        });
        await WriteAsync(db, test, async () =>
        {
            await service.CommitTrackedAsync(test.CartId, reservation, test.AttemptId, original.Id);
            return true;
        });
        await using var final = Context(test);
        (await final.Discounts.SingleAsync(row => row.Id == original.Id)).TimesRedeemed.Should().Be(1);
        (await final.DiscountReservations.SingleAsync()).Status.Should().Be(DiscountReservationStatuses.Redeemed);
    }

    [SkippableFact]
    public async Task Release_Should_BindTenantCartAndAttemptBeforeReusingNativeClaim()
    {
        RequireSql();
        var test = new Harness();
        await SeedAsync(test, maximum: 1);
        await using var db = Context(test);
        var service = Service(db, test);
        var quote = await service.ComputeAsync("SAVE", Lines(test), "GBP");
        var reservation = (await WriteAsync(db, test, () => service.ReserveTrackedAsync(test.CartId,
            test.AttemptId, "SAVE", Lines(test), "GBP", quote)))!.Value;
        var foreignTenant = new Harness();
        await using (var foreignDb = Context(foreignTenant))
        {
            var foreign = Service(foreignDb, foreignTenant);
            var denied = () => foreign.ReleaseTrackedAsync(test.CartId, reservation, test.AttemptId);
            await denied.Should().ThrowAsync<DiscountException>();
        }
        var wrongAttempt = () => service.ReleaseTrackedAsync(test.CartId, reservation, Guid.NewGuid());
        await wrongAttempt.Should().ThrowAsync<DiscountException>();
        await WriteAsync(db, test, async () => { await service.ReleaseTrackedAsync(test.CartId, reservation, test.AttemptId); return true; });
        var replacement = Guid.NewGuid();
        var reused = await WriteAsync(db, test, () => service.ReserveTrackedAsync(test.CartId, replacement,
            "SAVE", Lines(test), "GBP", quote));
        reused.Should().Be(reservation);
        await using var verify = Context(test);
        var row = (await verify.DiscountReservations.ToListAsync()).Should().ContainSingle().Subject;
        row.AttemptId.Should().Be(replacement);
        row.Status.Should().Be(DiscountReservationStatuses.Reserved);
        row.RowVersion.Should().HaveCount(8);
    }

    private void RequireSql() => Skip.IfNot(database.IsAvailable, database.SkipReason ?? "SQL Server unavailable.");
    private CommerceDbContext Context(Harness test, IInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<CommerceDbContext>().UseSqlServer(database.ConnectionString,
            sql => sql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(1), null));
        if (interceptor is not null) options.AddInterceptors(interceptor);
        return new(options.Options, new TestTenantProvider(test.TenantId), null, test.Clock);
    }
    private static DiscountService Service(CommerceDbContext db, Harness test) => new(db, new TestTenantProvider(test.TenantId), test.Clock);
    private static DiscountChargeLine[] Lines(Harness test) => [new(0, test.ProductId, 20m)];
    private async Task<DiscountDto> SeedAsync(Harness test, int? maximum = null)
    {
        await using var db = Context(test);
        return await Service(db, test).CreateAsync(new("SAVE", DiscountKinds.Percentage, 10, MaxRedemptions: maximum));
    }
    private static async Task<T> WriteAsync<T>(CommerceDbContext db, Harness test, Func<Task<T>> mutation)
    {
        var service = Service(db, test);
        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                service.Detach(test.CartId);
                await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
                var result = await mutation();
                await db.SaveChangesAsync();
                await transaction.CommitAsync();
                return result;
            });
        }
        finally { service.Detach(test.CartId); }
    }
    private static async Task AwaitGateAsync(Task gate, Task operation)
    {
        var completed = await Task.WhenAny(gate, operation).WaitAsync(TimeSpan.FromSeconds(30));
        if (completed == operation) await operation;
        await gate.WaitAsync(TimeSpan.FromSeconds(30));
    }
    private sealed class Harness
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid CartId { get; } = Guid.NewGuid();
        public Guid AttemptId { get; } = Guid.NewGuid();
        public Guid ProductId { get; } = Guid.NewGuid();
        public TestClock Clock { get; } = new();
    }
    private sealed class TestClock : IClock { public DateTime UtcNow => new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc); }
    private sealed class InjectedFailure : Exception { }
    private sealed class ClaimSaveGate : SaveChangesInterceptor
    {
        public TaskCompletionSource Arrived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<DiscountReservation>().Any(entry => entry.State == EntityState.Added))
            {
                Arrived.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return result;
        }
    }
}
