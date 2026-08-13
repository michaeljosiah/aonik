using Aonik.Ai.Entities.Safety;
using Aonik.Ai.Persistence;
using Aonik.Ai.Services.Safety;
using Aonik.IntegrationTests.Support;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Persistence;
using Aonik.SharedKernel.Abstractions.Safety;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Database.Tests;

/// <summary>
/// Spec 096 §13 — the retention sweep, in the only lane that can prove it works.
///
/// <para>
/// These started life as InMemory tests and had to move, which is the point. <c>AonikDbContextBase</c>
/// converts every <c>EntityState.Deleted</c> into <c>IsDeleted = true</c> — correct for ordinary
/// business data, and exactly wrong for a sweep whose job is to remove blocked content about
/// children. The first implementation used <c>RemoveRange</c> and therefore <strong>retained
/// everything forever while reporting that it had deleted something</strong>.
/// </para>
///
/// <para>
/// The fix is <c>ExecuteDeleteAsync</c>, which bypasses the change tracker — and which the InMemory
/// provider does not implement at all. So the behaviour that matters here is <em>structurally
/// untestable</em> on InMemory: a test there could only ever assert the intent. This lane is where
/// it can actually fail.
/// </para>
/// </summary>
public class SafetyRetentionSqlServerTests : IClassFixture<SqlLocalDbFixture>
{
    private readonly SqlLocalDbFixture _db;
    private static readonly DateTime Now = new(2026, 8, 13, 3, 30, 0, DateTimeKind.Utc);

