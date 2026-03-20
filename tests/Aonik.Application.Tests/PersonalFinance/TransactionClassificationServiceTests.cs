using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.PersonalFinance;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

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

    private sealed class NoOpGraphCacheInvalidator : IFinancialLifeGraphCacheInvalidator
    {
        public void InvalidateCurrentUserGraph()
        {
        }

        public Task InvalidateCurrentUserGraphAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task InvalidateAllGraphCachesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    /// <summary>
    /// Stub AI classifier that never classifies anything — used to test rule-only paths.
    /// </summary>
    private sealed class NoOpAiClassifier : ITransactionAiClassifier
    {
        public Task<bool> ClassifyAsync(PersonalTransaction transaction, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<int> ClassifyBatchAsync(IReadOnlyList<PersonalTransaction> transactions, CancellationToken cancellationToken = default)
            => Task.FromResult(0);
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
            new TestCurrentUserProvider(userId),
            new NoOpGraphCacheInvalidator(),
            new NoOpAiClassifier(),
            NullLogger<TransactionClassificationService>.Instance);

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
            new TestCurrentUserProvider(userId),
            new NoOpGraphCacheInvalidator(),
            new NoOpAiClassifier(),
            NullLogger<TransactionClassificationService>.Instance);

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

    [Fact]
    public async Task AcceptClassificationAsync_ShouldMatchMerchantRule_WhenDescriptionAlsoExists()
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
            Amount = -80m,
            Currency = "USD",
            Merchant = "SuperMart",
            Description = "POS REF 8891",
            ReviewStatus = "Pending",
            TagsJson = "[]"
        };

        var rule = new CategorisationRule
        {
            TenantId = tenantId,
            UserId = userId,
            Pattern = "SuperMart",
            Category = "Groceries",
            Priority = 200,
            IsActive = true,
            MatchType = "contains",
            CaseSensitive = false,
            Scope = "User",
            ApprovalStatus = "Approved"
        };

        context.PersonalTransactions.Add(transaction);
        context.CategorisationRules.Add(rule);
        await context.SaveChangesAsync();

        var service = new TransactionClassificationService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId),
            new NoOpGraphCacheInvalidator(),
            new NoOpAiClassifier(),
            NullLogger<TransactionClassificationService>.Instance);

        // Act
        var result = await service.AcceptClassificationAsync(transaction.Id);

        // Assert
        result.Category.Should().Be("Groceries");
        result.CategorisedBy.Should().Be("rule");
        result.ClassificationMethod.Should().Be("rule_engine");
        result.ReviewStatus.Should().Be("Reviewed");
    }

    [Fact]
    public async Task CreateRuleAsync_ShouldThrowArgumentException_WhenRegexPatternIsInvalid()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        var service = new TransactionClassificationService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId),
            new NoOpGraphCacheInvalidator(),
            new NoOpAiClassifier(),
            NullLogger<TransactionClassificationService>.Instance);

        // Act
        Func<Task> action = () => service.CreateRuleAsync(
            new CreateCategorisationRuleRequest(
                "(",
                "Groceries",
                null,
                100,
                "regex",
                false,
                null,
                null,
                null,
                "User"));

        // Assert
        var exception = await action.Should().ThrowAsync<ArgumentException>();
        exception.Which.ParamName.Should().Be(nameof(CreateCategorisationRuleRequest.Pattern));
    }

    [Fact]
    public async Task AcceptClassificationAsync_ShouldNotThrow_WhenStoredRegexPatternIsInvalid()
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
            Amount = -15m,
            Currency = "USD",
            Merchant = "Corner Store",
            Description = "POS TXN",
            ReviewStatus = "Pending",
            TagsJson = "[]"
        };

        var malformedRule = new CategorisationRule
        {
            TenantId = tenantId,
            UserId = userId,
            Pattern = "(",
            Category = "Groceries",
            Priority = 100,
            IsActive = true,
            MatchType = "regex",
            CaseSensitive = false,
            Scope = "User",
            ApprovalStatus = "Approved"
        };

        context.PersonalTransactions.Add(transaction);
        context.CategorisationRules.Add(malformedRule);
        await context.SaveChangesAsync();

        var service = new TransactionClassificationService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId),
            new NoOpGraphCacheInvalidator(),
            new NoOpAiClassifier(),
            NullLogger<TransactionClassificationService>.Instance);

        // Act
        var result = await service.AcceptClassificationAsync(transaction.Id);

        // Assert
        result.Category.Should().Be("uncategorized");
        result.CategorisedBy.Should().Be("manual");
        result.ClassificationMethod.Should().Be("manual_fallback");
        result.ReviewStatus.Should().Be("Reviewed");
    }

    [Fact]
    public async Task AcceptClassificationAsync_ShouldPropagateSubCategory_WhenRuleHasSubCategory()
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
            Amount = -9.99m,
            Currency = "USD",
            Merchant = "Netflix",
            Description = "Monthly subscription",
            ReviewStatus = "Pending",
            TagsJson = "[]"
        };

        var rule = new CategorisationRule
        {
            TenantId = tenantId,
            UserId = userId,
            Pattern = "Netflix",
            Category = "subscriptions",
            SubCategory = "streaming",
            Priority = 200,
            IsActive = true,
            MatchType = "contains",
            CaseSensitive = false,
            Scope = "User",
            ApprovalStatus = "Approved"
        };

        context.PersonalTransactions.Add(transaction);
        context.CategorisationRules.Add(rule);
        await context.SaveChangesAsync();

        var service = new TransactionClassificationService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId),
            new NoOpGraphCacheInvalidator(),
            new NoOpAiClassifier(),
            NullLogger<TransactionClassificationService>.Instance);

        // Act
        var result = await service.AcceptClassificationAsync(transaction.Id);

        // Assert
        result.Category.Should().Be("subscriptions");
        result.SubCategory.Should().Be("streaming");
        result.CategorisedBy.Should().Be("rule");
        result.ClassificationMethod.Should().Be("rule_engine");
        result.ReviewStatus.Should().Be("Reviewed");
    }

    [Fact]
    public async Task AcceptClassificationAsync_ShouldMatchSystemRule_WhenNoUserRuleExists()
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
            Amount = -35.00m,
            Currency = "GBP",
            Merchant = "Tesco",
            Description = "Tesco Superstore",
            ReviewStatus = "Pending",
            TagsJson = "[]"
        };

        // System rule: TenantId = Guid.Empty, UserId = Guid.Empty, Scope = "System"
        var systemRule = new CategorisationRule
        {
            TenantId = Guid.Empty,
            UserId = Guid.Empty,
            Pattern = "Tesco",
            Category = "groceries",
            SubCategory = "supermarket",
            Priority = 100,
            IsActive = true,
            MatchType = "contains",
            CaseSensitive = false,
            Scope = "System",
            ApprovalStatus = "Approved"
        };

        context.PersonalTransactions.Add(transaction);
        context.CategorisationRules.Add(systemRule);
        await context.SaveChangesAsync();

        var service = new TransactionClassificationService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId),
            new NoOpGraphCacheInvalidator(),
            new NoOpAiClassifier(),
            NullLogger<TransactionClassificationService>.Instance);

        // Act
        var result = await service.AcceptClassificationAsync(transaction.Id);

        // Assert
        result.Category.Should().Be("groceries");
        result.SubCategory.Should().Be("supermarket");
        result.CategorisedBy.Should().Be("rule");
        result.ClassificationMethod.Should().Be("system_rule");
        result.Confidence.Should().Be(0.8m);
        result.ReviewStatus.Should().Be("Reviewed");
    }
}
