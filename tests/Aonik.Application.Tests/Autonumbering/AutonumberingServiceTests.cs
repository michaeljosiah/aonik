using System;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Models.Autonumbering;
using Aonik.Application.Services.Autonumbering;
using Aonik.Domain.Autonumbering.Entities;
using Aonik.Infrastructure.Persistence;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Application.Tests.Autonumbering;

public class AutonumberingServiceTests
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
        public TestClock(DateTime utcNow) => UtcNow = utcNow;

        public DateTime UtcNow { get; private set; }

        public void AdvanceTo(DateTime utcNow) => UtcNow = utcNow;
    }

    [Fact]
    public async Task GenerateAsync_ShouldIncrementSequenceAndFormatReference()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AonikDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;
        var clock = new TestClock(new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc));

        using var context = new AonikDbContext(options, new TestTenantProvider(tenantId));
        context.AutonumberProfiles.Add(new AutonumberProfile
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EntityType = "Invoice",
            PrefixTemplate = "INV-{YYYY}-",
            SuffixTemplate = string.Empty,
            Strategy = AutonumberStrategy.Sequential,
            ResetPolicy = AutonumberResetPolicy.None,
            PaddingLength = 4,
            MinValue = 1,
            MaxValue = 9999,
            LastIssuedValue = 0,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var service = new AutonumberingService(context, new TestTenantProvider(tenantId), clock);

        // Act
        var result = await service.GenerateAsync(new AutonumberGenerateRequest("Invoice"));

        // Assert
        result.SequenceValue.Should().Be(1);
        result.Reference.Should().Be("INV-2026-0001");
    }

    [Fact]
    public async Task GenerateAsync_ShouldResetSequence_WhenMonthlyPolicyCrossesBoundary()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AonikDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;
        var clock = new TestClock(new DateTime(2026, 2, 1, 8, 0, 0, DateTimeKind.Utc));

        using var context = new AonikDbContext(options, new TestTenantProvider(tenantId));
        context.AutonumberProfiles.Add(new AutonumberProfile
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EntityType = "Order",
            PrefixTemplate = "ORD-{MM}-",
            SuffixTemplate = string.Empty,
            Strategy = AutonumberStrategy.Sequential,
            ResetPolicy = AutonumberResetPolicy.Monthly,
            PaddingLength = 3,
            MinValue = 100,
            MaxValue = 999,
            LastIssuedValue = 250,
            LastIssuedAt = new DateTime(2026, 1, 31, 23, 0, 0, DateTimeKind.Utc),
            IsActive = true
        });
        await context.SaveChangesAsync();

        var service = new AutonumberingService(context, new TestTenantProvider(tenantId), clock);

        // Act
        var result = await service.GenerateAsync(new AutonumberGenerateRequest("Order"));

        // Assert
        result.SequenceValue.Should().Be(100);
        result.Reference.Should().Be("ORD-02-100");
    }
}
