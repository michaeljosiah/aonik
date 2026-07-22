using Aonik.Commerce.Contracts.Models.Checkout;
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
        => new(ctx, new TestTenantProvider(tenantId), new ProductContentReviewFlagger(ctx), NullLogger<ProductOptionService>.Instance);

    /// <summary>Spec 067 — content authoring + exact-selection resolution.</summary>
    public static ProductContentService NewContentService(CommerceDbContext ctx, Guid tenantId)
        => new(ctx, new TestTenantProvider(tenantId), NewSelectionService(ctx, tenantId), NewOptionService(ctx, tenantId));

    /// <summary>Spec 066 — selection validation, canonicalisation and difference pricing.</summary>
    public static OptionSelectionService NewSelectionService(CommerceDbContext ctx, Guid tenantId)
        => new(ctx, NewOptionService(ctx, tenantId), new TestTenantProvider(tenantId));

    /// <summary>Builds a ProductService with its Spec 066 option dependency wired.</summary>
    public static ProductService NewProductService(CommerceDbContext ctx, Guid tenantId)
        => new(ctx, new TestTenantProvider(tenantId), NewOptionService(ctx, tenantId), NullLogger<ProductService>.Instance);
}

/// <summary>Settings null-objects: no tenant override, no registered default — services fall
/// back to their own defaults (delivery amounts 0/0 for the box quote).</summary>
internal sealed class NullTenantSettingStore : Aonik.SharedKernel.Abstractions.Settings.ITenantSettingStore
{
    public Task<string?> GetTenantValueAsync(string key, Guid tenantId, CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);

    public Task SetTenantValueAsync(string key, string? value, Guid tenantId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

internal sealed class NullSettingProvider : Aonik.SharedKernel.Abstractions.Settings.ISettingProvider
{
    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);

    public Task<string> GetRequiredAsync(string key, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException($"No value for '{key}'.");

    public Task<string?> GetForScopeAsync(
        string key, Aonik.SharedKernel.Abstractions.Settings.SettingScope scope,
        Guid? tenantId = null, Guid? userId = null, CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);

    public Task<Aonik.SharedKernel.Abstractions.Settings.SettingResolution> GetResolvedAsync(
        string key, Guid? tenantId = null, Guid? userId = null, CancellationToken cancellationToken = default)
        => Task.FromResult(new Aonik.SharedKernel.Abstractions.Settings.SettingResolution(key, null, "none"));
}

/// <summary>R10 threading for tests: every harness cart is party-bound, so the owning access
/// context is the party principal. Guest-token paths get their own dedicated tests.</summary>
internal static class CartTestAccess
{
    public static CartAccessContext Owner(CartDto cart)
        => CartAccessContext.ForParty(cart.BuyerPartyId!.Value);
}
