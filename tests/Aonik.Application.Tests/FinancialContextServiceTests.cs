using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Entities;
using Aonik.Finance.Persistence;
using Aonik.PersonalFinance.Services;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests;

public class FinancialContextServiceTests
{
    private class TestTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;

        public TestTenantProvider(Guid tenantId) => _tenantId = tenantId;

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

        public TestCurrentUserProvider(Guid userId) => _userId = userId;

        public Guid? GetCurrentUserId() => _userId;

        public bool TryGetCurrentUserId(out Guid userId)
        {
            userId = _userId;
            return true;
        }
    }

    private sealed class NoOpCacheInvalidator : IFinancialLifeGraphCacheInvalidator
    {
        public void InvalidateCurrentUserGraph() { }
        public Task InvalidateCurrentUserGraphAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task InvalidateUserGraphAsync(Guid userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task InvalidateUserGraphsAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task InvalidateAllGraphCachesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private (FinancialContextService service, PersonalFinanceDbContext context) CreateService(
        Guid? tenantId = null, Guid? userId = null)
    {
        var tid = tenantId ?? Guid.NewGuid();
        var uid = userId ?? Guid.NewGuid();

        var options = new DbContextOptionsBuilder<PersonalFinanceDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        var context = new PersonalFinanceDbContext(options, new TestTenantProvider(tid));
        var service = new FinancialContextService(
            context,
            new TestTenantProvider(tid),
            new TestCurrentUserProvider(uid),
            new NoOpCacheInvalidator());

        return (service, context);
    }

    [Fact]
    public async Task CreateContextAsync_ShouldCreateContext()
    {
        // Arrange
        var (service, _) = CreateService();
        var request = new CreateFinancialContextRequest(
            "Mum's Property",
            "Property",
            RelatedPartyId: null,
            Notes: "Rental property management",
            MetadataJson: null);

        // Act
        var result = await service.CreateContextAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Mum's Property");
        result.ContextType.Should().Be("Property");
        result.Status.Should().Be("Active");
        result.Notes.Should().Be("Rental property management");
        result.MetadataJson.Should().Be("{}");
        result.FundingSources.Should().BeEmpty();
    }

    [Fact]
    public async Task ListContextsAsync_ShouldReturnOnlyActiveContexts()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var (service, context) = CreateService(tenantId, userId);

        context.FinancialContexts.AddRange(
            new FinancialContext
            {
                TenantId = tenantId, UserId = userId, Name = "Active Space",
                ContextType = "Property", Status = "Active"
            },
            new FinancialContext
            {
                TenantId = tenantId, UserId = userId, Name = "Archived Space",
                ContextType = "SideBusiness", Status = "Archived"
            });
        await context.SaveChangesAsync();

        // Act
        var result = await service.ListContextsAsync(includeArchived: false);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Active Space");
    }

    [Fact]
    public async Task ListContextsAsync_WithIncludeArchived_ShouldReturnAll()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var (service, context) = CreateService(tenantId, userId);

        context.FinancialContexts.AddRange(
            new FinancialContext
            {
                TenantId = tenantId, UserId = userId, Name = "Active Space",
                ContextType = "Property", Status = "Active"
            },
            new FinancialContext
            {
                TenantId = tenantId, UserId = userId, Name = "Archived Space",
                ContextType = "SideBusiness", Status = "Archived"
            });
        await context.SaveChangesAsync();

        // Act
        var result = await service.ListContextsAsync(includeArchived: true);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task ArchiveContextAsync_ShouldSetStatusToArchived()
    {
        // Arrange
        var (service, _) = CreateService();
        var created = await service.CreateContextAsync(
            new CreateFinancialContextRequest("Test", "Property", null, null, null));

        // Act
        await service.ArchiveContextAsync(created.FinancialContextId);

        // Assert
        var result = await service.GetContextAsync(created.FinancialContextId);
        result.Should().NotBeNull();
        result!.Status.Should().Be("Archived");
    }

    [Fact]
    public async Task AddFundingSourceAsync_ShouldLinkAccountToContext()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var (service, context) = CreateService(tenantId, userId);

        var account = new PersonalAccount
        {
            TenantId = tenantId,
            UserId = userId,
            Name = "Savings Account",
            AccountType = "Savings",
            Currency = "GBP",
            Status = "Active"
        };
        context.PersonalAccounts.Add(account);
        await context.SaveChangesAsync();

        var created = await service.CreateContextAsync(
            new CreateFinancialContextRequest("Mum's Property", "Property", null, null, null));

        // Act
        var result = await service.AddFundingSourceAsync(
            created.FinancialContextId,
            new AddFundingSourceRequest(account.Id, IsPrimary: true));

        // Assert
        result.Should().NotBeNull();
        result.PersonalAccountId.Should().Be(account.Id);
        result.IsPrimary.Should().BeTrue();

        // Verify it appears in the context
        var refreshed = await service.GetContextAsync(created.FinancialContextId);
        refreshed!.FundingSources.Should().HaveCount(1);
    }

    [Fact]
    public async Task AssignTransactionContextAsync_ShouldUpdateTransaction()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var (service, context) = CreateService(tenantId, userId);

        var financialContext = new FinancialContext
        {
            TenantId = tenantId,
            UserId = userId,
            Name = "Side Hustle",
            ContextType = "SideBusiness",
            Status = "Active"
        };
        context.FinancialContexts.Add(financialContext);

        var transaction = new PersonalTransaction
        {
            TenantId = tenantId,
            UserId = userId,
            SourceType = "manual",
            SourceId = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow,
            Amount = -50.00m,
            Currency = "GBP",
            TransactionType = "expense",
            TagsJson = "[]"
        };
        context.PersonalTransactions.Add(transaction);
        await context.SaveChangesAsync();

        // Act
        await service.AssignTransactionContextAsync(
            transaction.Id,
            new AssignTransactionContextRequest(financialContext.Id));

        // Assert
        var updated = await context.PersonalTransactions
            .FirstOrDefaultAsync(t => t.Id == transaction.Id);
        updated!.FinancialContextId.Should().Be(financialContext.Id);
    }

    [Fact]
    public async Task AssignTransactionContextAsync_WithNull_ShouldUnassign()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var (service, context) = CreateService(tenantId, userId);

        var financialContext = new FinancialContext
        {
            TenantId = tenantId,
            UserId = userId,
            Name = "Side Hustle",
            ContextType = "SideBusiness",
            Status = "Active"
        };
        context.FinancialContexts.Add(financialContext);

        var transaction = new PersonalTransaction
        {
            TenantId = tenantId,
            UserId = userId,
            SourceType = "manual",
            SourceId = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow,
            Amount = -50.00m,
            Currency = "GBP",
            TransactionType = "expense",
            TagsJson = "[]",
            FinancialContextId = financialContext.Id
        };
        context.PersonalTransactions.Add(transaction);
        await context.SaveChangesAsync();

        // Act
        await service.AssignTransactionContextAsync(
            transaction.Id,
            new AssignTransactionContextRequest(null));

        // Assert
        var updated = await context.PersonalTransactions
            .FirstOrDefaultAsync(t => t.Id == transaction.Id);
        updated!.FinancialContextId.Should().BeNull();
    }

    [Fact]
    public async Task GetContextSummaryAsync_ShouldAggregateTransactions()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var (service, context) = CreateService(tenantId, userId);

        var financialContext = new FinancialContext
        {
            TenantId = tenantId,
            UserId = userId,
            Name = "Mum's Property",
            ContextType = "Property",
            Status = "Active"
        };
        context.FinancialContexts.Add(financialContext);
        await context.SaveChangesAsync();

        var now = DateTime.UtcNow;
        context.PersonalTransactions.AddRange(
            new PersonalTransaction
            {
                TenantId = tenantId, UserId = userId,
                SourceType = "manual", SourceId = Guid.NewGuid(),
                OccurredAt = now.AddDays(-5),
                Amount = 1200.00m, Currency = "GBP",
                TransactionType = "income", TagsJson = "[]",
                FinancialContextId = financialContext.Id
            },
            new PersonalTransaction
            {
                TenantId = tenantId, UserId = userId,
                SourceType = "manual", SourceId = Guid.NewGuid(),
                OccurredAt = now.AddDays(-3),
                Amount = -300.00m, Currency = "GBP",
                TransactionType = "expense", TagsJson = "[]",
                FinancialContextId = financialContext.Id
            },
            new PersonalTransaction
            {
                TenantId = tenantId, UserId = userId,
                SourceType = "manual", SourceId = Guid.NewGuid(),
                OccurredAt = now.AddDays(-1),
                Amount = -150.00m, Currency = "GBP",
                TransactionType = "expense", TagsJson = "[]",
                FinancialContextId = financialContext.Id
            });
        await context.SaveChangesAsync();

        // Act
        var result = await service.GetContextSummaryAsync(
            financialContext.Id,
            from: now.AddDays(-10),
            to: now);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Mum's Property");
        result.TransactionCount.Should().Be(3);
        result.TotalInflow.Should().Be(1200.00m);
        result.TotalOutflow.Should().Be(450.00m);
        result.NetAmount.Should().Be(750.00m);
    }
}
