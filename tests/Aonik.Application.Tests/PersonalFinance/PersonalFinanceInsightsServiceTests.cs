using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Persistence;
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

    private static FinanceDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;

        return new FinanceDbContext(options, new TestTenantProvider(tenantId));
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
}
