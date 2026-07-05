using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Contracts.Services;
using Aonik.PersonalFinance.Entities;
using Aonik.Finance.Persistence;
using Aonik.PersonalFinance.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Persistence;
using Aonik.Worker.Jobs;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aonik.Application.Tests.PersonalFinance;

public class CustomerInsightSnapshotJobTests
{
    private sealed class TestTenantContext : ITenantContext
    {
        public Guid? TenantId { get; set; }
        public string? ResolutionSource { get; set; }
        public bool IsResolved => TenantId.HasValue;
    }

    private sealed class ContextTenantProvider : ITenantProvider
    {
        private readonly ITenantContext _tenantContext;

        public ContextTenantProvider(ITenantContext tenantContext)
        {
            _tenantContext = tenantContext;
        }

        public Guid GetCurrentTenantId() => _tenantContext.TenantId ?? Guid.Empty;

        public bool TryGetCurrentTenantId(out Guid tenantId)
        {
            tenantId = _tenantContext.TenantId ?? Guid.Empty;
            return _tenantContext.TenantId.HasValue;
        }
    }

    private sealed class TestClock : IClock
    {
        public TestClock(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; set; }
    }

    private sealed class StubEnumerator : ICustomerInsightSnapshotJobUserEnumerator
    {
        private readonly IReadOnlyList<CustomerInsightSnapshotJobUserTarget> _users;

        public StubEnumerator(IReadOnlyList<CustomerInsightSnapshotJobUserTarget> users)
        {
            _users = users;
        }

        public Task<IReadOnlyList<CustomerInsightSnapshotJobUserTarget>> GetNextBatchAsync(
            CustomerInsightSnapshotJobCheckpoint? checkpoint,
            int batchSize,
            CancellationToken cancellationToken = default)
        {
            var filtered = _users
                .Where(x => checkpoint is null
                    || x.TenantId.CompareTo(checkpoint.Value.TenantId) > 0
                    || (x.TenantId == checkpoint.Value.TenantId && x.UserId.CompareTo(checkpoint.Value.UserId) > 0))
                .Take(batchSize)
                .ToList();

            return Task.FromResult<IReadOnlyList<CustomerInsightSnapshotJobUserTarget>>(filtered);
        }
    }

    private sealed class RecordingSnapshotService : ICustomerInsightSnapshotService
    {
        private readonly Guid _timedOutUserId;
        private readonly List<Guid> _calls = [];

        public RecordingSnapshotService(Guid timedOutUserId)
        {
            _timedOutUserId = timedOutUserId;
        }

        public IReadOnlyList<Guid> Calls => _calls;

        public async Task<CustomerInsightSnapshotResponse> GenerateCurrentSnapshotAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            _calls.Add(userId);

