using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Entities;
using Aonik.PersonalFinance.Persistence;
using Aonik.PersonalFinance.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.PersonalFinance;

public class PersonalFinanceInsightsServiceTests
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

    private sealed class TestCurrentUserProvider : ICurrentUserProvider
    {
        private readonly Guid _userId;

        public TestCurrentUserProvider(Guid userId)
        {
            _userId = userId;
        }

        public Guid? GetCurrentUserId() => _userId;

        public bool TryGetCurrentUserId(out Guid userId)
        {
            userId = _userId;
            return true;
        }
    }

    private static PersonalFinanceDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<PersonalFinanceDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;

        return new PersonalFinanceDbContext(options, new TestTenantProvider(tenantId));
    }

    [Fact]
    public async Task GetSpendingSummaryAsync_ShouldReturnIncomeExpenseAndNet()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        context.PersonalTransactions.AddRange(
            new PersonalTransaction
            {
                TenantId = tenantId,
                UserId = userId,
                SourceType = "manual",
                SourceId = Guid.NewGuid(),
                OccurredAt = DateTime.UtcNow.AddDays(-2),
                Amount = 2000m,
                Currency = "USD",
                TagsJson = "[]"
            },
            new PersonalTransaction
            {
                TenantId = tenantId,
                UserId = userId,
                SourceType = "manual",
                SourceId = Guid.NewGuid(),
                OccurredAt = DateTime.UtcNow.AddDays(-1),
                Amount = -350m,
                Currency = "USD",
                Category = "Groceries",
                TagsJson = "[]"
            });

        await context.SaveChangesAsync();

        var service = new PersonalFinanceInsightsService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId));

        // Act
        var result = await service.GetSpendingSummaryAsync(DateTime.UtcNow.AddDays(-10), DateTime.UtcNow);

        // Assert
        result.TotalIncome.Should().Be(2000m);
        result.TotalExpense.Should().Be(350m);
        result.NetAmount.Should().Be(1650m);
        result.TransactionCount.Should().Be(2);
    }

    [Fact]
    public async Task GetAccountBreakdownAsync_ShouldIncludeOnlyExpenseTotalsPerAccount()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        var accountA = new PersonalAccount
        {
            TenantId = tenantId,
            UserId = userId,
            Name = "Main Account",
            AccountType = "Bank",
            Currency = "USD",
            Status = "Active"
        };

        var accountB = new PersonalAccount
        {
            TenantId = tenantId,
            UserId = userId,
            Name = "Card Account",
            AccountType = "CreditCard",
            Currency = "USD",
            Status = "Active"
        };

        context.PersonalAccounts.AddRange(accountA, accountB);
        await context.SaveChangesAsync();

        context.PersonalTransactions.AddRange(
            new PersonalTransaction
            {
                TenantId = tenantId,
                UserId = userId,
                PersonalAccountId = accountA.Id,
                SourceType = "manual",
                SourceId = Guid.NewGuid(),
                OccurredAt = DateTime.UtcNow.AddDays(-3),
                Amount = -100m,
                Currency = "USD",
                TagsJson = "[]"
            },
            new PersonalTransaction
            {
                TenantId = tenantId,
                UserId = userId,
                PersonalAccountId = accountA.Id,
                SourceType = "manual",
                SourceId = Guid.NewGuid(),
                OccurredAt = DateTime.UtcNow.AddDays(-2),
                Amount = 300m,
                Currency = "USD",
                TagsJson = "[]"
            },
            new PersonalTransaction
            {
                TenantId = tenantId,
                UserId = userId,
                PersonalAccountId = accountB.Id,
                SourceType = "manual",
                SourceId = Guid.NewGuid(),
                OccurredAt = DateTime.UtcNow.AddDays(-1),
                Amount = -50m,
                Currency = "USD",
                TagsJson = "[]"
            },
            new PersonalTransaction
            {
                TenantId = tenantId,
                UserId = userId,
                PersonalAccountId = null,
                SourceType = "manual",
                SourceId = Guid.NewGuid(),
                OccurredAt = DateTime.UtcNow.AddHours(-12),
                Amount = -20m,
                Currency = "USD",
                TagsJson = "[]"
            });

        await context.SaveChangesAsync();

        var service = new PersonalFinanceInsightsService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId));

        // Act
        var result = await service.GetAccountBreakdownAsync(DateTime.UtcNow.AddDays(-10), DateTime.UtcNow);

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(item => item.PersonalAccountId == accountA.Id && item.TotalAmount == 100m && item.TransactionCount == 1);
        result.Should().Contain(item => item.PersonalAccountId == accountB.Id && item.TotalAmount == 50m && item.TransactionCount == 1);
        result.Should().Contain(item => item.PersonalAccountId == null && item.TotalAmount == 20m && item.TransactionCount == 1);
    }

    [Fact]
    public async Task GetCategoryBreakdownAsync_ShouldUseDominantExpenseCurrency_WhenPeriodContainsMultipleExpenseCurrencies()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        context.PersonalTransactions.AddRange(
            new PersonalTransaction
            {
                TenantId = tenantId,
                UserId = userId,
                SourceType = "manual",
                SourceId = Guid.NewGuid(),
                OccurredAt = DateTime.UtcNow.AddDays(-3),
                Amount = -100m,
                Currency = "GBP",
                Category = "Groceries",
                TagsJson = "[]"
            },
            new PersonalTransaction
            {
                TenantId = tenantId,
                UserId = userId,
                SourceType = "manual",
                SourceId = Guid.NewGuid(),
                OccurredAt = DateTime.UtcNow.AddDays(-2),
                Amount = -50m,
                Currency = "GBP",
                Category = "Dining",
                TagsJson = "[]"
            },
            new PersonalTransaction
            {
                TenantId = tenantId,
                UserId = userId,
                SourceType = "manual",
                SourceId = Guid.NewGuid(),
                OccurredAt = DateTime.UtcNow.AddDays(-1),
                Amount = -40m,
                Currency = "USD",
                Category = "Travel",
                TagsJson = "[]"
            });

        await context.SaveChangesAsync();

        var service = new PersonalFinanceInsightsService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId));

        // Act
        var result = await service.GetCategoryBreakdownAsync(DateTime.UtcNow.AddDays(-10), DateTime.UtcNow);

        // Assert
        result.Should().HaveCount(2);
        result.Should().ContainEquivalentOf(new CategorySpendingItemResponse("Groceries", "GBP", 100m, 66.67m, 1));
        result.Should().ContainEquivalentOf(new CategorySpendingItemResponse("Dining", "GBP", 50m, 33.33m, 1));
        result.Should().NotContain(item => item.Category == "Travel");
    }

    [Fact]
    public async Task GetMerchantBreakdownAsync_ShouldUseDominantExpenseCurrency_WhenPeriodContainsMultipleExpenseCurrencies()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        context.PersonalTransactions.AddRange(
            new PersonalTransaction
            {
                TenantId = tenantId,
                UserId = userId,
                SourceType = "manual",
                SourceId = Guid.NewGuid(),
                OccurredAt = DateTime.UtcNow.AddDays(-3),
                Amount = -120m,
                Currency = "GBP",
                Merchant = "Tesco",
                TagsJson = "[]"
            },
            new PersonalTransaction
            {
                TenantId = tenantId,
                UserId = userId,
                SourceType = "manual",
                SourceId = Guid.NewGuid(),
                OccurredAt = DateTime.UtcNow.AddDays(-2),
                Amount = -45m,
                Currency = "GBP",
                Merchant = "Pret",
                TagsJson = "[]"
            },
            new PersonalTransaction
            {
                TenantId = tenantId,
                UserId = userId,
                SourceType = "manual",
                SourceId = Guid.NewGuid(),
                OccurredAt = DateTime.UtcNow.AddDays(-1),
                Amount = -40m,
                Currency = "USD",
                Merchant = "Amazon",
                TagsJson = "[]"
            });

        await context.SaveChangesAsync();

        var service = new PersonalFinanceInsightsService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId));

        // Act
        var result = await service.GetMerchantBreakdownAsync(DateTime.UtcNow.AddDays(-10), DateTime.UtcNow);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(item => item.Merchant == "Tesco" && item.Currency == "GBP" && item.TotalAmount == 120m && item.TransactionCount == 1);
        result.Should().Contain(item => item.Merchant == "Pret" && item.Currency == "GBP" && item.TotalAmount == 45m && item.TransactionCount == 1);
        result.Should().NotContain(item => item.Merchant == "Amazon");
    }

    [Fact]
    public async Task GetSpendingSummaryAsync_ShouldThrowArgumentException_WhenPeriodContainsMultipleCurrencies()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        context.PersonalTransactions.AddRange(
            new PersonalTransaction
            {
                TenantId = tenantId,
                UserId = userId,
                SourceType = "manual",
                SourceId = Guid.NewGuid(),
                OccurredAt = DateTime.UtcNow.AddDays(-2),
                Amount = -120m,
                Currency = "USD",
                TagsJson = "[]"
            },
            new PersonalTransaction
            {
                TenantId = tenantId,
                UserId = userId,
                SourceType = "manual",
                SourceId = Guid.NewGuid(),
                OccurredAt = DateTime.UtcNow.AddDays(-1),
                Amount = -80m,
                Currency = "EUR",
                TagsJson = "[]"
            });

        await context.SaveChangesAsync();

        var service = new PersonalFinanceInsightsService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId));

        // Act
        Func<Task> action = () => service.GetSpendingSummaryAsync(DateTime.UtcNow.AddDays(-10), DateTime.UtcNow);

        // Assert
        var exception = await action.Should().ThrowAsync<ArgumentException>();
        exception.Which.Message.Should().Contain("multiple currencies");
    }

    [Fact]
    public async Task GetCategoryBreakdownAsync_ShouldFoldNullAndEmptyCategory_IntoUncategorized()
    {
        // H1 (SQL aggregation): the GROUP BY item.Category runs in the database and produces
        // separate null and empty-string groups; the service must fold both into a single
        // "uncategorized" bucket in memory with the combined total, count, and percentage.
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        context.PersonalTransactions.AddRange(
            new PersonalTransaction
            {
                TenantId = tenantId,
                UserId = userId,
                SourceType = "manual",
                SourceId = Guid.NewGuid(),
                OccurredAt = DateTime.UtcNow.AddDays(-3),
                Amount = -60m,
                Currency = "USD",
                Category = null,
                TagsJson = "[]"
            },
            new PersonalTransaction
            {
                TenantId = tenantId,
                UserId = userId,
                SourceType = "manual",
                SourceId = Guid.NewGuid(),
                OccurredAt = DateTime.UtcNow.AddDays(-2),
                Amount = -40m,
                Currency = "USD",
                Category = "",
                TagsJson = "[]"
            },
            new PersonalTransaction
            {
                TenantId = tenantId,
                UserId = userId,
                SourceType = "manual",
                SourceId = Guid.NewGuid(),
                OccurredAt = DateTime.UtcNow.AddDays(-1),
                Amount = -100m,
                Currency = "USD",
                Category = "Rent",
                TagsJson = "[]"
            });

        await context.SaveChangesAsync();

        var service = new PersonalFinanceInsightsService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId));

        // Act
        var result = await service.GetCategoryBreakdownAsync(DateTime.UtcNow.AddDays(-10), DateTime.UtcNow);

        // Assert
        result.Should().HaveCount(2);
        var uncategorized = result.Single(item => item.Category == "uncategorized");
        uncategorized.TotalAmount.Should().Be(100m); // -60 and -40 folded together
        uncategorized.TransactionCount.Should().Be(2);
        uncategorized.Percentage.Should().Be(50.00m); // 100 of 200 total expense
        result.Single(item => item.Category == "Rent").TotalAmount.Should().Be(100m);
    }

    [Fact]
    public async Task GetMerchantBreakdownAsync_ShouldFoldNullAndEmptyMerchant_IntoUnknownMerchant()
    {
        // H1: the GROUP BY item.Merchant aggregate yields distinct null and empty-string groups;
        // the service folds both into "Unknown Merchant".
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        context.PersonalTransactions.AddRange(
            new PersonalTransaction
            {
                TenantId = tenantId,
                UserId = userId,
                SourceType = "manual",
                SourceId = Guid.NewGuid(),
                OccurredAt = DateTime.UtcNow.AddDays(-3),
                Amount = -30m,
                Currency = "USD",
                Merchant = null,
                TagsJson = "[]"
            },
            new PersonalTransaction
            {
                TenantId = tenantId,
                UserId = userId,
                SourceType = "manual",
                SourceId = Guid.NewGuid(),
                OccurredAt = DateTime.UtcNow.AddDays(-2),
                Amount = -20m,
                Currency = "USD",
                Merchant = "",
                TagsJson = "[]"
            },
            new PersonalTransaction
            {
                TenantId = tenantId,
                UserId = userId,
                SourceType = "manual",
                SourceId = Guid.NewGuid(),
                OccurredAt = DateTime.UtcNow.AddDays(-1),
                Amount = -50m,
                Currency = "USD",
                Merchant = "Spotify",
                TagsJson = "[]"
            });

        await context.SaveChangesAsync();

        var service = new PersonalFinanceInsightsService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId));

        // Act
        var result = await service.GetMerchantBreakdownAsync(DateTime.UtcNow.AddDays(-10), DateTime.UtcNow);

        // Assert
        result.Should().HaveCount(2);
        var unknown = result.Single(item => item.Merchant == "Unknown Merchant");
        unknown.TotalAmount.Should().Be(50m); // -30 and -20 folded together
        unknown.TransactionCount.Should().Be(2);
        result.Single(item => item.Merchant == "Spotify").TotalAmount.Should().Be(50m);
    }

    [Fact]
    public async Task GetMerchantHistoryAsync_ShouldCountAndSumExpensesOnly_IgnoringIncome()
    {
        // H1: CountAsync + SumAsync run in SQL over the expense predicate; income rows for the
        // same merchant must be excluded from the count, total, and average.
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        context.PersonalTransactions.AddRange(
            new PersonalTransaction
            {
                TenantId = tenantId,
                UserId = userId,
                SourceType = "manual",
                SourceId = Guid.NewGuid(),
                OccurredAt = DateTime.UtcNow.AddDays(-3),
                Amount = -100m,
                Currency = "GBP",
                Merchant = "Tesco",
                TagsJson = "[]"
            },
            new PersonalTransaction
            {
                TenantId = tenantId,
                UserId = userId,
                SourceType = "manual",
                SourceId = Guid.NewGuid(),
                OccurredAt = DateTime.UtcNow.AddDays(-2),
                Amount = -50m,
                Currency = "GBP",
                Merchant = "Tesco",
                TagsJson = "[]"
            },
            new PersonalTransaction
            {
                TenantId = tenantId,
                UserId = userId,
                SourceType = "manual",
                SourceId = Guid.NewGuid(),
                OccurredAt = DateTime.UtcNow.AddDays(-1),
                Amount = 200m, // income for the same merchant — must be excluded
                Currency = "GBP",
                Merchant = "Tesco",
                TagsJson = "[]"
            });

        await context.SaveChangesAsync();

        var service = new PersonalFinanceInsightsService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId));

        // Act
        var result = await service.GetMerchantHistoryAsync("Tesco");

        // Assert — two expense rows totalling 150 (average 75); the 200 income row is ignored.
        // Format expectations with the same N2 call the service uses so the assertion is
        // culture-independent.
        result.TransactionCountLabel.Should().Be("2");
        result.TotalSpentLabel.Should().Be($"£{150m:N2}");
        result.AverageSpendLabel.Should().Be($"£{75m:N2}");
    }
}
