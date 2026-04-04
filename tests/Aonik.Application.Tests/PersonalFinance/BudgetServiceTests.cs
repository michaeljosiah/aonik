using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.PersonalFinance;
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

    private static FinanceDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;

        return new FinanceDbContext(options, new TestTenantProvider(tenantId));
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
}
