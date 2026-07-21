using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Catalog;
using Aonik.SharedKernel.Abstractions;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

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

    /// <summary>Spec 066 — the option catalogue/resolution service the catalog reads depend on.</summary>
    public static ProductOptionService NewOptionService(CommerceDbContext ctx, Guid tenantId)
        => new(ctx, new TestTenantProvider(tenantId), NullLogger<ProductOptionService>.Instance);

    /// <summary>Spec 066 — selection validation, canonicalisation and difference pricing.</summary>
    public static OptionSelectionService NewSelectionService(CommerceDbContext ctx, Guid tenantId)
        => new(ctx, NewOptionService(ctx, tenantId), new TestTenantProvider(tenantId));

    /// <summary>Builds a ProductService with its Spec 066 option dependency wired.</summary>
    public static ProductService NewProductService(CommerceDbContext ctx, Guid tenantId)
        => new(ctx, new TestTenantProvider(tenantId), NewOptionService(ctx, tenantId));
}
