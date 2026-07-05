using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Finance.Readers;
using Aonik.Finance.Services.PersonalFinance;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Finance;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.PersonalFinance;

public class CustomerInsightSnapshotServiceTests
{
    private sealed class TestTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;

        public TestTenantProvider(Guid tenantId)
        {
            _tenantId = tenantId;
        }

        public Guid GetCurrentTenantId() => _tenantId;

        public bool TryGetCurrentTenantId(out Guid tenantId)
        {
            tenantId = _tenantId;
            return true;
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

    private static string _lastDbName = string.Empty;

    private static FinanceDbContext CreateDbContext(Guid tenantId)
    {
        _lastDbName = $"CustomerInsightSnapshot_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseInMemoryDatabase(_lastDbName)
            .Options;

        return new FinanceDbContext(options, new TestTenantProvider(tenantId));
    }

    private static PersonalFinanceDbContext CreatePersonalFinanceDbContext(string sharedDbName, Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<PersonalFinanceDbContext>()
            .UseInMemoryDatabase(sharedDbName)
            .Options;
        return new PersonalFinanceDbContext(options, new TestTenantProvider(tenantId));
    }

    private static CustomerInsightSnapshotService CreateService(FinanceDbContext context, Guid tenantId, TestClock clock)
    {
        var pfContext = CreatePersonalFinanceDbContext(_lastDbName, tenantId);
        var orderReader = new CustomerOrderHistoryReader(context);
        var generator = new CustomerInsightSnapshotGenerator(pfContext, orderReader, new TestTenantProvider(tenantId), clock);
        var reader = new CustomerInsightSnapshotReader(pfContext);
        return new CustomerInsightSnapshotService(pfContext, generator, reader, clock);
    }

    [Fact]
    public async Task GenerateCurrentSnapshotAsync_ShouldBuildDeterministicMetricsAndSignals()
    {
        // Arrange
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var userId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var clock = new TestClock(new DateTime(2026, 3, 31, 10, 0, 0, DateTimeKind.Utc));
        using var context = CreateDbContext(tenantId);

        SeedSnapshotScenario(context, tenantId, userId, reverseOrder: false);
        var service = CreateService(context, tenantId, clock);

        // Act
        var result = await service.GenerateCurrentSnapshotAsync(userId);

        // Assert
        result.Status.Should().Be(CustomerInsightSnapshotContract.StatusCurrent);
        result.Version.Should().Be(1);
        result.SourceHash.Should().NotBeNullOrWhiteSpace();
        result.Snapshot.Should().NotBeNull();

        var snapshot = result.Snapshot!;
        snapshot.CurrencyPolicy.CanonicalMonetaryView.Should().Be(CustomerInsightSnapshotContract.MonetaryPolicyNativeCurrency);
        snapshot.Currencies.Should().ContainInOrder("EUR", "USD");
        snapshot.Coverage.IsPartial.Should().BeFalse();

        snapshot.Metrics.CashPosition.TotalBalanceByCurrency.Should().ContainEquivalentOf(new CustomerInsightMoneyAmount("USD", 1000m));
        snapshot.Metrics.CashPosition.TotalBalanceByCurrency.Should().ContainEquivalentOf(new CustomerInsightMoneyAmount("EUR", 400m));

        snapshot.Metrics.Income.TotalInflowsByCurrency.Should().ContainSingle(x => x.Currency == "USD" && x.Amount == 3000m);
        snapshot.Metrics.Expense.TotalOutflowsByCurrency.Should().ContainSingle(x => x.Currency == "USD" && x.Amount == 2520m);
        snapshot.Metrics.Expense.FixedSpendEstimateByCurrency.Should().ContainSingle(x => x.Currency == "USD" && x.Amount > 0m);

        snapshot.Metrics.Categories.TopCategoriesByAmount.Should().Contain(x => x.Currency == "USD" && x.Category == "housing" && x.Amount == 1200m);
        snapshot.Metrics.Categories.CategoryTrendDeltas.Should().Contain(x => x.Currency == "USD" && x.Category == "entertainment" && x.DeltaPercentage == 600m);

        snapshot.Metrics.Obligations.TotalUpcomingByCurrency.Should().ContainSingle(x => x.Currency == "USD" && x.Amount == 230m);
        snapshot.Metrics.Obligations.CoverageRatios.Should().ContainSingle(x => x.Currency == "USD" && x.Ratio == 4.35m);
        snapshot.Metrics.Obligations.SupportObligations.Should().ContainSingle(x => x.DisplayName == "Water Support");

        snapshot.Metrics.Budgets.OverspentCategories.Should().ContainSingle(x => x.Category == "Entertainment" && x.PercentUsed == 140m);
        snapshot.Metrics.Goals.ActiveGoals.Should().ContainSingle(x => x.Name == "Emergency Fund" && x.EstimatedMonthsToTarget.HasValue && x.EstimatedMonthlyContribution > 0m);
        snapshot.Metrics.Goals.SavingsContributionConsistency.Should().Be(CustomerInsightSnapshotContract.ConfidenceLow);

        snapshot.Signals.Should().Contain(x => x.SignalKey == "recurring_commitment_growth:USD");
        snapshot.Signals.Should().Contain(x => x.SignalKey == "category_spend_acceleration:USD:entertainment");
        snapshot.Signals.Should().Contain(x => x.SignalKey == "dormant_subscription:SPOTIFY");
        snapshot.Signals.Should().Contain(x => x.SignalKey == "budget_pressure:USD:Entertainment");

        snapshot.Evidence.ConfirmedTransferCount.Should().Be(2);
        snapshot.Evidence.Warnings.Should().ContainSingle(x => x.Contains("Transfer exclusion only applies", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GenerateCurrentSnapshotAsync_ShouldKeepStableHash_WhenLogicalInputOrderChanges()
    {
        // Arrange
        var tenantId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var userId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var clock = new TestClock(new DateTime(2026, 3, 31, 10, 0, 0, DateTimeKind.Utc));

        using var firstContext = CreateDbContext(tenantId);
        SeedSnapshotScenario(firstContext, tenantId, userId, reverseOrder: false);
        var firstService = CreateService(firstContext, tenantId, clock);

        using var secondContext = CreateDbContext(tenantId);
        SeedSnapshotScenario(secondContext, tenantId, userId, reverseOrder: true);
        var secondService = CreateService(secondContext, tenantId, clock);

        // Act
        var first = await firstService.GenerateCurrentSnapshotAsync(userId);
        var second = await secondService.GenerateCurrentSnapshotAsync(userId);

        // Assert
        first.SourceHash.Should().Be(second.SourceHash);
        first.Snapshot.Should().BeEquivalentTo(second.Snapshot);
    }

    [Fact]
    public async Task GenerateCurrentSnapshotAsync_ShouldReuseCurrentSnapshot_WhenSourceHashIsUnchanged()
    {
        // Arrange
        var tenantId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var userId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var clock = new TestClock(new DateTime(2026, 3, 31, 10, 0, 0, DateTimeKind.Utc));
        using var context = CreateDbContext(tenantId);

        SeedSnapshotScenario(context, tenantId, userId, reverseOrder: false);
        var service = CreateService(context, tenantId, clock);

        // Act
        var first = await service.GenerateCurrentSnapshotAsync(userId);
        var second = await service.GenerateCurrentSnapshotAsync(userId);

        // Assert
        second.Id.Should().Be(first.Id);
        second.Version.Should().Be(1);
        await using var pfVerifyContext = CreatePersonalFinanceDbContext(_lastDbName, tenantId);
        (await pfVerifyContext.CustomerInsightSnapshots.CountAsync()).Should().Be(1);
        (await pfVerifyContext.CustomerInsightSnapshots.SingleAsync()).Status.Should().Be(CustomerInsightSnapshotContract.StatusCurrent);
    }

    [Fact]
    public async Task GenerateCurrentSnapshotAsync_ShouldSupersedeCurrentSnapshot_WhenSourceDataChanges()
    {
        // Arrange
        var tenantId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        var userId = Guid.Parse("88888888-8888-8888-8888-888888888888");
        var clock = new TestClock(new DateTime(2026, 3, 31, 10, 0, 0, DateTimeKind.Utc));
        using var context = CreateDbContext(tenantId);

        SeedSnapshotScenario(context, tenantId, userId, reverseOrder: false);
        var service = CreateService(context, tenantId, clock);

        var first = await service.GenerateCurrentSnapshotAsync(userId);

        await using var pfSeedContext = CreatePersonalFinanceDbContext(_lastDbName, tenantId);
        pfSeedContext.PersonalTransactions.Add(new PersonalTransaction
        {
            Id = Guid.Parse("99999999-9999-9999-9999-999999999999"),
            TenantId = tenantId,
            UserId = userId,
            PersonalAccountId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            SourceType = "manual",
            SourceId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            OccurredAt = new DateTime(2026, 3, 30, 13, 0, 0, DateTimeKind.Utc),
            Amount = -75m,
            Currency = "USD",
            Merchant = "Fresh Market",
            TransactionType = TransactionCategoryReference.TypeExpense,
            Category = TransactionCategoryReference.Groceries,
            TagsJson = "[]"
        });
        await pfSeedContext.SaveChangesAsync();

        // Act
        var second = await service.GenerateCurrentSnapshotAsync(userId);

        // Assert
        second.Id.Should().NotBe(first.Id);
        second.Version.Should().Be(2);

        await using var pfVerifyContext = CreatePersonalFinanceDbContext(_lastDbName, tenantId);
        var snapshots = await pfVerifyContext.CustomerInsightSnapshots
            .IncludeSoftDeleted()
            .OrderBy(x => x.Version)
            .ToListAsync();

        snapshots.Should().HaveCount(2);
        snapshots[0].Status.Should().Be(CustomerInsightSnapshotContract.StatusSuperseded);
        snapshots[0].SupersededById.Should().Be(snapshots[1].Id);
        snapshots[1].Status.Should().Be(CustomerInsightSnapshotContract.StatusCurrent);
        snapshots[1].SourceHash.Should().NotBe(first.SourceHash);
    }

    private static void SeedSnapshotScenario(FinanceDbContext context, Guid tenantId, Guid userId, bool reverseOrder)
    {
        var mainAccountId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var eurAccountId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var budgetId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var entertainmentBudgetLineId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var groceriesBudgetLineId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

        var pf = CreatePersonalFinanceDbContext(_lastDbName, tenantId);

        pf.PersonalAccounts.AddRange(
            new PersonalAccount
            {
                Id = mainAccountId,
                TenantId = tenantId,
                UserId = userId,
                Name = "Main Wallet",
                AccountType = "Bank",
                Currency = "USD",
                Status = "Active",
                CurrentBalance = 1000m,
                BalanceAsOf = new DateTime(2026, 3, 31, 9, 0, 0, DateTimeKind.Utc)
            },
            new PersonalAccount
            {
                Id = eurAccountId,
                TenantId = tenantId,
                UserId = userId,
                Name = "Euro Savings",
                AccountType = "Savings",
                Currency = "EUR",
                Status = "Active",
                CurrentBalance = 400m,
                BalanceAsOf = new DateTime(2026, 3, 31, 9, 0, 0, DateTimeKind.Utc)
            });

        pf.Bills.AddRange(
            new Bill
            {
                Id = Guid.Parse("f1111111-1111-1111-1111-111111111111"),
                TenantId = tenantId,
                UserId = userId,
                Payee = "Electric Co",
                Frequency = "monthly",
                NextDueDate = new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc),
                ExpectedAmount = 150m,
                Currency = "USD",
                Status = "Active"
            },
            new Bill
            {
                Id = Guid.Parse("f2222222-2222-2222-2222-222222222222"),
                TenantId = tenantId,
                UserId = userId,
                Payee = "Water Support",
                Frequency = "monthly",
                NextDueDate = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
                ExpectedAmount = 50m,
                Currency = "USD",
                LinkedOrderId = Guid.Parse("f3333333-3333-3333-3333-333333333333"),
                Status = "Active"
            });

        pf.Subscriptions.AddRange(
            new Subscription
            {
                Id = Guid.Parse("f4444444-4444-4444-4444-444444444444"),
                TenantId = tenantId,
                UserId = userId,
                Merchant = "Netflix",
                RenewalDate = new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc),
                ExpectedAmount = 20m,
                Currency = "USD",
                Status = "Active",
                DetectedBy = "Seed"
            },
            new Subscription
            {
                Id = Guid.Parse("f5555555-5555-5555-5555-555555555555"),
                TenantId = tenantId,
                UserId = userId,
                Merchant = "Spotify",
                RenewalDate = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc),
                ExpectedAmount = 10m,
                Currency = "USD",
                Status = "Active",
                DetectedBy = "Seed"
            });

        pf.Budgets.Add(new Budget
        {
            Id = budgetId,
            TenantId = tenantId,
            UserId = userId,
            PeriodType = "Monthly",
            PeriodStart = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            BudgetCreatedBy = "User",
            Status = "Active",
            Lines =
            [
                new BudgetLine
                {
                    Id = entertainmentBudgetLineId,
                    TenantId = tenantId,
                    BudgetId = budgetId,
                    Category = "entertainment",
                    LimitAmount = 500m,
                    Currency = "USD"
                },
                new BudgetLine
                {
                    Id = groceriesBudgetLineId,
                    TenantId = tenantId,
                    BudgetId = budgetId,
                    Category = "groceries",
                    LimitAmount = 350m,
                    Currency = "USD"
                }
            ]
        });

        pf.Goals.Add(new Goal
        {
            Id = Guid.Parse("f6666666-6666-6666-6666-666666666666"),
            TenantId = tenantId,
            UserId = userId,
            Name = "Emergency Fund",
            TargetAmount = 5000m,
            ProgressAmount = 2000m,
            Currency = "USD",
            TargetDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            Status = "Active"
        });

        var transactions = new List<PersonalTransaction>
        {
            BuildTransaction(Guid.Parse("10000000-0000-0000-0000-000000000001"), tenantId, userId, mainAccountId, new DateTime(2026, 1, 5, 9, 0, 0, DateTimeKind.Utc), 4000m, "USD", "Employer Inc", TransactionCategoryReference.TypeIncome, TransactionCategoryReference.Income),
            BuildTransaction(Guid.Parse("10000000-0000-0000-0000-000000000002"), tenantId, userId, mainAccountId, new DateTime(2026, 1, 18, 9, 0, 0, DateTimeKind.Utc), -300m, "USD", "Savings Pot", TransactionCategoryReference.TypeExpense, TransactionCategoryReference.Savings),
            BuildTransaction(Guid.Parse("10000000-0000-0000-0000-000000000003"), tenantId, userId, mainAccountId, new DateTime(2026, 1, 20, 9, 0, 0, DateTimeKind.Utc), -60m, "USD", "Loan Servicer", TransactionCategoryReference.TypeExpense, TransactionCategoryReference.LoanPayments),
            BuildTransaction(Guid.Parse("10000000-0000-0000-0000-000000000004"), tenantId, userId, mainAccountId, new DateTime(2026, 1, 26, 20, 0, 0, DateTimeKind.Utc), -80m, "USD", "Cinema World", TransactionCategoryReference.TypeExpense, TransactionCategoryReference.Entertainment),
            BuildTransaction(Guid.Parse("10000000-0000-0000-0000-000000000005"), tenantId, userId, mainAccountId, new DateTime(2026, 2, 5, 9, 0, 0, DateTimeKind.Utc), 3000m, "USD", "Employer Inc", TransactionCategoryReference.TypeIncome, TransactionCategoryReference.Income),
            BuildTransaction(Guid.Parse("10000000-0000-0000-0000-000000000006"), tenantId, userId, mainAccountId, new DateTime(2026, 2, 6, 8, 0, 0, DateTimeKind.Utc), -1200m, "USD", "Landlord", TransactionCategoryReference.TypeExpense, TransactionCategoryReference.Housing),
            BuildTransaction(Guid.Parse("10000000-0000-0000-0000-000000000007"), tenantId, userId, mainAccountId, new DateTime(2026, 2, 15, 12, 0, 0, DateTimeKind.Utc), -20m, "USD", "Netflix", TransactionCategoryReference.TypeExpense, TransactionCategoryReference.Subscriptions),
            BuildTransaction(Guid.Parse("10000000-0000-0000-0000-000000000008"), tenantId, userId, mainAccountId, new DateTime(2026, 2, 18, 12, 0, 0, DateTimeKind.Utc), -150m, "USD", "Savings Pot", TransactionCategoryReference.TypeExpense, TransactionCategoryReference.Savings),
            BuildTransaction(Guid.Parse("10000000-0000-0000-0000-000000000009"), tenantId, userId, mainAccountId, new DateTime(2026, 2, 20, 10, 0, 0, DateTimeKind.Utc), -80m, "USD", "Loan Servicer", TransactionCategoryReference.TypeExpense, TransactionCategoryReference.LoanPayments),
            BuildTransaction(Guid.Parse("10000000-0000-0000-0000-000000000010"), tenantId, userId, mainAccountId, new DateTime(2026, 2, 25, 20, 0, 0, DateTimeKind.Utc), -100m, "USD", "Cinema World", TransactionCategoryReference.TypeExpense, TransactionCategoryReference.Entertainment),
            BuildTransaction(Guid.Parse("10000000-0000-0000-0000-000000000011"), tenantId, userId, mainAccountId, new DateTime(2026, 3, 5, 9, 0, 0, DateTimeKind.Utc), 3000m, "USD", "Employer Inc", TransactionCategoryReference.TypeIncome, TransactionCategoryReference.Income),
            BuildTransaction(Guid.Parse("10000000-0000-0000-0000-000000000012"), tenantId, userId, mainAccountId, new DateTime(2026, 3, 6, 8, 0, 0, DateTimeKind.Utc), -1200m, "USD", "Landlord", TransactionCategoryReference.TypeExpense, TransactionCategoryReference.Housing),
            BuildTransaction(Guid.Parse("10000000-0000-0000-0000-000000000013"), tenantId, userId, mainAccountId, new DateTime(2026, 3, 7, 12, 0, 0, DateTimeKind.Utc), -300m, "USD", "Fresh Market", TransactionCategoryReference.TypeExpense, TransactionCategoryReference.Groceries),
            BuildTransaction(Guid.Parse("10000000-0000-0000-0000-000000000014"), tenantId, userId, mainAccountId, new DateTime(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc), -500m, "USD", "Internal Transfer", TransactionCategoryReference.TypeTransfer, TransactionCategoryReference.TransferOut, "own_account"),
            BuildTransaction(Guid.Parse("10000000-0000-0000-0000-000000000015"), tenantId, userId, eurAccountId, new DateTime(2026, 3, 11, 9, 0, 0, DateTimeKind.Utc), 450m, "EUR", "Internal Transfer", TransactionCategoryReference.TypeTransfer, TransactionCategoryReference.TransferIn, "own_account"),
            BuildTransaction(Guid.Parse("10000000-0000-0000-0000-000000000016"), tenantId, userId, mainAccountId, new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc), -20m, "USD", "Netflix", TransactionCategoryReference.TypeExpense, TransactionCategoryReference.Subscriptions),
            BuildTransaction(Guid.Parse("10000000-0000-0000-0000-000000000017"), tenantId, userId, mainAccountId, new DateTime(2026, 3, 18, 12, 0, 0, DateTimeKind.Utc), -200m, "USD", "Savings Pot", TransactionCategoryReference.TypeExpense, TransactionCategoryReference.Savings),
            BuildTransaction(Guid.Parse("10000000-0000-0000-0000-000000000018"), tenantId, userId, mainAccountId, new DateTime(2026, 3, 20, 10, 0, 0, DateTimeKind.Utc), -100m, "USD", "Loan Servicer", TransactionCategoryReference.TypeExpense, TransactionCategoryReference.LoanPayments),
            BuildTransaction(Guid.Parse("10000000-0000-0000-0000-000000000019"), tenantId, userId, mainAccountId, new DateTime(2026, 3, 26, 20, 0, 0, DateTimeKind.Utc), -450m, "USD", "Cinema World", TransactionCategoryReference.TypeExpense, TransactionCategoryReference.Entertainment),
            BuildTransaction(Guid.Parse("10000000-0000-0000-0000-000000000020"), tenantId, userId, mainAccountId, new DateTime(2026, 3, 28, 20, 0, 0, DateTimeKind.Utc), -250m, "USD", "Cinema World", TransactionCategoryReference.TypeExpense, TransactionCategoryReference.Entertainment)
        };

        if (reverseOrder)
        {
            transactions.Reverse();
        }

        pf.PersonalTransactions.AddRange(transactions);
        pf.SaveChanges();
    }

    private static PersonalTransaction BuildTransaction(
        Guid id,
        Guid tenantId,
        Guid userId,
        Guid accountId,
        DateTime occurredAt,
        decimal amount,
        string currency,
        string merchant,
        string transactionType,
        string category,
        string? subCategory = null)
    {
        return new PersonalTransaction
        {
            Id = id,
            TenantId = tenantId,
            UserId = userId,
            PersonalAccountId = accountId,
            SourceType = "manual",
            SourceId = Guid.NewGuid(),
            OccurredAt = occurredAt,
            Amount = amount,
            Currency = currency,
            Merchant = merchant,
            TransactionType = transactionType,
            Category = category,
            SubCategory = subCategory,
            TagsJson = "[]"
        };
    }
}
