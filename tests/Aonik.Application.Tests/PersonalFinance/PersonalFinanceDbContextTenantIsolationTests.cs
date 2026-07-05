using Aonik.PersonalFinance.Entities;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.PersonalFinance;

/// <summary>
/// Spec 027 S3 (#126): after the PersonalFinance entities were de-duplicated off
/// <c>FinanceDbContext</c>, <see cref="PersonalFinanceDbContext"/> is their sole
/// runtime owner. This proves the tenant query filter it applies
/// (<c>ApplyTenantQueryFilters</c>) still scopes a PF entity correctly: a row
/// written under tenant A is invisible to a query issued under tenant B, and
/// visible under tenant A.
/// </summary>
public class PersonalFinanceDbContextTenantIsolationTests
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

    // Both context instances bind to the SAME in-memory database name so they
    // read the same physical rows; only their resolved tenant differs. The
    // tenant query filter is baked into the model per-context from the
    // provider's CurrentTenantId, so "query as tenant B" means a fresh context
    // whose provider returns tenant B.
    private static PersonalFinanceDbContext CreateDbContext(string databaseName, Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<PersonalFinanceDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new PersonalFinanceDbContext(options, new TestTenantProvider(tenantId));
    }

    [Fact]
    public async Task Bill_Should_BeVisibleOnlyToOwningTenant_When_QueriedThroughPersonalFinanceDbContext()
    {
        // Arrange
        var databaseName = $"TestDb_{Guid.NewGuid()}";
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var billId = Guid.NewGuid();

        await using (var tenantAContext = CreateDbContext(databaseName, tenantA))
        {
            tenantAContext.Bills.Add(new Bill
            {
                Id = billId,
                TenantId = tenantA,
                UserId = userId,
                Payee = "Octopus Energy",
                Frequency = "Monthly",
                NextDueDate = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc),
                ExpectedAmount = 85m,
                Currency = "GBP",
                Status = "Active"
            });

            await tenantAContext.SaveChangesAsync();
        }

        // Act + Assert — tenant B must not see tenant A's row.
        await using (var tenantBContext = CreateDbContext(databaseName, tenantB))
        {
            var visibleToTenantB = await tenantBContext.Bills
                .AsNoTracking()
                .ToListAsync();

            visibleToTenantB.Should().BeEmpty(
                because: "the tenant query filter must exclude another tenant's Bill");

            var byIdForTenantB = await tenantBContext.Bills
                .AsNoTracking()
                .FirstOrDefaultAsync(bill => bill.Id == billId);

            byIdForTenantB.Should().BeNull(
                because: "even a direct id lookup must not leak across tenants");
        }

        // Act + Assert — tenant A still sees its own row.
        await using (var tenantAContext = CreateDbContext(databaseName, tenantA))
        {
            var visibleToTenantA = await tenantAContext.Bills
                .AsNoTracking()
                .ToListAsync();

            visibleToTenantA.Should().ContainSingle()
                .Which.Id.Should().Be(billId);
        }
    }
}
