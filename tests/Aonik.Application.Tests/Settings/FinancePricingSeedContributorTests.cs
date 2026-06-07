using Aonik.Finance.Entities;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Seeding;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Settings;

public class FinancePricingSeedContributorTests
{
    [Fact]
    public async Task SeedAsync_Should_SeedRemittanceFxRatesAndFeePolicies_ForEveryTenantWithUsers()
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseInMemoryDatabase($"FinancePricingSeed_{Guid.NewGuid()}")
            .Options;
        var tenantId = Guid.NewGuid();
        await using var context = new FinanceDbContext(
            options,
            new TestTenantProvider(tenantId),
            clock: new TestClock());
        context.Users.Add(new UserReadModel
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = "seed@example.test"
        });
        await context.SaveChangesAsync();
        var seed = new FinancePricingSeedContributor(context, options, new TestClock());

        await seed.SeedAsync();

        var fxPairs = await context.FxQuotes
            .Select(quote => new { quote.TenantId, quote.BaseCurrency, quote.TargetCurrency, quote.Rate })
            .ToListAsync();
        fxPairs.Should().Contain(pair =>
            pair.TenantId == tenantId
            && pair.BaseCurrency == "GBP"
            && pair.TargetCurrency == "NGN"
            && pair.Rate == 1985.25m);
        fxPairs.Should().Contain(pair =>
            pair.TenantId == tenantId
            && pair.BaseCurrency == "NGN"
            && pair.TargetCurrency == "NGN"
            && pair.Rate == 1m);

        var policyNames = await context.FeePolicies.Select(policy => policy.Name).ToListAsync();
        policyNames.Should().Contain("Remittance-UK-NG-Default");
        policyNames.Should().Contain("Remittance-NG-NG-Default");
    }

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow => new(2026, 6, 7, 12, 0, 0, DateTimeKind.Utc);
    }

    private sealed class TestTenantProvider(Guid tenantId) : ITenantProvider
    {
        public Guid GetCurrentTenantId() => tenantId;

        public bool TryGetCurrentTenantId(out Guid resolvedTenantId)
        {
            resolvedTenantId = tenantId;
            return true;
        }
    }
}
