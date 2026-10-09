using Aonik.Platform.Contracts.Models.Autonumbering;
using Aonik.Platform.Entities.Autonumbering;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Autonumbering;
using Aonik.SharedKernel.Abstractions;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Autonumbering;

public class OrderNumberGeneratorTests
{
    private sealed class Clock : IClock
    {
        public DateTime UtcNow { get; } = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);
    }

    [Fact]
    public async Task Generate_Should_UseTenantProfile_AndMissingProfileCompatibilityWithoutCreatingConfiguration()
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<PlatformDbContext>().UseInMemoryDatabase($"Numbers_{Guid.NewGuid()}").Options;
        await using var configured = new PlatformDbContext(options, new TestTenantProvider(tenantId));
        configured.AutonumberProfiles.Add(Profile(tenantId));
        await configured.SaveChangesAsync();
        var generator = Generator(configured, tenantId);

        (await generator.GenerateAsync()).Should().Be("SHOP-0100");
        (await generator.GenerateAsync()).Should().Be("SHOP-0101");
        var otherTenant = Guid.NewGuid();
        await using var missing = new PlatformDbContext(options, new TestTenantProvider(otherTenant));
        var fallback = await Generator(missing, otherTenant).GenerateAsync();

        fallback.Should().MatchRegex("^ORD-20261009120000000-[0-9A-F]{8}$");
        (await missing.AutonumberProfiles.CountAsync()).Should().Be(0);
        (await configured.AutonumberProfiles.AsNoTracking().SingleAsync()).LastIssuedValue.Should().Be(101);
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("exhausted")]
    [InlineData("overflow")]
    [InlineData("unsafe-reset")]
    [InlineData("overlong")]
    [InlineData("padding")]
    public async Task Generate_Should_FailAuthoredInvalidProfiles_WithoutFallbackOrConsumingSequence(string reason)
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<PlatformDbContext>().UseInMemoryDatabase($"Numbers_{Guid.NewGuid()}").Options;
        await using var context = new PlatformDbContext(options, new TestTenantProvider(tenantId));
        var profile = Profile(tenantId);
        switch (reason)
        {
            case "inactive": profile.IsActive = false; break;
            case "exhausted": profile.LastIssuedValue = profile.MaxValue; break;
            case "overflow": profile.LastIssuedValue = profile.MaxValue = long.MaxValue; break;
            case "unsafe-reset": profile.ResetPolicy = AutonumberResetPolicy.Monthly; profile.PrefixTemplate = "ORD-{MM}-"; break;
            case "overlong": profile.PrefixTemplate = new string('x', 64); break;
            case "padding": profile.PaddingLength = 1000000000; break;
        }
        context.AutonumberProfiles.Add(profile);
        await context.SaveChangesAsync();
        var before = profile.LastIssuedValue;

        var generate = () => Generator(context, tenantId).GenerateAsync();

        await generate.Should().ThrowAsync<InvalidOperationException>();
        await context.SaveChangesAsync();
        (await context.AutonumberProfiles.AsNoTracking().SingleAsync()).LastIssuedValue.Should().Be(before);
        (await context.AutonumberProfiles.AsNoTracking().SingleAsync()).LastIssuedAt.Should().BeNull();
    }

    [Fact]
    public async Task Preview_Should_UseTheSameBoundedFormat_WithoutAllocating()
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<PlatformDbContext>().UseInMemoryDatabase($"Numbers_{Guid.NewGuid()}").Options;
        await using var context = new PlatformDbContext(options, new TestTenantProvider(tenantId));
        context.AutonumberProfiles.Add(Profile(tenantId));
        await context.SaveChangesAsync();
        var service = new AutonumberingService(context, new TestTenantProvider(tenantId), new Clock());

        var preview = await service.PreviewAsync(new AutonumberGenerateRequest("Order"));
        var generated = await service.GenerateAsync(new AutonumberGenerateRequest("Order"));

        preview.Should().Be(generated);
        (await context.AutonumberProfiles.AsNoTracking().SingleAsync()).LastIssuedValue.Should().Be(100);
    }

    private static OrderNumberGenerator Generator(PlatformDbContext context, Guid tenantId)
        => new(new AutonumberingService(context, new TestTenantProvider(tenantId), new Clock()),
            new TestTenantProvider(tenantId), new Clock());

    private static AutonumberProfile Profile(Guid tenantId) => new()
    {
        TenantId = tenantId, EntityType = "Order", PrefixTemplate = "SHOP-", PaddingLength = 4,
        MinValue = 100, MaxValue = 9999, LastIssuedValue = 99, IsActive = true,
        Strategy = AutonumberStrategy.Sequential, ResetPolicy = AutonumberResetPolicy.None
    };
}
