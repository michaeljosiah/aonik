using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.PersonalFinance;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.PersonalFinance;

public class TransactionClassificationServiceTests
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
    public async Task OverrideClassificationAsync_ShouldCreateRule_WhenCreateRuleFromCorrectionIsTrue()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        var transaction = new PersonalTransaction
        {
            TenantId = tenantId,
            UserId = userId,
            SourceType = "manual",
            SourceId = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow,
            Amount = -90m,
            Currency = "USD",
            Merchant = "Market One",
            Description = "Grocery run",
            ReviewStatus = "Pending",
            TagsJson = "[]"
        };
        context.PersonalTransactions.Add(transaction);
        await context.SaveChangesAsync();

        var service = new TransactionClassificationService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId));

        // Act
        var result = await service.OverrideClassificationAsync(
            transaction.Id,
            new OverrideTransactionClassificationRequest(
                "Groceries",
                null,
                true,
                "Market One",
                120,
                "contains"));

        // Assert
        result.Category.Should().Be("Groceries");
        result.ReviewStatus.Should().Be("Reviewed");

        var savedRule = await context.CategorisationRules.SingleAsync();
        savedRule.Pattern.Should().Be("Market One");
        savedRule.CreatedFromUserCorrection.Should().BeTrue();
    }

    [Fact]
    public async Task OverrideClassificationAsync_ShouldThrowArgumentException_WhenRuleMatchTypeIsNull()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        var transaction = new PersonalTransaction
        {
            TenantId = tenantId,
            UserId = userId,
            SourceType = "manual",
            SourceId = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow,
            Amount = -42m,
            Currency = "USD",
            Merchant = "Store",
            Description = "Test transaction",
            ReviewStatus = "Pending",
            TagsJson = "[]"
        };

        context.PersonalTransactions.Add(transaction);
        await context.SaveChangesAsync();

        var service = new TransactionClassificationService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId));

        // Act
        Func<Task> action = () => service.OverrideClassificationAsync(
            transaction.Id,
            new OverrideTransactionClassificationRequest(
                "Groceries",
                null,
                true,
                "Store",
                100,
                null!));

        // Assert
        var exception = await action.Should().ThrowAsync<ArgumentException>();
        exception.Which.ParamName.Should().Be(nameof(OverrideTransactionClassificationRequest.RuleMatchType));
    }
}
