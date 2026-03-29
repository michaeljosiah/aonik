using Aonik.Ai.Contracts.Services;
using Aonik.Ai.Entities;
using Aonik.Ai.Persistence;
using Aonik.Ai.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.UserBrief;

public class UserMemoryServiceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private class TestTenantProvider : ITenantProvider
    {
        public Guid GetCurrentTenantId() => TenantId;
        public bool TryGetCurrentTenantId(out Guid tenantId) { tenantId = TenantId; return true; }
    }

    private class TestClock : IClock
    {
        public DateTime UtcNow { get; set; } = DateTime.UtcNow;
    }

    private static AiDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"UserMemoryTest_{Guid.NewGuid()}")
            .Options;
        return new AiDbContext(options, new TestTenantProvider());
    }

    private static UserMemoryService CreateService(AiDbContext dbContext, TestClock? clock = null)
    {
        return new UserMemoryService(dbContext, new TestTenantProvider(), clock ?? new TestClock());
    }

    [Fact]
    public async Task SetEntryAsync_Should_CreateNewEntry_When_NoExistingEntry()
    {
        using var db = CreateDbContext();
        var service = CreateService(db);

        var result = await service.SetEntryAsync(new SetUserMemoryEntryRequest(
            UserId, UserMemoryEntryType.Preference, "communication.reminder_time",
            "\"morning\"", 1.0m, UserMemorySource.UserStated));

        result.Should().NotBeNull();
        result.Key.Should().Be("communication.reminder_time");
        result.ValueJson.Should().Be("\"morning\"");
        result.Confidence.Should().Be(1.0m);
        result.EffectiveConfidence.Should().Be(1.0m);
        result.SupersededById.Should().BeNull();
    }

    [Fact]
    public async Task SetEntryAsync_Should_SupersedeExistingEntry_When_SameKeyExists()
    {
        using var db = CreateDbContext();
        var service = CreateService(db);

        var first = await service.SetEntryAsync(new SetUserMemoryEntryRequest(
            UserId, UserMemoryEntryType.Correction, "bills.rent_amount",
            "1150", 1.0m, UserMemorySource.UserStated));

        var second = await service.SetEntryAsync(new SetUserMemoryEntryRequest(
            UserId, UserMemoryEntryType.Correction, "bills.rent_amount",
            "1200", 1.0m, UserMemorySource.UserStated));

        second.Key.Should().Be("bills.rent_amount");
        second.ValueJson.Should().Be("1200");

        // The first entry should now be superseded
        var history = await service.GetEntryHistoryAsync(UserId, "bills.rent_amount");
        history.Should().HaveCount(2);

        // One entry should be superseded (old), one should be current (new)
        var current = history.Where(h => h.SupersededById is null).ToList();
        var superseded = history.Where(h => h.SupersededById is not null).ToList();
        current.Should().HaveCount(1);
        superseded.Should().HaveCount(1);
        current[0].ValueJson.Should().Be("1200");
        superseded[0].ValueJson.Should().Be("1150");
    }

    [Fact]
    public async Task GetCurrentEntriesAsync_Should_ExcludeSupersededEntries()
    {
        using var db = CreateDbContext();
        var service = CreateService(db);

        await service.SetEntryAsync(new SetUserMemoryEntryRequest(
            UserId, UserMemoryEntryType.Fact, "income.payday",
            "25", 0.8m, UserMemorySource.AiInferred));

        await service.SetEntryAsync(new SetUserMemoryEntryRequest(
            UserId, UserMemoryEntryType.Fact, "income.payday",
            "26", 0.9m, UserMemorySource.AiInferred));

        var current = await service.GetCurrentEntriesAsync(UserId);
        current.Should().HaveCount(1);
        current[0].ValueJson.Should().Be("26");
    }

    [Fact]
    public async Task GetCurrentEntriesAsync_Should_ApplyConfidenceDecay_ForAiInferred()
    {
        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var clock = new TestClock { UtcNow = baseTime };
        using var db = CreateDbContext();
        var service = CreateService(db, clock);

        // Create an entry at baseTime
        await service.SetEntryAsync(new SetUserMemoryEntryRequest(
            UserId, UserMemoryEntryType.Fact, "spending.pattern",
            "\"late_month_spender\"", 0.8m, UserMemorySource.AiInferred));

        // Move clock forward 60 days: effective = 0.8 - (60/30 * 0.1) = 0.8 - 0.2 = 0.6
        clock.UtcNow = baseTime.AddDays(60);

        var current = await service.GetCurrentEntriesAsync(UserId);
        current.Should().HaveCount(1);
        current[0].EffectiveConfidence.Should().BeApproximately(0.6m, 0.01m);
    }

    [Fact]
    public async Task GetCurrentEntriesAsync_Should_ExcludeEntriesBelowConfidenceFloor()
    {
        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var clock = new TestClock { UtcNow = baseTime };
        using var db = CreateDbContext();
        var service = CreateService(db, clock);

        await service.SetEntryAsync(new SetUserMemoryEntryRequest(
            UserId, UserMemoryEntryType.Fact, "spending.pattern",
            "\"late_month_spender\"", 0.5m, UserMemorySource.AiInferred));

        // Move clock 180 days forward: effective = 0.5 - (180/30 * 0.1) = 0.5 - 0.6 = -0.1 → clamped to 0
        clock.UtcNow = baseTime.AddDays(180);

        var current = await service.GetCurrentEntriesAsync(UserId);
        current.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCurrentEntriesAsync_Should_NotDecay_UserStatedEntries()
    {
        var baseTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var clock = new TestClock { UtcNow = baseTime };
        using var db = CreateDbContext();
        var service = CreateService(db, clock);

        await service.SetEntryAsync(new SetUserMemoryEntryRequest(
            UserId, UserMemoryEntryType.Preference, "agent.fx.auto_alert",
            "true", 1.0m, UserMemorySource.UserStated));

        // Move clock forward 1 year — user-stated entries should NOT decay
        clock.UtcNow = baseTime.AddDays(365);

        var current = await service.GetCurrentEntriesAsync(UserId);
        current.Should().HaveCount(1);
        current[0].EffectiveConfidence.Should().Be(1.0m);
    }

    [Fact]
    public async Task GetCurrentEntriesAsync_Should_FilterByEntryType()
    {
        using var db = CreateDbContext();
        var service = CreateService(db);

        await service.SetEntryAsync(new SetUserMemoryEntryRequest(
            UserId, UserMemoryEntryType.Preference, "communication.style",
            "\"concise\"", 1.0m, UserMemorySource.UserStated));

        await service.SetEntryAsync(new SetUserMemoryEntryRequest(
            UserId, UserMemoryEntryType.Identity, "corridor.countries",
            "[\"GB\",\"NG\"]", 1.0m, UserMemorySource.SystemDerived));

        var preferences = await service.GetCurrentEntriesAsync(UserId, UserMemoryEntryType.Preference);
        preferences.Should().HaveCount(1);
        preferences[0].Key.Should().Be("communication.style");
    }

    [Fact]
    public async Task ConfirmEntryAsync_Should_ResetLastConfirmedAt()
    {
        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var clock = new TestClock { UtcNow = baseTime };
        using var db = CreateDbContext();
        var service = CreateService(db, clock);

        var entry = await service.SetEntryAsync(new SetUserMemoryEntryRequest(
            UserId, UserMemoryEntryType.Fact, "income.payday",
            "25", 0.8m, UserMemorySource.AiInferred));

        // Move time forward 60 days and confirm
        clock.UtcNow = baseTime.AddDays(60);
        await service.ConfirmEntryAsync(entry.Id);

        var current = await service.GetCurrentEntriesAsync(UserId);
        current.Should().HaveCount(1);
        // After confirmation, effective confidence should be back to original (LastConfirmedAt = now)
        current[0].EffectiveConfidence.Should().Be(0.8m);
    }
}
