using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions;
using Aonik.Finance.Contracts.Services.Pricing;
using Aonik.Finance.Entities.Pricing;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Pricing;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Pricing;

public class PricingPolicyServiceTests
{
    private sealed class TestTenantProvider : ITenantProvider
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

    private static FinanceDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;

        return new FinanceDbContext(options, new TestTenantProvider(tenantId));
    }

    [Fact]
    public async Task ResolveLimitsPolicyAsync_ShouldReturnTenantPolicy_WhenCustomerIdMissing()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = new PricingPolicyService(context, new TestTenantProvider(tenantId));
        var tenantPolicyId = Guid.NewGuid();

        context.LimitsPolicies.Add(new LimitsPolicy
        {
            Id = tenantPolicyId,
            TenantId = tenantId,
            ScopeType = "Tenant",
            ScopeId = tenantId,
            Currency = "USD",
            MaxAmount = 1000m,
            Period = "Monthly",
            IsActive = true
        });
        await context.SaveChangesAsync();

        // Act
        var result = await service.ResolveLimitsPolicyAsync(null, "USD");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(tenantPolicyId);
    }

    [Fact]
    public async Task ResolveLimitsPolicyAsync_ShouldReturnCustomerPolicy_WhenCustomerPolicyExists()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = new PricingPolicyService(context, new TestTenantProvider(tenantId));
        var customerPolicyId = Guid.NewGuid();

        context.LimitsPolicies.AddRange(
            new LimitsPolicy
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ScopeType = "Tenant",
                ScopeId = tenantId,
                Currency = "USD",
                MaxAmount = 1000m,
                Period = "Monthly",
                IsActive = true
            },
            new LimitsPolicy
            {
                Id = customerPolicyId,
                TenantId = tenantId,
                ScopeType = "Customer",
                ScopeId = customerId,
                Currency = "USD",
                MaxAmount = 200m,
                Period = "Monthly",
                IsActive = true
            });
        await context.SaveChangesAsync();

        // Act
        var result = await service.ResolveLimitsPolicyAsync(customerId, "USD");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(customerPolicyId);
    }

    [Fact]
    public async Task ResolveLimitsPolicyAsync_ShouldFallbackToTenantPolicy_WhenCustomerPolicyMissing()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = new PricingPolicyService(context, new TestTenantProvider(tenantId));
        var tenantPolicyId = Guid.NewGuid();

        context.LimitsPolicies.Add(new LimitsPolicy
        {
            Id = tenantPolicyId,
            TenantId = tenantId,
            ScopeType = "Tenant",
            ScopeId = tenantId,
            Currency = "USD",
            MaxAmount = 1000m,
            Period = "Monthly",
            IsActive = true
        });
        await context.SaveChangesAsync();

        // Act
        var result = await service.ResolveLimitsPolicyAsync(customerId, "USD");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(tenantPolicyId);
    }
}
