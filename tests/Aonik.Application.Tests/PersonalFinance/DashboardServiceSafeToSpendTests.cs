using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Services.PersonalFinance;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ordering;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.PersonalFinance;

public class DashboardServiceSafeToSpendTests
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

    // Safe-to-spend does not call the order or party readers; the stubs throw
    // so a regression that adds a call surfaces loudly.
    private sealed class ThrowingOrderHistoryReader : ICustomerOrderHistoryReader
    {
        public Task<IReadOnlyList<OrderHistoryItem>> GetForPartyAsync(Guid t, Guid p, DateTime f, DateTime to, CancellationToken c = default) => throw new InvalidOperationException("not used");
        public Task<IReadOnlyList<OrderHistoryItem>> GetByIdsAsync(Guid t, IReadOnlyCollection<Guid> ids, CancellationToken c = default) => throw new InvalidOperationException("not used");
        public Task<IReadOnlyList<OrderWithPartyRolesItem>> GetRecentForPayerAsync(Guid t, Guid p, int take, CancellationToken c = default) => throw new InvalidOperationException("not used");
        public Task<bool> ExistsAsync(Guid t, Guid o, CancellationToken c = default) => throw new InvalidOperationException("not used");
    }

    private sealed class ThrowingPartyReader : IPartyReader
    {
        public Task<IReadOnlyList<PartyHistoryItem>> GetByIdsAsync(Guid t, IReadOnlyCollection<Guid> ids, CancellationToken c = default) => throw new InvalidOperationException("not used");
        public Task<IReadOnlyList<PartyRelationshipHistoryItem>> GetRelationshipsForPartyAsync(Guid t, Guid p, CancellationToken c = default) => throw new InvalidOperationException("not used");
        public Task<bool> ExistsAsync(Guid t, Guid p, CancellationToken c = default) => throw new InvalidOperationException("not used");
        public Task<bool> HasActiveRelationshipBetweenAsync(Guid t, Guid a, Guid b, CancellationToken c = default) => throw new InvalidOperationException("not used");
    }

    private static PersonalFinanceDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<PersonalFinanceDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;
        return new PersonalFinanceDbContext(options, new TestTenantProvider(tenantId));
    }

    private static DashboardService CreateService(PersonalFinanceDbContext db, Guid tenantId, Guid userId) =>
        new(db,
            new ThrowingOrderHistoryReader(),
            new ThrowingPartyReader(),
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId));

    [Fact]
    public async Task GetSafeToSpendBreakdownAsync_ShouldSubtractObligationsAndListFactors_When_ObligationsExist()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var db = CreateDbContext(tenantId);

        db.PersonalAccounts.Add(new PersonalAccount
        {
            TenantId = tenantId, UserId = userId,
            Name = "Main", AccountType = "Checking",
            CurrentBalance = 1000m, Currency = "GBP"
        });

        var billDue = DateTime.UtcNow.Date.AddDays(3);
        var debtDue = DateTime.UtcNow.Date.AddDays(10);

        db.Bills.Add(new Bill
        {
            TenantId = tenantId, UserId = userId,
            Payee = "Council", Frequency = "Monthly",
            NextDueDate = billDue, ExpectedAmount = 180m, Currency = "GBP",
            Status = "Active"
        });
        db.DebtRepayments.Add(new DebtRepayment
        {
            TenantId = tenantId, UserId = userId,
            CreditorName = "Klarna", DebtType = "BNPL",
            NextDueDate = debtDue, ExpectedAmount = 50m, Currency = "GBP",
            Status = "Active", VerificationStatus = "Confirmed"
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId, userId);

        // Act
        var result = await service.GetSafeToSpendBreakdownAsync();

        // Assert
        result.LiquidAssets.Should().Be(1000m);
        result.ProtectedObligations.Should().Be(230m);
        result.AvailableToSpend.Should().Be(770m);
        result.Currency.Should().Be("GBP");
        result.Factors.Should().HaveCount(2);

        // Factors are ordered by due date ascending.
        result.Factors[0].Kind.Should().Be("Bill");
        result.Factors[0].Label.Should().Be("Council");
        result.Factors[0].Amount.Should().Be(180m);
        result.Factors[0].DueDate.Should().Be(billDue);

        result.Factors[1].Kind.Should().Be("DebtRepayment");
        result.Factors[1].Label.Should().Be("Klarna");
        result.Factors[1].Amount.Should().Be(50m);
    }

    [Fact]
    public async Task GetSafeToSpendBreakdownAsync_ShouldExcludeLiabilityAndLongTermInvestmentAccounts()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var db = CreateDbContext(tenantId);

        db.PersonalAccounts.AddRange(
            new PersonalAccount { TenantId = tenantId, UserId = userId, Name = "Cash", AccountType = "Checking", CurrentBalance = 500m, Currency = "GBP" },
            new PersonalAccount { TenantId = tenantId, UserId = userId, Name = "Savings", AccountType = "Savings", CurrentBalance = 300m, Currency = "GBP" },
            new PersonalAccount { TenantId = tenantId, UserId = userId, Name = "ISA", AccountType = "Investment", CurrentBalance = 10_000m, Currency = "GBP" },
            new PersonalAccount { TenantId = tenantId, UserId = userId, Name = "Credit", AccountType = "CreditCard", CurrentBalance = 200m, Currency = "GBP" });
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId, userId);

        // Act
        var result = await service.GetSafeToSpendBreakdownAsync();

        // Assert
        // Liquid = Checking 500 + Savings 300; Investment and CreditCard excluded.
        result.LiquidAssets.Should().Be(800m);
        result.ProtectedObligations.Should().Be(0m);
        result.AvailableToSpend.Should().Be(800m);
        result.Factors.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSafeToSpendBreakdownAsync_ShouldFloorAtZero_When_ObligationsExceedLiquidAssets()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var db = CreateDbContext(tenantId);

        db.PersonalAccounts.Add(new PersonalAccount
        {
            TenantId = tenantId, UserId = userId,
            Name = "Main", AccountType = "Checking",
            CurrentBalance = 100m, Currency = "GBP"
        });
        db.Bills.Add(new Bill
        {
            TenantId = tenantId, UserId = userId,
            Payee = "Rent", Frequency = "Monthly",
            NextDueDate = DateTime.UtcNow.Date.AddDays(5),
            ExpectedAmount = 500m, Currency = "GBP", Status = "Active"
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId, userId);

        // Act
        var result = await service.GetSafeToSpendBreakdownAsync();

        // Assert
        result.LiquidAssets.Should().Be(100m);
        result.ProtectedObligations.Should().Be(500m);
        result.AvailableToSpend.Should().Be(0m);
    }

    [Fact]
    public async Task GetSafeToSpendBreakdownAsync_ShouldIgnoreObligationsWithoutExpectedAmount()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var db = CreateDbContext(tenantId);

        db.PersonalAccounts.Add(new PersonalAccount
        {
            TenantId = tenantId, UserId = userId,
            Name = "Main", AccountType = "Checking",
            CurrentBalance = 1000m, Currency = "GBP"
        });
        db.Bills.Add(new Bill
        {
            TenantId = tenantId, UserId = userId,
            Payee = "Unknown amount", Frequency = "Monthly",
            NextDueDate = DateTime.UtcNow.Date.AddDays(7),
            ExpectedAmount = null, Currency = "GBP", Status = "Active"
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId, userId);

        // Act
        var result = await service.GetSafeToSpendBreakdownAsync();

        // Assert
        result.ProtectedObligations.Should().Be(0m);
        result.AvailableToSpend.Should().Be(1000m);
        result.Factors.Should().BeEmpty();
    }
}
