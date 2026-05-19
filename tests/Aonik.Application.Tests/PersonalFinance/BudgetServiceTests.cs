using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Services.PersonalFinance;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.PersonalFinance;

public class BudgetServiceTests
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

    private static PersonalFinanceDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<PersonalFinanceDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;

        return new PersonalFinanceDbContext(options, new TestTenantProvider(tenantId));
    }

    [Fact]
    public async Task CreateBudgetAsync_Should_CreateBudgetLine_When_CategorySelected()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = new BudgetService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId));

        var request = new CreateBudgetRequest("housing");

        // Act
        var result = await service.CreateBudgetAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Housing");
        result.Id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreateBudgetAsync_Should_CreateBudgetLine_When_BrandNewUser()
    {
        // Arrange — simulates the exact flow: LIST (creates budget), then CREATE (adds line)
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = new BudgetService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId));

        // First call: ListBudgetsAsync (happens when the screen loads, creates the Budget)
        var list = await service.ListBudgetsAsync();
        list.Should().BeEmpty();

        // Second call: CreateBudgetAsync (happens when user picks a category)
        var request = new CreateBudgetRequest("groceries");
        var result = await service.CreateBudgetAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Food & Groceries");
    }

    [Fact]
    public async Task ListBudgetsAsync_Should_ReflectTransactionSpending_When_CategoryLinked()
    {
        // Arrange — create a budget for "eating-out", add a transaction with category "eating_out"
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = new BudgetService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId));

        // Create budget line for eating-out
        var budget = await service.CreateBudgetAsync(new CreateBudgetRequest("eating-out"));
        budget.Name.Should().Be("Eating Out");

        // Simulate a manual transaction with the transaction taxonomy category "eating_out"
        var now = DateTime.UtcNow;
        context.Set<PersonalTransaction>().Add(new PersonalTransaction
        {
            TenantId = tenantId,
            UserId = userId,
            OccurredAt = now,
            Amount = -17.50m,
            Currency = "GBP",
            Category = "eating_out",  // Transaction taxonomy uses snake_case
            Merchant = "Nando's",
            SourceType = "Manual",
            SourceId = Guid.NewGuid(),
            TransactionType = "Expense",
        });
        await context.SaveChangesAsync();

        // Act — list budgets (recalculates spent from transactions)
        var budgets = await service.ListBudgetsAsync();

        // Assert — the eating-out budget should show £17.50 spent
        var eatingOut = budgets.Should().ContainSingle().Subject;
        eatingOut.Name.Should().Be("Eating Out");
        eatingOut.LineItems.Should().ContainSingle()
            .Which.Spent.Should().Be(17.50m);
    }
}
