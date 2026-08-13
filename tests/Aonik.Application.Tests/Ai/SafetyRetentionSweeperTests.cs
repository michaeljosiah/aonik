using Aonik.Ai.Entities.Safety;
using Aonik.Ai.Persistence;
using Aonik.Ai.Services.Safety;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Safety;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Application.Tests.Ai;

/// <summary>
/// Spec 096 §13. An expiry column deletes nothing — this is the thing that does, and it ships with
/// the gate because artefacts start accumulating the moment blocking works.
/// </summary>
public class SafetyRetentionSweeperTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 8, 13, 3, 30, 0, DateTimeKind.Utc);

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; } = Now;
    }

    private static AiDbContext CreateDbContext()
        => new(
            new DbContextOptionsBuilder<AiDbContext>()
                .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
                .Options,
            new TestTenantProvider(TenantId),
            new TestCurrentUserProvider(Guid.NewGuid()),
            new TestClock());

    private static SafetyRetentionSweeper CreateSweeper(AiDbContext context)
        => new(
            context,
            new TestTenantProvider(TenantId),
            new TestClock(),
            Microsoft.Extensions.Options.Options.Create(new SafetyOptions()),
            NullLogger<SafetyRetentionSweeper>.Instance);

    private static Guid SeedDecision(AiDbContext context, DateTime expiresAt, Guid? subject = null)
    {
        var decision = new SafetyDecision
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
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
        context.SaveChanges();
        return decision.Id;
    }

    private static void SeedIncident(
        AiDbContext context, Guid decisionId, string category, bool legalHold, DateTime? occurredAt = null)
    {
        context.SafetyIncidents.Add(new SafetyIncident
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            SafetyDecisionId = decisionId,
            SubjectPartyId = Guid.NewGuid(),
            Category = category,
            IsNonOverridable = SafetyCategories.IsNonOverridable(category),
            IsUnderLegalHold = legalHold,
            AppealState = SafetyAppealStates.None,
            OccurredAt = occurredAt ?? Now.AddDays(-10)
        });
        context.SaveChanges();
    }

    private static void SeedArtefact(AiDbContext context, DateTime expiresAt, bool legalHold)
    {
        context.SafetyArtefacts.Add(new SafetyArtefact
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            SafetyIncidentId = Guid.NewGuid(),
            Reference = "blob://blocked",
            ExpiresAt = expiresAt,
            IsUnderLegalHold = legalHold
        });
        context.SaveChanges();
    }

    [Fact]
    public async Task Sweep_Should_DeleteExpiredArtefacts()
    {
        await using var context = CreateDbContext();
        SeedArtefact(context, Now.AddDays(-1), legalHold: false);

        var summary = await CreateSweeper(context).SweepAsync();

        summary.ArtefactsDeleted.Should().Be(1);
        (await context.SafetyArtefacts.AnyAsync()).Should().BeFalse(
            "storing the very thing we judged unsafe for a child, indefinitely, would be perverse");
    }

    [Fact]
    public async Task Sweep_Should_SkipAnArtefactUnderLegalHold()
    {
        await using var context = CreateDbContext();
        SeedArtefact(context, Now.AddDays(-1), legalHold: true);

        var summary = await CreateSweeper(context).SweepAsync();

        summary.ArtefactsDeleted.Should().Be(0);
        summary.ArtefactsHeld.Should().Be(1);
        (await context.SafetyArtefacts.CountAsync()).Should().Be(1,
            "preservation overrides retention, and the skip is counted rather than silent");
    }

    [Fact]
    public async Task Sweep_Should_NotTouchAnUnexpiredArtefact()
    {
        await using var context = CreateDbContext();
        SeedArtefact(context, Now.AddDays(1), legalHold: false);

        (await CreateSweeper(context).SweepAsync()).ArtefactsDeleted.Should().Be(0);
    }

    [Fact]
    public async Task Sweep_Should_AnonymiseExpiredDecisions_RatherThanDeleteThem()
    {
        await using var context = CreateDbContext();
        var subject = Guid.NewGuid();
        SeedDecision(context, Now.AddDays(-1), subject);

        var summary = await CreateSweeper(context).SweepAsync();

        summary.DecisionsAnonymised.Should().Be(1);

        // The aggregate is what the §10.3 evaluation needs; the subject link is what minimisation
        // says to drop. Keeping the verdict without the child is the resolution, not a compromise.
        var decision = await context.SafetyDecisions.SingleAsync();
        decision.SubjectPartyId.Should().Be(Guid.Empty);
        decision.AnonymisedAt.Should().NotBeNull();
        decision.Outcome.Should().Be(nameof(SafetyDecisionOutcome.Blocked), "the verdict survives");
    }

    [Fact]
    public async Task Sweep_Should_NotAnonymiseADecisionUnderLegalHold()
    {
        await using var context = CreateDbContext();
        var subject = Guid.NewGuid();
        var decisionId = SeedDecision(context, Now.AddDays(-1), subject);
        SeedIncident(context, decisionId, SafetyCategories.Csam, legalHold: true);

        await CreateSweeper(context).SweepAsync();

        (await context.SafetyDecisions.SingleAsync()).SubjectPartyId.Should().Be(subject,
            "a hold overrides anonymisation as well as deletion — evidence must stay intact");
    }

    [Fact]
    public async Task Sweep_Should_BeIdempotent()
    {
        await using var context = CreateDbContext();
        SeedArtefact(context, Now.AddDays(-1), legalHold: false);
        SeedDecision(context, Now.AddDays(-1));

        var sweeper = CreateSweeper(context);
        await sweeper.SweepAsync();
        var second = await sweeper.SweepAsync();

        second.ArtefactsDeleted.Should().Be(0);
        second.DecisionsAnonymised.Should().Be(0, "a re-run must find nothing left to do");
    }

    [Fact]
    public async Task Sweep_Should_DeleteExpiredIncidents_ButNotHeldOnes()
    {
        await using var context = CreateDbContext();
        var a = SeedDecision(context, Now.AddYears(1));
        var b = SeedDecision(context, Now.AddYears(1));
        SeedIncident(context, a, SafetyCategories.GraphicViolence, legalHold: false, Now.AddDays(-500));
        SeedIncident(context, b, SafetyCategories.Csam, legalHold: true, Now.AddDays(-500));

        var summary = await CreateSweeper(context).SweepAsync();

        summary.IncidentsDeleted.Should().Be(1);
        (await context.SafetyIncidents.SingleAsync()).Category.Should().Be(SafetyCategories.Csam);
    }

    [Fact]
    public async Task FindTenantsWithWork_Should_SeeAcrossTenants()
    {
        await using var context = CreateDbContext();
        SeedArtefact(context, Now.AddDays(-1), legalHold: false);

        // Reads across tenants to know where to go; every WRITE happens inside a per-tenant scope,
        // because EnforceTenantOnWrites rejects a cross-tenant save.
        (await CreateSweeper(context).FindTenantsWithWorkAsync()).Should().Contain(TenantId);
    }

    [Fact]
    public async Task FindTenantsWithWork_Should_BeEmpty_WhenNothingHasExpired()
    {
        await using var context = CreateDbContext();
        SeedArtefact(context, Now.AddDays(30), legalHold: false);
        SeedDecision(context, Now.AddDays(30));

        (await CreateSweeper(context).FindTenantsWithWorkAsync()).Should().BeEmpty();
    }
}
