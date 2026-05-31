using Aonik.Agents.Entities;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Persistence;

/// <summary>
/// Fail-closed verification for the global tenant query filters in
/// <see cref="Aonik.SharedKernel.Persistence.AonikDbContextBase"/> (finding C5).
/// <para>
/// When a DbContext runs without a resolved tenant (CurrentTenantId == null) the
/// filter must expose ONLY global rows — never another tenant's data. Previously a
/// missing tenant "failed open": the predicate carried a
/// <c>(CurrentTenantId == null) OR ...</c> disjunct, so an unscoped reader saw every
/// tenant's rows. These tests pin the corrected fail-closed behaviour for both the
/// non-nullable (<c>ApplyTenantQueryFilters</c>) and nullable
/// (<c>ApplyNullableTenantQueryFilter</c>) paths.
/// </para>
/// </summary>
public class FailClosedTenantFilterTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");

    [Fact]
    public async Task NonNullableTenantFilter_Should_ReturnNoRows_When_NoTenantContextResolved()
    {
        // Proposal : ITenantScoped (non-nullable Guid TenantId). We seed only real-tenant
        // rows (no Guid.Empty global row). A reader with no resolved tenant must therefore
        // see NOTHING — the old fail-open code returned both tenants' rows here.
        var dbName = $"FailClosedDb_{Guid.NewGuid()}";

        await using (var seedCtx = CreateAgentsDbContext(dbName, new FixedTenantProvider(TenantA)))
        {
            SeedProposal(seedCtx, TenantA);
            SeedProposal(seedCtx, TenantB);
            await seedCtx.SaveChangesAsync();
        }

        await using var noTenantCtx = CreateAgentsDbContext(dbName, new NoTenantProvider());

        var visible = await noTenantCtx.Proposals.AsNoTracking().ToListAsync();

        visible.Should().BeEmpty(
            because: "a context with no resolved tenant must fail closed — neither tenant's rows may leak");
    }

    [Fact]
    public async Task NullableTenantFilter_Should_ReturnOnlyGlobalRows_When_NoTenantContextResolved()
    {
        // Agent has a nullable Guid? TenantId; global rows are TenantId == null. A reader
        // with no resolved tenant must see the global row ONLY — not either tenant's rows.
        var dbName = $"FailClosedDb_{Guid.NewGuid()}";
        Guid globalAgentId;

        await using (var seedCtx = CreateAgentsDbContext(dbName, new FixedTenantProvider(TenantA)))
        {
            SeedAgent(seedCtx, TenantA, "TenantA Agent");
            SeedAgent(seedCtx, TenantB, "TenantB Agent");
            globalAgentId = SeedAgent(seedCtx, tenantId: null, "Global Agent").Id;
            await seedCtx.SaveChangesAsync();
        }

        await using var noTenantCtx = CreateAgentsDbContext(dbName, new NoTenantProvider());

        var visible = await noTenantCtx.Agents.AsNoTracking().ToListAsync();

        visible.Should().ContainSingle(
            because: "only the global (TenantId == null) row may surface when no tenant is resolved")
            .Which.Id.Should().Be(globalAgentId);
    }

    [Fact]
    public async Task TenantFilter_Should_StillScopeToResolvedTenant_When_TenantContextPresent()
    {
        // Guard against over-correction: a resolved tenant must still see exactly its own
        // rows. This proves the fail-closed change did not break ordinary tenant scoping.
        var dbName = $"FailClosedDb_{Guid.NewGuid()}";

        await using (var seedCtx = CreateAgentsDbContext(dbName, new FixedTenantProvider(TenantA)))
        {
            SeedProposal(seedCtx, TenantA);
            SeedProposal(seedCtx, TenantB);
            await seedCtx.SaveChangesAsync();
        }

        await using var tenantACtx = CreateAgentsDbContext(dbName, new FixedTenantProvider(TenantA));

        var visible = await tenantACtx.Proposals.AsNoTracking().ToListAsync();

        visible.Should().ContainSingle(because: "a resolved tenant sees only its own rows")
            .Which.TenantId.Should().Be(TenantA);
    }

    // ── Test helpers ────────────────────────────────────────────────────

    private static AgentsDbContext CreateAgentsDbContext(string dbName, ITenantProvider tenantProvider)
    {
        var options = new DbContextOptionsBuilder<AgentsDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AgentsDbContext(options, tenantProvider);
    }

    private static Agent SeedAgent(AgentsDbContext dbContext, Guid? tenantId, string name)
    {
        var agent = new Agent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            Domain = "Test",
            Description = "test",
            InstructionsText = string.Empty,
            ToolsetIdsJson = "[]",
            InputSchemaJson = "{}",
            OutputSchemaJson = "{}",
            PermissionsProfileJson = "{}",
            RiskTier = "Low",
            IsActive = true,
        };
        dbContext.Agents.Add(agent);
        return agent;
    }

    private static Proposal SeedProposal(AgentsDbContext dbContext, Guid tenantId)
    {
        var proposal = new Proposal
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProposedByAgentId = Guid.NewGuid(),
            ProposalType = "Test",
            ImpactSummary = "test",
            RiskTier = "Low",
            PayloadJson = "{}",
        };
        dbContext.Proposals.Add(proposal);
        return proposal;
    }

    /// <summary>Tenant provider that always reports a fixed, resolved tenant.</summary>
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
    /// Tenant provider that reports NO resolved tenant — mirrors a real
    /// <c>HttpContextTenantProvider</c> on an unauthenticated request or a
    /// <c>StaticTenantProvider(Guid.Empty)</c> background scope.
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
