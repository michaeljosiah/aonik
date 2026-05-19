using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.PersonalFinance.Persistence;
using Aonik.Finance.Services.PersonalFinance;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.PersonalFinance;

public class PersonalTransactionServiceTests
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

        public Task InvalidateUserGraphAsync(Guid userId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task InvalidateUserGraphsAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task InvalidateAllGraphCachesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static PersonalFinanceDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<PersonalFinanceDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;

        return new PersonalFinanceDbContext(options, new TestTenantProvider(tenantId));
    }

    [Fact]
    public async Task CreateManualTransactionAsync_Should_CreatePendingTransaction_WhenCategoryNotProvided()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        context.PersonalAccounts.Add(new PersonalAccount
        {
            TenantId = tenantId,
            UserId = userId,
            Name = "Main Account",
            AccountType = "Bank",
            Currency = "USD",
            Status = "Active"
        });
        await context.SaveChangesAsync();

        var accountId = await context.PersonalAccounts.Select(x => x.Id).FirstAsync();

        var service = new PersonalTransactionService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId),
            new NoOpGraphCacheInvalidator());

        var request = new CreateManualPersonalTransactionRequest(
            accountId,
            DateTime.UtcNow,
            -25.50m,
            "usd",
            "Coffee Shop",
            "Morning coffee",
            null,
            null,
            new[] { "Food", "Coffee" });

        // Act
        var result = await service.CreateManualTransactionAsync(request);

        // Assert
        result.Category.Should().BeNull();
        result.Confidence.Should().Be(0);
        result.CategorisedBy.Should().BeNull();
        result.ClassificationMethod.Should().BeNull();
        result.Currency.Should().Be("USD");
        result.Tags.Should().BeEquivalentTo(new[] { "Food", "Coffee" });
    }

    [Fact]
    public async Task UpdateManualTransactionAsync_Should_SetManualClassification_WhenCategoryProvided()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = new PersonalTransactionService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId),
            new NoOpGraphCacheInvalidator());

        var created = await service.CreateManualTransactionAsync(new CreateManualPersonalTransactionRequest(
            null,
            DateTime.UtcNow,
            -70m,
            "USD",
            "Fuel Station",
            "Gas refill",
            null,
            null,
            null));

        // Act
        var updated = await service.UpdateManualTransactionAsync(
            created.PersonalTransactionId,
            new UpdateManualPersonalTransactionRequest(
                null,
                created.OccurredAt,
                created.Amount,
                "USD",
                created.Merchant,
                created.Description,
                "Transport",
                created.Notes,
                new[] { "Car" }));

        // Assert
        updated.Category.Should().Be("Transport");
        updated.Confidence.Should().Be(1.0m);
        updated.CategorisedBy.Should().Be("manual");
        updated.ClassificationMethod.Should().Be("manual");
        updated.Tags.Should().BeEquivalentTo(new[] { "Car" });
    }

    [Fact]
    public async Task UpdateManualTransactionAsync_Should_SupportCategoryOnlyPatch()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = new PersonalTransactionService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId),
            new NoOpGraphCacheInvalidator());

        var created = await service.CreateManualTransactionAsync(new CreateManualPersonalTransactionRequest(
            null,
            DateTime.UtcNow,
            -70m,
            "USD",
            "Fuel Station",
            "Gas refill",
            "Transport",
            null,
            null));

        var trackedTransaction = await context.PersonalTransactions.FirstAsync(item => item.Id == created.PersonalTransactionId);
        trackedTransaction.SubCategory = "ride_hailing";
        await context.SaveChangesAsync();

        // Act
        var updated = await service.UpdateManualTransactionAsync(
            created.PersonalTransactionId,
            new UpdateManualPersonalTransactionRequest(
                null,
                default,
                0,
                string.Empty,
                null,
                null,
                "Groceries",
                null,
                null));

        // Assert
        updated.Category.Should().Be("Groceries");
        updated.SubCategory.Should().BeNull();
        updated.Amount.Should().Be(created.Amount);
        updated.Currency.Should().Be(created.Currency);
        updated.Merchant.Should().Be(created.Merchant);
        updated.Description.Should().Be(created.Description);
        updated.Confidence.Should().Be(1.0m);
        updated.CategorisedBy.Should().Be("manual");
        updated.ClassificationMethod.Should().Be("manual");
    }

    [Fact]
    public async Task CreateManualTransactionAsync_Should_UpdateManualAccountBalance_WhenAccountProvided()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var account = new PersonalAccount
        {
            TenantId = tenantId,
            UserId = userId,
            Name = "Main Account",
            AccountType = "Bank",
            Currency = "GBP",
            Status = "Active",
            CurrentBalance = 50m,
            BalanceAsOf = DateTime.UtcNow
        };
        context.PersonalAccounts.Add(account);
        await context.SaveChangesAsync();

        var service = new PersonalTransactionService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId),
            new NoOpGraphCacheInvalidator());

        // Act
        await service.CreateManualTransactionAsync(new CreateManualPersonalTransactionRequest(
            account.Id,
            DateTime.UtcNow,
            -10m,
            "GBP",
            "Mcdonalds",
            "Mcdonalds",
            "eating_out",
            null,
            null));

        // Assert
        account.CurrentBalance.Should().Be(40m);
        account.BalanceAsOf.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateManualTransactionAsync_Should_MoveBalanceBetweenManualAccounts_WhenAccountChanges()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        var checking = new PersonalAccount
        {
            TenantId = tenantId,
            UserId = userId,
            Name = "Checking",
            AccountType = "Bank",
            Currency = "GBP",
            Status = "Active",
            CurrentBalance = 100m,
            BalanceAsOf = DateTime.UtcNow
        };

        var savings = new PersonalAccount
        {
            TenantId = tenantId,
            UserId = userId,
            Name = "Savings",
            AccountType = "Savings",
            Currency = "GBP",
            Status = "Active",
            CurrentBalance = 10m,
            BalanceAsOf = DateTime.UtcNow
        };

        context.PersonalAccounts.AddRange(checking, savings);
        await context.SaveChangesAsync();

        var service = new PersonalTransactionService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId),
            new NoOpGraphCacheInvalidator());

        var created = await service.CreateManualTransactionAsync(new CreateManualPersonalTransactionRequest(
            checking.Id,
            DateTime.UtcNow,
            -70m,
            "GBP",
            "Transfer",
            "Transfer",
            "transfer_out",
            null,
            null));

        // Act
        await service.UpdateManualTransactionAsync(
            created.PersonalTransactionId,
            new UpdateManualPersonalTransactionRequest(
                savings.Id,
                created.OccurredAt,
                -50m,
                "GBP",
                created.Merchant,
                created.Description,
                created.Category,
                created.Notes,
                created.Tags));

        // Assert
        checking.CurrentBalance.Should().Be(100m);
        savings.CurrentBalance.Should().Be(-40m);
    }

    [Fact]
    public async Task DeleteManualTransactionAsync_Should_SoftDeleteAndReverseBalance()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var account = new PersonalAccount
        {
            TenantId = tenantId,
            UserId = userId,
            Name = "Main Account",
            AccountType = "Bank",
            Currency = "GBP",
            Status = "Active",
            CurrentBalance = 100m,
            BalanceAsOf = DateTime.UtcNow
        };
        context.PersonalAccounts.Add(account);
        await context.SaveChangesAsync();

        var service = new PersonalTransactionService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId),
            new NoOpGraphCacheInvalidator());

        var created = await service.CreateManualTransactionAsync(new CreateManualPersonalTransactionRequest(
            account.Id,
            DateTime.UtcNow,
            -17.50m,
            "GBP",
            "Nando's",
            "Dinner",
            "eating_out",
            null,
            null));

        account.CurrentBalance.Should().Be(82.50m);

        // Act
        await service.DeleteManualTransactionAsync(created.PersonalTransactionId);

        // Assert — balance should be restored
        account.CurrentBalance.Should().Be(100m);

        // Assert — transaction should not be visible via query (soft-deleted)
        var fetched = await service.GetTransactionAsync(created.PersonalTransactionId);
        fetched.Should().BeNull();
    }

    [Fact]
    public async Task DeleteManualTransactionAsync_Should_RejectNonManualTransaction()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        // Seed an imported transaction directly
        var transaction = new PersonalTransaction
        {
            TenantId = tenantId,
            UserId = userId,
            SourceType = "plaid",
            SourceId = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow,
            Amount = -50m,
            Currency = "GBP",
            TransactionType = "Expense",
        };
        context.PersonalTransactions.Add(transaction);
        await context.SaveChangesAsync();

        var service = new PersonalTransactionService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId),
            new NoOpGraphCacheInvalidator());

        // Act & Assert
        var act = () => service.DeleteManualTransactionAsync(transaction.Id);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*manually created*");
    }

    [Fact]
    public async Task DeleteManualTransactionAsync_Should_ThrowWhenTransactionNotFound()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);

        var service = new PersonalTransactionService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId),
            new NoOpGraphCacheInvalidator());

        // Act & Assert
        var act = () => service.DeleteManualTransactionAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
