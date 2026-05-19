using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.PersonalFinance.Persistence;
using Aonik.Finance.Services.PersonalFinance;
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
}
