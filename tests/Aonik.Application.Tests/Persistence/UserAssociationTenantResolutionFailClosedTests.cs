using Aonik.Infrastructure.Persistence;
using Aonik.Platform.Entities.Identity;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Persistence;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Persistence;

/// <summary>
/// Regression cover for the login path broken by the fail-closed tenant filter (finding C5).
/// <para>
/// During JWT validation, <c>AonikAuthenticationSetup.ResolveFromUserAssociationAsync</c>
/// discovers WHICH tenant an authenticating user belongs to by looking the user up in the
/// tenant-scoped <c>Users</c> table keyed on their global IdP identity (iss + sub). This runs
/// BEFORE any tenant is resolved, so the DbContext has no ambient tenant. Under the old
/// fail-open filter a no-tenant query returned every row, so the lookup happened to succeed.
/// Under fail-closed, a no-tenant query returns only global rows — the real (tenant-scoped)
/// user row is hidden, the lookup returns null, and the user is rejected with a 401
/// ("Tenant could not be resolved"). This is exactly what bricked the /host/me/tenants
/// bootstrap call after Phase 2 shipped.
/// </para>
/// <para>
/// The fix is the sanctioned <see cref="QueryFilterIntentExtensions.AcrossTenants{TEntity}"/>
/// escape hatch: the lookup is cross-tenant by necessity (it is resolving the tenant), and the
/// iss + sub predicate keeps the read scoped to the single authenticated user. These tests pin
/// (1) why the regression happened and (2) that the escape hatch restores the lookup without
/// widening it to other users.
/// </para>
/// </summary>
public class UserAssociationTenantResolutionFailClosedTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");

    private const string Issuer = "https://aonik-dev.eu.auth0.com/";
    private const string AliceSubject = "auth0|alice";
    private const string BobSubject = "auth0|bob";

    [Fact]
    public async Task UserLookupByIssSub_Should_ReturnNull_When_NoTenantResolved_AndFiltersApplied()
    {
        // Reproduces the regression: the exact query shape from
        // ResolveFromUserAssociationAsync, run with NO ambient tenant and WITHOUT the
        // escape hatch. Fail-closed hides the tenant-scoped user row, so the auth pipeline
        // cannot resolve a tenant and would 401 the login.
        var dbName = $"UserAssocFailClosed_{Guid.NewGuid()}";
        await SeedUserAsync(dbName, TenantA, Issuer, AliceSubject);

        await using var noTenantCtx = CreateContext(dbName, new NoTenantProvider());

        var resolved = await noTenantCtx.Users
            .Where(u => u.ExternalIssuer == Issuer && u.ExternalSubject == AliceSubject)
            .Select(u => new { u.TenantId })
            .FirstOrDefaultAsync();

        resolved.Should().BeNull(
            because: "fail-closed hides the tenant-scoped user when no tenant is resolved — this is the 401 cause the fix must work around");
    }

    [Fact]
    public async Task UserLookupByIssSub_Should_ResolveTenant_When_NoTenantResolved_AndAcrossTenants()
    {
        // The fix: the same query plus AcrossTenants() recovers the user's tenant even with
        // no ambient tenant context, so login tenant resolution works again.
        var dbName = $"UserAssocFailClosed_{Guid.NewGuid()}";
        await SeedUserAsync(dbName, TenantA, Issuer, AliceSubject);

        await using var noTenantCtx = CreateContext(dbName, new NoTenantProvider());

        var resolved = await noTenantCtx.Users
            .AcrossTenants()
            .Where(u => u.ExternalIssuer == Issuer && u.ExternalSubject == AliceSubject)
            .Select(u => new { u.TenantId })
            .FirstOrDefaultAsync();

        resolved.Should().NotBeNull(
            because: "the sanctioned cross-tenant escape hatch must let login discover the user's tenant before any tenant is resolved");
        resolved!.TenantId.Should().Be(TenantA);
    }

    [Fact]
    public async Task UserLookupByIssSub_Should_StayScopedToTheOneUser_When_AcrossTenants()
    {
        // Guard against over-widening: AcrossTenants() bypasses the tenant filter, so the
        // iss + sub predicate is the only thing scoping the read. Two users in two tenants
        // must not collide — a lookup for Alice must return Alice's tenant, never Bob's.
        var dbName = $"UserAssocFailClosed_{Guid.NewGuid()}";
        await SeedUserAsync(dbName, TenantA, Issuer, AliceSubject);
        await SeedUserAsync(dbName, TenantB, Issuer, BobSubject);

        await using var noTenantCtx = CreateContext(dbName, new NoTenantProvider());

        var matches = await noTenantCtx.Users
            .AcrossTenants()
            .Where(u => u.ExternalIssuer == Issuer && u.ExternalSubject == AliceSubject)
            .Select(u => new { u.ExternalSubject, u.TenantId })
            .ToListAsync();

        matches.Should().ContainSingle(because: "iss + sub identifies exactly one user across all tenants")
            .Which.TenantId.Should().Be(TenantA);
    }

    // ── Test helpers ────────────────────────────────────────────────────

    private static AonikDbContext CreateContext(string dbName, ITenantProvider tenantProvider)
    {
        var options = new DbContextOptionsBuilder<AonikDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AonikDbContext(options, tenantProvider);
    }

    private static async Task SeedUserAsync(string dbName, Guid tenantId, string issuer, string subject)
    {
        await using var seedCtx = CreateContext(dbName, new FixedTenantProvider(tenantId));
        seedCtx.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ExternalIssuer = issuer,
            ExternalSubject = subject,
            Email = $"{subject}@example.test",
            Status = "Active",
        });
        await seedCtx.SaveChangesAsync();
    }

    /// <summary>Tenant provider that always reports a fixed, resolved tenant (seeding).</summary>
    private sealed class FixedTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;
        public FixedTenantProvider(Guid tenantId) => _tenantId = tenantId;
        public Guid GetCurrentTenantId() => _tenantId;
        public bool TryGetCurrentTenantId(out Guid tenantId)
        {
            tenantId = _tenantId;
            return true;
        }
    }

    /// <summary>
    /// Tenant provider that reports NO resolved tenant — mirrors the DbContext state during
    /// JWT validation, before a tenant has been resolved from the user association.
    /// </summary>
    private sealed class NoTenantProvider : ITenantProvider
    {
        public Guid GetCurrentTenantId() =>
            throw new InvalidOperationException("No tenant resolved.");
        public bool TryGetCurrentTenantId(out Guid tenantId)
        {
            tenantId = Guid.Empty;
            return false;
        }
    }
}