            if (userId == _timedOutUserId)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return new CustomerInsightSnapshotResponse(
                        Guid.NewGuid(),
                        userId,
                        CustomerInsightSnapshotContract.StatusFailed,
                        DateTime.UtcNow,
                        DateTime.UtcNow.AddDays(-CustomerInsightSnapshotContract.BehaviourWindowDays),
                        DateTime.UtcNow,
                        1,
                        string.Empty,
                        CustomerInsightSnapshotContract.GeneratorVersion,
                        0,
                        "Snapshot generation timed out or was cancelled.",
                        null,
                        DateTime.UtcNow,
                        null,
                        null);
                }
            }

            return new CustomerInsightSnapshotResponse(
                Guid.NewGuid(),
                userId,
                CustomerInsightSnapshotContract.StatusCurrent,
                DateTime.UtcNow,
                DateTime.UtcNow.AddDays(-CustomerInsightSnapshotContract.BehaviourWindowDays),
                DateTime.UtcNow,
                1,
                "hash",
                CustomerInsightSnapshotContract.GeneratorVersion,
                1,
                null,
                null,
                DateTime.UtcNow,
                null,
                null);
        }
    }

    private static Aonik.PersonalFinance.Persistence.PersonalFinanceDbContext CreateDbContext(ITenantProvider tenantProvider)
    {
        var options = new DbContextOptionsBuilder<Aonik.PersonalFinance.Persistence.PersonalFinanceDbContext>()
            .UseInMemoryDatabase($"CustomerInsightSnapshotJob_{Guid.NewGuid()}")
            .Options;

        return new Aonik.PersonalFinance.Persistence.PersonalFinanceDbContext(options, tenantProvider);
    }

    [Fact]
    public async Task UserEnumerator_ShouldReturnStableOrderedPagedUsers()
    {
        // Arrange
        var tenantContext = new TestTenantContext { TenantId = Guid.NewGuid(), ResolutionSource = "test" };
        var tenantProvider = new ContextTenantProvider(tenantContext);
        using var dbContext = CreateDbContext(tenantProvider);

        var tenantA = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var tenantB = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var userA1 = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var userA2 = Guid.Parse("10000000-0000-0000-0000-000000000002");
        var userB1 = Guid.Parse("10000000-0000-0000-0000-000000000003");

        dbContext.PersonalTransactions.Add(new PersonalTransaction
        {
            TenantId = tenantB,
            UserId = userB1,
            SourceType = "manual",
            SourceId = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow,
            Amount = -20m,
            Currency = "USD",
            TransactionType = TransactionCategoryReference.TypeExpense,
            Category = TransactionCategoryReference.Groceries,
            TagsJson = "[]"
        });

        dbContext.Budgets.Add(new Budget
        {
            TenantId = tenantA,
            UserId = userA2,
            PeriodType = "Monthly",
            PeriodStart = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            BudgetCreatedBy = "User",
            Status = "Active"
        });

        dbContext.PersonalProfiles.Add(new PersonalProfile
        {
            TenantId = tenantA,
            UserId = userA1
        });

        await dbContext.SaveChangesAsync();

        var enumerator = new CustomerInsightSnapshotJobUserEnumerator(dbContext);

        // Act
        var firstBatch = await enumerator.GetNextBatchAsync(null, 2);
        var secondBatch = await enumerator.GetNextBatchAsync(
            new CustomerInsightSnapshotJobCheckpoint(firstBatch[1].TenantId, firstBatch[1].UserId),
            2);

        // Assert
        firstBatch.Should().Equal(
            new CustomerInsightSnapshotJobUserTarget(tenantA, userA1),
            new CustomerInsightSnapshotJobUserTarget(tenantA, userA2));

        secondBatch.Should().Equal(new CustomerInsightSnapshotJobUserTarget(tenantB, userB1));
    }

    [Fact]
    public async Task UserEnumerator_ShouldReturnEachUserOnce_WhenPresentInMultipleTables()
    {
        // Arrange — one user that shows up in three of the union'd tables. The enumerator
        // must collapse it to a single target (dedup happens in SQL now, issue H11); it must
        // not emit the user once per table.
        var tenantContext = new TestTenantContext { TenantId = Guid.NewGuid(), ResolutionSource = "test" };
        var tenantProvider = new ContextTenantProvider(tenantContext);
        using var dbContext = CreateDbContext(tenantProvider);

        var tenant = Guid.Parse("00000000-0000-0000-0000-0000000000aa");
        var user = Guid.Parse("10000000-0000-0000-0000-0000000000aa");

        dbContext.PersonalProfiles.Add(new PersonalProfile { TenantId = tenant, UserId = user });
        dbContext.Budgets.Add(new Budget
        {
            TenantId = tenant,
            UserId = user,
            PeriodType = "Monthly",
            PeriodStart = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            BudgetCreatedBy = "User",
            Status = "Active"
        });
        dbContext.PersonalTransactions.Add(new PersonalTransaction
        {
            TenantId = tenant,
            UserId = user,
            SourceType = "manual",
            SourceId = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow,
            Amount = -5m,
            Currency = "USD",
            TransactionType = TransactionCategoryReference.TypeExpense,
            Category = TransactionCategoryReference.Groceries,
            TagsJson = "[]"
        });
        await dbContext.SaveChangesAsync();

        var enumerator = new CustomerInsightSnapshotJobUserEnumerator(dbContext);

        // Act
        var batch = await enumerator.GetNextBatchAsync(null, 10);

        // Assert
        batch.Should().ContainSingle()
            .Which.Should().Be(new CustomerInsightSnapshotJobUserTarget(tenant, user));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldContinueWhenOneUserTimesOut()
    {
        // Arrange
        var first = new CustomerInsightSnapshotJobUserTarget(Guid.NewGuid(), Guid.NewGuid());
        var second = new CustomerInsightSnapshotJobUserTarget(Guid.NewGuid(), Guid.NewGuid());
        var tenantContext = new TestTenantContext();
        var service = new RecordingSnapshotService(first.UserId);
        var job = new CustomerInsightSnapshotJob(
            new StubEnumerator([first, second]),
            service,
            tenantContext,
            Microsoft.Extensions.Options.Options.Create(new ScheduledJobOptions
            {
                CustomerInsightSnapshot = new CustomerInsightSnapshotJobOptions
                {
                    BatchSize = 10,
                    UserTimeoutSeconds = 1,
                    UserWarningThresholdSeconds = 1
                }
            }),
            NullLogger<CustomerInsightSnapshotJob>.Instance);

        var jobDataMap = new JobDataMap();

        // Act
        await job.ExecuteAsync(jobDataMap, CancellationToken.None);

        // Assert
        service.Calls.Should().ContainInOrder(first.UserId, second.UserId);
        tenantContext.TenantId.Should().BeNull();
        CustomerInsightSnapshotJob.ReadCheckpoint(jobDataMap).Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotCreateDuplicateCurrentSnapshots_WhenHashIsUnchangedAcrossRuns()
    {
        // Arrange
        var tenantContext = new TestTenantContext();
        var tenantProvider = new ContextTenantProvider(tenantContext);
        var clock = new TestClock(new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc));
        using var dbContext = CreateDbContext(tenantProvider);

        var tenantId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        var userId = Guid.Parse("30000000-0000-0000-0000-000000000001");
        SeedMinimalSnapshotUser(dbContext, tenantId, userId);

        var reader = new CustomerInsightSnapshotReader(dbContext);
        var orderHistoryReader = new EmptyOrderHistoryReader();
        var generator = new CustomerInsightSnapshotGenerator(dbContext, orderHistoryReader, tenantProvider, clock);
        var service = new CustomerInsightSnapshotService(dbContext, generator, reader, clock);
        var enumerator = new CustomerInsightSnapshotJobUserEnumerator(dbContext);
        var job = new CustomerInsightSnapshotJob(
            enumerator,
            service,
            tenantContext,
            Microsoft.Extensions.Options.Options.Create(new ScheduledJobOptions
            {
                CustomerInsightSnapshot = new CustomerInsightSnapshotJobOptions
                {
                    BatchSize = 1,
                    UserTimeoutSeconds = 10,
                    UserWarningThresholdSeconds = 10
                }
            }),
            NullLogger<CustomerInsightSnapshotJob>.Instance);

        var jobDataMap = new JobDataMap();

        // Act
        await job.ExecuteAsync(jobDataMap, CancellationToken.None);
        await job.ExecuteAsync(jobDataMap, CancellationToken.None);

        // Assert
        var snapshots = await dbContext.CustomerInsightSnapshots
            .IncludeSoftDeleted()
            .OrderBy(x => x.Version)
            .ToListAsync();

        snapshots.Should().HaveCount(1);
        snapshots[0].Status.Should().Be(CustomerInsightSnapshotContract.StatusCurrent);
    }

    private sealed class EmptyOrderHistoryReader : Aonik.SharedKernel.Abstractions.Ordering.ICustomerOrderHistoryReader
    {
        public Task<IReadOnlyList<Aonik.SharedKernel.Abstractions.Ordering.OrderHistoryItem>> GetForPartyAsync(
            Guid tenantId, Guid partyId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Aonik.SharedKernel.Abstractions.Ordering.OrderHistoryItem>>([]);

        public Task<IReadOnlyList<Aonik.SharedKernel.Abstractions.Ordering.OrderHistoryItem>> GetByIdsAsync(
            Guid tenantId, IReadOnlyCollection<Guid> orderIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Aonik.SharedKernel.Abstractions.Ordering.OrderHistoryItem>>([]);

        public Task<IReadOnlyList<Aonik.SharedKernel.Abstractions.Ordering.OrderWithPartyRolesItem>> GetRecentForPayerAsync(
            Guid tenantId, Guid payerPartyId, int take, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Aonik.SharedKernel.Abstractions.Ordering.OrderWithPartyRolesItem>>([]);

        public Task<bool> ExistsAsync(Guid tenantId, Guid orderId, CancellationToken ct = default)
            => Task.FromResult(false);
    }

    private static void SeedMinimalSnapshotUser(Aonik.PersonalFinance.Persistence.PersonalFinanceDbContext dbContext, Guid tenantId, Guid userId)
    {
        var accountId = Guid.Parse("40000000-0000-0000-0000-000000000001");

        dbContext.PersonalAccounts.Add(new PersonalAccount
        {
            Id = accountId,
            TenantId = tenantId,
            UserId = userId,
            Name = "Main Wallet",
            AccountType = "Bank",
            Currency = "USD",
            Status = "Active",
            CurrentBalance = 1000m,
            BalanceAsOf = new DateTime(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc)
        });

        dbContext.PersonalTransactions.AddRange(
            new PersonalTransaction
            {
                TenantId = tenantId,
                UserId = userId,
                PersonalAccountId = accountId,
                SourceType = "manual",
                SourceId = Guid.NewGuid(),
                OccurredAt = new DateTime(2026, 3, 5, 9, 0, 0, DateTimeKind.Utc),
                Amount = 2500m,
                Currency = "USD",
                Merchant = "Employer Inc",
                TransactionType = TransactionCategoryReference.TypeIncome,
                Category = TransactionCategoryReference.Income,
                TagsJson = "[]"
            },
            new PersonalTransaction
            {
                TenantId = tenantId,
                UserId = userId,
                PersonalAccountId = accountId,
                SourceType = "manual",
                SourceId = Guid.NewGuid(),
                OccurredAt = new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc),
                Amount = -300m,
                Currency = "USD",
                Merchant = "Fresh Market",
                TransactionType = TransactionCategoryReference.TypeExpense,
                Category = TransactionCategoryReference.Groceries,
                TagsJson = "[]"
            });

        dbContext.SaveChanges();
    }
}
