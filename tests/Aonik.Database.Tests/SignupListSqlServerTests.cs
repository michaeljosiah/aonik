using System.Text.Json;

using Aonik.IntegrationTests.Support;
using Aonik.Platform.Contracts.Models.SignupLists;
using Aonik.Platform.Entities.SignupLists;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.SignupLists;
using Aonik.Platform.Settings;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.SharedKernel.Persistence;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;

namespace Aonik.Database.Tests;

public class SignupListSqlServerTests(SqlLocalDbFixture database) : IClassFixture<SqlLocalDbFixture>
{
    [SkippableFact]
    public async Task ConcurrentCapture_Should_ReturnSuccessForBothRequests_AndPersistOneUnchangedConsent()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var barrier = new ConcurrentSaveBarrier(EntityState.Added);
        await using var firstContext = CreateContext(tenantId, barrier);
        await using var secondContext = CreateContext(tenantId, barrier);
        var first = CreateService(firstContext, tenantId);
        var second = CreateService(secondContext, tenantId);

        // Both requests have read the absent key before either INSERT reaches SQL Server.
        await Task.WhenAll(
            first.CaptureAsync(SignupListTypes.DeliveryAvailability,
                new(" Cook@Example.TEST ", "v1", Postcode: "SW1A 1AA")),
            second.CaptureAsync(SignupListTypes.DeliveryAvailability,
                new("cook@example.test", "v1", Postcode: "M1 1AE")));

        barrier.Arrivals.Should().Be(2);
        await using var verification = CreateContext(tenantId);
        var stored = await verification.SignupSubscriptions.SingleAsync();
        stored.NormalizedEmail.Should().Be("cook@example.test");
        stored.Postcode.Should().BeOneOf("SW1A 1AA", "M1 1AE");
        stored.ConsentVersion.Should().Be("v1");
        stored.ConsentTextSnapshot.Should().Be("Tell me when delivery reaches my area.");
        stored.ConsentSource.Should().Be("delivery-availability-form");
        stored.RowVersion.Should().NotBeEmpty();
        var demand = await CreateService(verification, tenantId).GetAreaDemandAsync();
        demand.Should().ContainSingle().Which.Should().Be(new SignupAreaDemandDto(stored.PostcodeOutwardCode!, 1));
        var originalVersion = stored.RowVersion.ToArray();

        await CreateService(verification, tenantId).CaptureAsync(SignupListTypes.DeliveryAvailability,
            new("COOK@example.test", "v1", Postcode: "EH1 2NG"));
        await verification.Entry(stored).ReloadAsync();

