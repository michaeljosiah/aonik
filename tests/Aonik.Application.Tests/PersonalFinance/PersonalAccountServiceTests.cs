using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.PersonalFinance;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.PersonalFinance;

public class PersonalAccountServiceTests
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
    public async Task CreateAccountAsync_Should_CreateActiveAccount_WhenRequestIsValid()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = new PersonalAccountService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId));

        var request = new CreatePersonalAccountRequest(
            "Main Bank",
            "Bank",
            "usd",
            "Acme Bank",
            "REF-1",
            "Current",
            "1234");

        // Act
        var result = await service.CreateAccountAsync(request);

        // Assert
        result.Name.Should().Be("Main Bank");
        result.Currency.Should().Be("USD");
        result.IsArchived.Should().BeFalse();
        result.Status.Should().Be("Active");
        result.Last4.Should().Be("1234");
    }

    [Fact]
    public async Task ArchiveAccountAsync_Should_ExcludeAccountFromDefaultList()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = new PersonalAccountService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId));

        var created = await service.CreateAccountAsync(new CreatePersonalAccountRequest(
            "Card",
            "CreditCard",
            "USD",
            null,
            null,
            "Visa",
            "0001"));

        // Act
        await service.ArchiveAccountAsync(created.PersonalAccountId);
        var activeOnly = await service.ListAccountsAsync();
        var all = await service.ListAccountsAsync(includeArchived: true);

        // Assert
        activeOnly.Should().BeEmpty();
        all.Should().HaveCount(1);
        all[0].IsArchived.Should().BeTrue();
        all[0].Status.Should().Be("Archived");
    }
}