    public SafetyRetentionSqlServerTests(SqlLocalDbFixture db)
    {
        _db = db;
    }

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; } = Now;
    }

    private AiDbContext CreateContext(Guid tenantId)
        => new(
            new DbContextOptionsBuilder<AiDbContext>()
                .UseSqlServer(_db.ConnectionString)
                .Options,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(Guid.NewGuid()),
            new TestClock());

    private static SafetyRetentionSweeper CreateSweeper(AiDbContext context, Guid tenantId)
        => new(
            context,
            new TestTenantProvider(tenantId),
            new TestClock(),
            Microsoft.Extensions.Options.Options.Create(new SafetyOptions()),
            NullLogger<SafetyRetentionSweeper>.Instance);

    private static async Task<Guid> SeedDecisionAsync(
        AiDbContext context, Guid tenantId, DateTime expiresAt, Guid? subject = null)
    {
        var decision = new SafetyDecision
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SubjectPartyId = subject ?? Guid.NewGuid(),
            SafetyBand = "6-9",
            Modality = SafetyModalities.Text,
            Layer = SafetyLayers.Output,
            Outcome = nameof(SafetyDecisionOutcome.Blocked),
            SafetyPolicyVersion = "v1",
            DecidedAt = Now.AddDays(-100),
            ExpiresAt = expiresAt
        };
        context.SafetyDecisions.Add(decision);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        return decision.Id;
    }

    private static async Task SeedIncidentAsync(
        AiDbContext context, Guid tenantId, Guid decisionId, string category, bool legalHold,
        DateTime? occurredAt = null)
    {
        context.SafetyIncidents.Add(new SafetyIncident
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SafetyDecisionId = decisionId,
            SubjectPartyId = Guid.NewGuid(),
            Category = category,
            IsNonOverridable = SafetyCategories.IsNonOverridable(category),
            IsUnderLegalHold = legalHold,
            AppealState = SafetyAppealStates.None,
            OccurredAt = occurredAt ?? Now.AddDays(-10)
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    private static async Task SeedArtefactAsync(
        AiDbContext context, Guid tenantId, DateTime expiresAt, bool legalHold)
    {
        context.SafetyArtefacts.Add(new SafetyArtefact
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SafetyIncidentId = Guid.NewGuid(),
            Reference = "blob://blocked",
            ExpiresAt = expiresAt,
            IsUnderLegalHold = legalHold
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    [SkippableFact]
    public async Task Sweep_Should_ActuallyRemoveTheRow_NotSoftDeleteIt()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        await SeedArtefactAsync(context, tenantId, Now.AddDays(-1), legalHold: false);

        var summary = await CreateSweeper(context, tenantId).SweepAsync();

        summary.ArtefactsDeleted.Should().Be(1);

        // Seeing PAST the soft-delete filter is the assertion that matters: a marked row is
        // invisible to an ordinary query while still sitting in the table, which is precisely the
        // defect this test exists to catch — and why it cannot live on InMemory.
        //
        // The explicit TenantId predicate is required because IncludeSoftDeleted() is implemented
        // with IgnoreQueryFilters(), which drops EVERY filter including tenant scoping. Without it
        // this counts other tests' rows in the shared per-class database.
        (await context.SafetyArtefacts.IncludeSoftDeleted()
            .CountAsync(a => a.TenantId == tenantId))
            .Should().Be(0, "blocked content about a child must be REMOVED, not flagged");
    }

    [SkippableFact]
    public async Task Sweep_Should_SkipAnArtefactUnderLegalHold()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        await SeedArtefactAsync(context, tenantId, Now.AddDays(-1), legalHold: true);

        var summary = await CreateSweeper(context, tenantId).SweepAsync();

        summary.ArtefactsDeleted.Should().Be(0);
        summary.ArtefactsHeld.Should().Be(1, "the skip is counted rather than silent");
        (await context.SafetyArtefacts.IncludeSoftDeleted()
            .CountAsync(a => a.TenantId == tenantId)).Should().Be(1,
            "still present, and present as a real row rather than a flagged one");
    }

    [SkippableFact]
    public async Task Sweep_Should_NotTouchAnUnexpiredArtefact()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        await SeedArtefactAsync(context, tenantId, Now.AddDays(1), legalHold: false);

        (await CreateSweeper(context, tenantId).SweepAsync()).ArtefactsDeleted.Should().Be(0);
        (await context.SafetyArtefacts.CountAsync()).Should().Be(1);
    }

    [SkippableFact]
    public async Task Sweep_Should_BeIdempotent()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        await SeedArtefactAsync(context, tenantId, Now.AddDays(-1), legalHold: false);
        await SeedDecisionAsync(context, tenantId, Now.AddDays(-1));

        var sweeper = CreateSweeper(context, tenantId);
        await sweeper.SweepAsync();
        var second = await sweeper.SweepAsync();

        // This is the test that first exposed the soft-delete defect: a marked row is still matched
        // by the sweep predicate, so every subsequent run "deleted" it again.
        second.ArtefactsDeleted.Should().Be(0, "a re-run must find nothing left to do");
        second.DecisionsAnonymised.Should().Be(0);
    }

    [SkippableFact]
    public async Task Sweep_Should_AnonymiseExpiredDecisions_RatherThanDeleteThem()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var subject = Guid.NewGuid();
        await SeedDecisionAsync(context, tenantId, Now.AddDays(-1), subject);

        var summary = await CreateSweeper(context, tenantId).SweepAsync();

        summary.DecisionsAnonymised.Should().Be(1);

        // The aggregate is what the §10.3 evaluation needs; the subject link is what minimisation
        // says to drop. Keeping the verdict without the child is the resolution, not a compromise —
        // so unlike an artefact, the ROW survives.
        var decision = await context.SafetyDecisions.SingleAsync();
        decision.SubjectPartyId.Should().Be(Guid.Empty);
        decision.AnonymisedAt.Should().NotBeNull();
        decision.Outcome.Should().Be(nameof(SafetyDecisionOutcome.Blocked), "the verdict survives");
    }

    [SkippableFact]
    public async Task Sweep_Should_NotAnonymiseADecisionUnderLegalHold()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var subject = Guid.NewGuid();
        var decisionId = await SeedDecisionAsync(context, tenantId, Now.AddDays(-1), subject);
        await SeedIncidentAsync(context, tenantId, decisionId, SafetyCategories.Csam, legalHold: true);

        await CreateSweeper(context, tenantId).SweepAsync();

        (await context.SafetyDecisions.SingleAsync()).SubjectPartyId.Should().Be(subject,
            "a hold overrides anonymisation as well as deletion — evidence must stay intact");
    }

    [SkippableFact]
    public async Task Sweep_Should_DeleteExpiredIncidents_ButNotHeldOnes()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var a = await SeedDecisionAsync(context, tenantId, Now.AddYears(1));
        var b = await SeedDecisionAsync(context, tenantId, Now.AddYears(1));
        await SeedIncidentAsync(context, tenantId, a, SafetyCategories.GraphicViolence, false, Now.AddDays(-500));
        await SeedIncidentAsync(context, tenantId, b, SafetyCategories.Csam, true, Now.AddDays(-500));

        var summary = await CreateSweeper(context, tenantId).SweepAsync();

        summary.IncidentsDeleted.Should().Be(1);

        var remaining = await context.SafetyIncidents.IncludeSoftDeleted()
            .Where(i => i.TenantId == tenantId)
            .ToListAsync();
        remaining.Should().ContainSingle()
            .Which.Category.Should().Be(SafetyCategories.Csam,
                "the reportable incident is preserved and the other is genuinely gone");
    }

    [SkippableFact]
    public async Task FindTenantsWithWork_Should_SeeAcrossTenants()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        await SeedArtefactAsync(context, tenantId, Now.AddDays(-1), legalHold: false);

        (await CreateSweeper(context, tenantId).FindTenantsWithWorkAsync()).Should().Contain(tenantId);
    }

    [SkippableFact]
    public async Task FindTenantsWithWork_Should_NotSeeATenantWithNothingExpired()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        await SeedArtefactAsync(context, tenantId, Now.AddDays(30), legalHold: false);
        await SeedDecisionAsync(context, tenantId, Now.AddDays(30));

        (await CreateSweeper(context, tenantId).FindTenantsWithWorkAsync())
            .Should().NotContain(tenantId);
    }

    private void RequireSqlServer()
        => Skip.IfNot(_db.IsAvailable, _db.SkipReason ?? "SQL Server LocalDB unavailable.");
}
