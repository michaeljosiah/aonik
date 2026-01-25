using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Models.Pricing;
using Aonik.Application.Services.Compliance;
using Aonik.Application.Services.Pricing;
using Aonik.Domain.Party.Entities;
using Aonik.Domain.Pricing.Entities;
using Aonik.Infrastructure.Persistence;
using Aonik.SharedKernel.Abstractions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Pricing;

public class PricingServiceTests
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

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; init; } = DateTime.UtcNow;
    }

    private sealed class NoOpAuditLogWriter : IAuditLogWriter
    {
        public Task LogAsync(
            string action,
            string resourceType,
            Guid resourceId,
            Guid tenantId,
            Guid? actorId,
            string? correlationId,
            string? detailsJson = null,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private static AonikDbContext CreateDbContext(Guid tenantId, IClock clock)
    {
        var options = new DbContextOptionsBuilder<AonikDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;

        return new AonikDbContext(options, new TestTenantProvider(tenantId), clock: clock);
    }

    [Fact]
    public async Task GetBillPaymentQuoteAsync_ShouldReturnTotals_WhenPolicyAndRateMatch()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var clock = new TestClock { UtcNow = DateTime.UtcNow };
        using var context = CreateDbContext(tenantId, clock);

        context.FeePolicies.Add(new FeePolicy
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Default",
            FixedFee = 1.00m,
            PercentageFee = 0.10m,
            ConditionsJson = "{}",
            IsActive = true
        });

        context.FxQuotes.Add(new FxQuote
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrency = "USD",
            TargetCurrency = "KES",
            Rate = 2.00m,
            ExpiresAt = clock.UtcNow.AddHours(1),
            Provider = "Test"
        });

        context.LimitsPolicies.Add(new LimitsPolicy
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ScopeType = "Tenant",
            ScopeId = tenantId,
            Currency = "USD",
            MaxAmount = 1000m,
            Period = "Monthly",
            IsActive = true
        });

        await context.SaveChangesAsync();

        var tenantProvider = new TestTenantProvider(tenantId);
        var policyService = new PricingPolicyService(context, tenantProvider);
        var fxRateService = new FxRateService(context, clock);
        var service = new PricingService(
            tenantProvider,
            policyService,
            fxRateService,
            new CurrencyMetadataProvider(),
            new NoOpAuditLogWriter(),
            context,
            clock);

        var request = new PricingQuoteRequest(
            "USD",
            "KES",
            "US",
            "KE",
            "BILLPAY",
            null,
            10.00m,
            null,
            null,
            null);

        // Act
        var result = await service.GetBillPaymentQuoteAsync(request);

        // Assert
        result.ExchangeRate.Should().Be(2.00m);
        result.OriginAmount.Should().Be(10.00m);
        result.DestinationAmount.Should().Be(20.00m);
        result.FeesTotal.Should().Be(2.00m);
        result.TotalAmount.Should().Be(12.00m);
    }

    [Fact]
    public async Task GetBillPaymentQuoteAsync_ShouldThrow_WhenAmountExceedsLimits()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var clock = new TestClock { UtcNow = DateTime.UtcNow };
        using var context = CreateDbContext(tenantId, clock);

        context.FeePolicies.Add(new FeePolicy
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Default",
            FixedFee = 0.00m,
            PercentageFee = 0.00m,
            ConditionsJson = "{}",
            IsActive = true
        });

        context.FxQuotes.Add(new FxQuote
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrency = "USD",
            TargetCurrency = "KES",
            Rate = 2.00m,
            ExpiresAt = clock.UtcNow.AddHours(1),
            Provider = "Test"
        });

        context.LimitsPolicies.Add(new LimitsPolicy
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ScopeType = "Tenant",
            ScopeId = tenantId,
            Currency = "USD",
            MaxAmount = 5m,
            Period = "Monthly",
            IsActive = true
        });

        await context.SaveChangesAsync();

        var tenantProvider = new TestTenantProvider(tenantId);
        var policyService = new PricingPolicyService(context, tenantProvider);
        var fxRateService = new FxRateService(context, clock);
        var service = new PricingService(
            tenantProvider,
            policyService,
            fxRateService,
            new CurrencyMetadataProvider(),
            new NoOpAuditLogWriter(),
            context,
            clock);

        var request = new PricingQuoteRequest(
            "USD",
            "KES",
            "US",
            "KE",
            "BILLPAY",
            null,
            10.00m,
            null,
            null,
            null);

        // Act
        var act = async () => await service.GetBillPaymentQuoteAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Requested amount exceeds corridor limits.");
    }

    [Fact]
    public async Task GetBillPaymentQuoteAsync_ShouldIncludeCapAdjustment_WhenFeesAreCapped()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var clock = new TestClock { UtcNow = DateTime.UtcNow };
        using var context = CreateDbContext(tenantId, clock);

        var conditionsJson = "{" +
            "\"minFee\":1.0," +
            "\"maxFee\":2.0," +
            "\"feeBreakdown\":[" +
                "{\"code\":\"SERVICE_FEE\",\"description\":\"Service fee\",\"calculationType\":\"Fixed\"}," +
                "{\"code\":\"PERCENT_FEE\",\"description\":\"Percentage fee\",\"calculationType\":\"Percentage\"}" +
            "]" +
        "}";

        context.FeePolicies.Add(new FeePolicy
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Default",
            FixedFee = 1.00m,
            PercentageFee = 0.50m,
            ConditionsJson = conditionsJson,
            IsActive = true
        });

        context.FxQuotes.Add(new FxQuote
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrency = "USD",
            TargetCurrency = "KES",
            Rate = 1.00m,
            ExpiresAt = clock.UtcNow.AddHours(1),
            Provider = "Test"
        });

        context.LimitsPolicies.Add(new LimitsPolicy
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ScopeType = "Tenant",
            ScopeId = tenantId,
            Currency = "USD",
            MaxAmount = 1000m,
            Period = "Monthly",
            IsActive = true
        });

        await context.SaveChangesAsync();

        var tenantProvider = new TestTenantProvider(tenantId);
        var policyService = new PricingPolicyService(context, tenantProvider);
        var fxRateService = new FxRateService(context, clock);
        var service = new PricingService(
            tenantProvider,
            policyService,
            fxRateService,
            new CurrencyMetadataProvider(),
            new NoOpAuditLogWriter(),
            context,
            clock);

        var request = new PricingQuoteRequest(
            "USD",
            "KES",
            "US",
            "KE",
            "BILLPAY",
            null,
            10.00m,
            null,
            null,
            null);

        // Act
        var result = await service.GetBillPaymentQuoteAsync(request);

        // Assert
        result.FeesTotal.Should().Be(2.00m);
        result.FeeBreakdown.Should().Contain(item => item.Code == "FEE_CAP_ADJUSTMENT" && item.Amount == -4.00m);
    }

    [Fact]
    public async Task GetBillPaymentQuoteAsync_ShouldUseCustomerTierFromParty_WhenNotProvided()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var clock = new TestClock { UtcNow = DateTime.UtcNow };
        using var context = CreateDbContext(tenantId, clock);

        var retailConditions = "{\"customerTier\":\"Retail\"}";
        var smbConditions = "{\"customerTier\":\"SMB\"}";

        context.FeePolicies.AddRange(
            new FeePolicy
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Retail",
                FixedFee = 1.00m,
                PercentageFee = 0.00m,
                ConditionsJson = retailConditions,
                IsActive = true
            },
            new FeePolicy
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "SMB",
                FixedFee = 5.00m,
                PercentageFee = 0.00m,
                ConditionsJson = smbConditions,
                IsActive = true
            });

        context.FxQuotes.Add(new FxQuote
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrency = "USD",
            TargetCurrency = "KES",
            Rate = 1.00m,
            ExpiresAt = clock.UtcNow.AddHours(1),
            Provider = "Test"
        });

        context.LimitsPolicies.Add(new LimitsPolicy
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ScopeType = "Tenant",
            ScopeId = tenantId,
            Currency = "USD",
            MaxAmount = 1000m,
            Period = "Monthly",
            IsActive = true
        });

        context.Parties.Add(new Party
        {
            Id = customerId,
            TenantId = tenantId,
            PartyType = "Person",
            DisplayName = "SMB Customer",
            Status = "Active",
            CustomerTierCode = "SMB"
        });

        await context.SaveChangesAsync();

        var tenantProvider = new TestTenantProvider(tenantId);
        var policyService = new PricingPolicyService(context, tenantProvider);
        var fxRateService = new FxRateService(context, clock);
        var service = new PricingService(
            tenantProvider,
            policyService,
            fxRateService,
            new CurrencyMetadataProvider(),
            new NoOpAuditLogWriter(),
            context,
            clock);

        var request = new PricingQuoteRequest(
            "USD",
            "KES",
            "US",
            "KE",
            "BILLPAY",
            null,
            10.00m,
            customerId,
            null,
            null);

        // Act
        var result = await service.GetBillPaymentQuoteAsync(request);

        // Assert
        result.FeesTotal.Should().Be(5.00m);
    }

    [Fact]
    public async Task GetBillPaymentQuoteAsync_ShouldReduceEffectiveRate_WhenMarkupIsPositive()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var clock = new TestClock { UtcNow = DateTime.UtcNow };
        using var context = CreateDbContext(tenantId, clock);

        var conditionsJson = "{\"markupBps\":100}";

        context.FeePolicies.Add(new FeePolicy
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Default",
            FixedFee = 0.00m,
            PercentageFee = 0.00m,
            ConditionsJson = conditionsJson,
            IsActive = true
        });

        context.FxQuotes.Add(new FxQuote
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrency = "USD",
            TargetCurrency = "KES",
            Rate = 2.00m,
            ExpiresAt = clock.UtcNow.AddHours(1),
            Provider = "Test"
        });

        context.LimitsPolicies.Add(new LimitsPolicy
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ScopeType = "Tenant",
            ScopeId = tenantId,
            Currency = "USD",
            MaxAmount = 1000m,
            Period = "Monthly",
            IsActive = true
        });

        await context.SaveChangesAsync();

        var tenantProvider = new TestTenantProvider(tenantId);
        var policyService = new PricingPolicyService(context, tenantProvider);
        var fxRateService = new FxRateService(context, clock);
        var service = new PricingService(
            tenantProvider,
            policyService,
            fxRateService,
            new CurrencyMetadataProvider(),
            new NoOpAuditLogWriter(),
            context,
            clock);

        var request = new PricingQuoteRequest(
            "USD",
            "KES",
            "US",
            "KE",
            "BILLPAY",
            198m,
            null,
            null,
            null,
            null);

        // Act
        var result = await service.GetBillPaymentQuoteAsync(request);

        // Assert
        result.ExchangeRate.Should().Be(1.98m);
        result.OriginAmount.Should().Be(100.00m);
    }

    [Fact]
    public async Task GetBillPaymentQuoteAsync_ShouldNotCreateFxMarkup_WhenMarkupIsZeroAndDestinationRounded()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var clock = new TestClock { UtcNow = DateTime.UtcNow };
        using var context = CreateDbContext(tenantId, clock);

        var conditionsJson = "{\"markupBps\":0,\"feeBreakdown\":[" +
            "{\"code\":\"FX_MARKUP\",\"description\":\"FX spread\",\"calculationType\":\"FxMarkup\"}" +
        "]}";

        context.FeePolicies.Add(new FeePolicy
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Default",
            FixedFee = 0.00m,
            PercentageFee = 0.00m,
            ConditionsJson = conditionsJson,
            IsActive = true
        });

        context.FxQuotes.Add(new FxQuote
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrency = "USD",
            TargetCurrency = "KES",
            Rate = 2.00m,
            ExpiresAt = clock.UtcNow.AddHours(1),
            Provider = "Test"
        });

        context.LimitsPolicies.Add(new LimitsPolicy
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ScopeType = "Tenant",
            ScopeId = tenantId,
            Currency = "USD",
            MaxAmount = 1000m,
            Period = "Monthly",
            IsActive = true
        });

        await context.SaveChangesAsync();

        var tenantProvider = new TestTenantProvider(tenantId);
        var policyService = new PricingPolicyService(context, tenantProvider);
        var fxRateService = new FxRateService(context, clock);
        var service = new PricingService(
            tenantProvider,
            policyService,
            fxRateService,
            new CurrencyMetadataProvider(),
            new NoOpAuditLogWriter(),
            context,
            clock);

        var request = new PricingQuoteRequest(
            "USD",
            "KES",
            "US",
            "KE",
            "BILLPAY",
            199m,
            null,
            null,
            null,
            null);

        // Act
        var result = await service.GetBillPaymentQuoteAsync(request);

        // Assert
        result.FeeBreakdown.Should().ContainSingle(item => item.Code == "FX_MARKUP" && item.Amount == 0m);
    }
}