        stored.Postcode.Should().BeOneOf("SW1A 1AA", "M1 1AE");
        stored.RowVersion.Should().Equal(originalVersion);
    }

    [SkippableFact]
    public async Task ConcurrentUnsubscribe_Should_SucceedIdempotently_WithoutOverwritingTheWinningTimestamp()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var protection = new EphemeralDataProtectionProvider();
        Guid subscriptionId;
        string token;
        await using (var seed = CreateContext(tenantId))
        {
            var service = CreateService(seed, tenantId, protection: protection);
            await service.CaptureAsync(SignupListTypes.Newsletter, new("cook@example.test", "v1"));
            var row = (await service.ListAsync(SignupListTypes.Newsletter)).Items.Single();
            subscriptionId = row.Id;
            token = row.UnsubscribeToken!;
        }
        var barrier = new ConcurrentSaveBarrier(EntityState.Modified);
        await using var firstContext = CreateContext(tenantId, barrier);
        await using var secondContext = CreateContext(tenantId, barrier);
        var firstClock = new TestClock(new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc));
        var secondClock = new TestClock(firstClock.UtcNow.AddMinutes(1));

        var outcomes = await Task.WhenAll(
            CreateService(firstContext, tenantId, firstClock, protection)
                .UnsubscribeAsync(SignupListTypes.Newsletter, subscriptionId, token),
            CreateService(secondContext, tenantId, secondClock, protection)
                .UnsubscribeAsync(SignupListTypes.Newsletter, subscriptionId, token));

        outcomes.Should().OnlyContain(success => success);
        barrier.Arrivals.Should().Be(2);
        await using var verification = CreateContext(tenantId);
        var rowAfterRace = await verification.SignupSubscriptions.AsNoTracking().SingleAsync();
        rowAfterRace.UnsubscribedAtUtc.Should().BeOneOf(firstClock.UtcNow, secondClock.UtcNow);
        var replay = CreateService(verification, tenantId, new TestClock(secondClock.UtcNow.AddYears(1)), protection);
        (await replay.UnsubscribeAsync(SignupListTypes.Newsletter, subscriptionId, token)).Should().BeTrue();
        await replay.CaptureAsync(SignupListTypes.Newsletter, new("cook@example.test", "v1"));
        var afterReplay = await verification.SignupSubscriptions.AsNoTracking().SingleAsync();
        afterReplay.UnsubscribedAtUtc.Should().Be(rowAfterRace.UnsubscribedAtUtc);
        afterReplay.RowVersion.Should().Equal(rowAfterRace.RowVersion);
        (await replay.ListAsync(SignupListTypes.Newsletter)).Items.Should().BeEmpty();
    }

    [SkippableFact]
    public async Task UniqueIndex_Should_BlockASecondLiveOrWithdrawnKey_ButPermitDifferentTenantsListsAndSoftDeletedKeys()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using (var seed = CreateContext(tenantId))
        {
            seed.SignupSubscriptions.Add(Subscription(tenantId));
            await seed.SaveChangesAsync();
        }
        await using (var duplicate = CreateContext(tenantId))
        {
            duplicate.SignupSubscriptions.Add(Subscription(tenantId));
            var save = () => duplicate.SaveChangesAsync();
            var exception = (await save.Should().ThrowAsync<DbUpdateException>()).Which;
            exception.InnerException.Should().BeOfType<SqlException>().Which.Number.Should().BeOneOf(2601, 2627);
        }
        await using (var withdraw = CreateContext(tenantId))
        {
            var existing = await withdraw.SignupSubscriptions.SingleAsync();
            existing.UnsubscribedAtUtc = new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);
            await withdraw.SaveChangesAsync();
        }
        await using (var duplicateWithdrawn = CreateContext(tenantId))
        {
            duplicateWithdrawn.SignupSubscriptions.Add(Subscription(tenantId));
            var save = () => duplicateWithdrawn.SaveChangesAsync();
            await save.Should().ThrowAsync<DbUpdateException>();
        }
        await using (var otherList = CreateContext(tenantId))
        {
            otherList.SignupSubscriptions.Add(Subscription(tenantId, SignupListTypes.PrivateTable));
            await otherList.SaveChangesAsync();
        }
        await using (var otherTenant = CreateContext(otherTenantId))
        {
            otherTenant.SignupSubscriptions.Add(Subscription(otherTenantId));
            await otherTenant.SaveChangesAsync();
        }
        await using (var softDelete = CreateContext(tenantId))
        {
            var original = await softDelete.SignupSubscriptions.SingleAsync(x => x.ListType == SignupListTypes.Newsletter);
            original.IsDeleted = true;
            await softDelete.SaveChangesAsync();
            softDelete.SignupSubscriptions.Add(Subscription(tenantId));
            await softDelete.SaveChangesAsync();
        }

        await using var verification = CreateContext(tenantId);
        (await verification.SignupSubscriptions.CountAsync()).Should().Be(2);
        (await verification.SignupSubscriptions.IncludeSoftDeleted().AcrossTenants()
            .CountAsync(x => x.TenantId == tenantId || x.TenantId == otherTenantId)).Should().Be(4);
    }

    private void RequireSqlServer() =>
        Skip.IfNot(database.IsAvailable, database.SkipReason ?? "SQL Server LocalDB unavailable.");

    private PlatformDbContext CreateContext(Guid tenantId, IInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>(database.CreateOptions<PlatformDbContext>());
        if (interceptor is not null) options.AddInterceptors(interceptor);
        return new(options.Options, new TestTenantProvider(tenantId), new TestCurrentUserProvider());
    }

    private static SignupListService CreateService(PlatformDbContext context, Guid tenantId,
        IClock? clock = null, IDataProtectionProvider? protection = null)
    {
        var configuration = new SignupListsConfigurationDto(
        [
            new(SignupListTypes.Newsletter, "v1", "Send kitchen notes and offers."),
            new(SignupListTypes.DeliveryAvailability, "v1", "Tell me when delivery reaches my area.")
        ]);
        var settings = new Mock<ITenantSettingStore>();
        settings.Setup(x => x.GetTenantValueAsync(SignupListSettingNames.Configuration, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.Serialize(configuration, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var permissions = new Mock<IPermissionService>();
        permissions.Setup(x => x.HasPermissionAsync(It.IsAny<Guid>(), "Customers.Read", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return new(context, new TestTenantProvider(tenantId), clock ?? new TestClock(DateTime.UtcNow), settings.Object,
            protection ?? new EphemeralDataProtectionProvider(), new TestCurrentUserProvider(), permissions.Object);
    }

    private static SignupSubscription Subscription(Guid tenantId, string listType = SignupListTypes.Newsletter) => new()
    {
        TenantId = tenantId,
        ListType = listType,
        Email = "cook@example.test",
        NormalizedEmail = "cook@example.test",
        ConsentVersion = "v1",
        ConsentTextSnapshot = "Explicit consent to this list.",
        ConsentSource = listType + "-form",
        ConsentedAtUtc = new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc)
    };

    private sealed class TestClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class ConcurrentSaveBarrier(EntityState state) : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource _bothReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;
        public int Arrivals => _arrivals;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            eventData.Context!.ChangeTracker.DetectChanges();
            if (eventData.Context.ChangeTracker.Entries<SignupSubscription>().Any(entry => entry.State == state))
            {
                if (Interlocked.Increment(ref _arrivals) == 2) _bothReady.TrySetResult();
                await _bothReady.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return result;
        }
    }
}
