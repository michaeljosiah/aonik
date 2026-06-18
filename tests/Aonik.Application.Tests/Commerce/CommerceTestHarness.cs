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

    public static CommerceDbContext CreateContext(DbContextOptions<CommerceDbContext> options, Guid tenantId)
        => new(options, new TestTenantProvider(tenantId), new TestCurrentUserProvider());
}
