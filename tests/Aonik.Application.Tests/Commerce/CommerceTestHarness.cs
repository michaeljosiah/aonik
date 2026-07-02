using Aonik.Commerce.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Commerce;

/// <summary>Shared scaffolding for the Commerce catalog/pricing tests (Spec 042).</summary>
internal static class CommerceTestHarness
{
    public sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 6, 18, 12, 0, 0, DateTimeKind.Utc);
    }

    public static (DbContextOptions<CommerceDbContext> Options, Guid TenantId) NewDb()
        => (new DbContextOptionsBuilder<CommerceDbContext>()
                .UseInMemoryDatabase($"Commerce_{Guid.NewGuid()}").Options,
            Guid.NewGuid());

    /// <summary>Optionally pass a <see cref="TestClock"/> so audit stamping (CreatedAt — the
    /// Spec 054 claim-order tiebreaker) is test-controlled instead of wall-clock.</summary>
    public static CommerceDbContext CreateContext(DbContextOptions<CommerceDbContext> options, Guid tenantId, IClock? clock = null)
        => new(options, new TestTenantProvider(tenantId), new TestCurrentUserProvider(), clock);
}
