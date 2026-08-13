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
/// Spec 096 §12 — the path that has to exist before it is needed.
///
/// <para>
/// None of this is expected to run. That is the point: <strong>discovering these obligations during an
/// incident is the worst possible time to learn them</strong>, so the mechanism exists now, unused,
/// rather than being written under pressure by whoever happens to be on call.
/// </para>
/// </summary>
public class SafetyReportingTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 8, 13, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Custodian = Guid.NewGuid();

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

    private static PreservedMaterialService CreateService(
        AiDbContext context, params Guid[] custodians)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new SafetyOptions
        {
            PreservedMaterialCustodians = [.. custodians.Select(c => c.ToString())],
        });

        return new PreservedMaterialService(
            context, new TestTenantProvider(TenantId), new TestClock(), options,
            NullLogger<PreservedMaterialService>.Instance);
    }

    private static SafetyIncidentRecorder CreateRecorder(AiDbContext context)
        => new(
            context,
            Microsoft.Extensions.Options.Options.Create(new SafetyOptions()),
            NullLogger<SafetyIncidentRecorder>.Instance);

    private static async Task<Guid> RecordBlockAsync(AiDbContext context, string category)
    {
        var recorder = CreateRecorder(context);

        var decisionId = await recorder.RecordAsync(new SafetyDecisionRecord(
            TenantId, Guid.NewGuid(), SafetyBandNames.Age6To9, SafetyModalities.Image,
            SafetyLayers.Output, SafetyDecisionOutcome.Blocked, [category], "v1",
            Guid.NewGuid(), [Guid.NewGuid()], Now));

        var decision = await context.SafetyDecisions.AsNoTracking().FirstAsync(d => d.Id == decisionId);
        await recorder.RecordIncidentAsync(
            decisionId, decision.SubjectPartyId, category, "blob://preserved", Now);

        return (await context.SafetyIncidents.AsNoTracking().FirstAsync(i => i.SafetyDecisionId == decisionId)).Id;
    }

    // ── Escalation is immediate ──────────────────────────────────────────

    [Fact]
    public async Task AReportableCategory_Should_EscalateInTheSameCallAsTheIncident()
    {
        await using var context = CreateDbContext();

        await RecordBlockAsync(context, SafetyCategories.Csam);

        // Not a scheduled job and not a notification. An escalation that depends on a scheduler
        // having run is not immediate, and a message that failed to send leaves no trace at all.
        var escalation = await context.SafetyEscalations.SingleAsync();
        escalation.Category.Should().Be(SafetyCategories.Csam);
        escalation.AcknowledgedAt.Should().BeNull("nobody has looked yet, and that must stay visible");
    }

    [Theory]
    [InlineData(SafetyCategories.Sexual)]
    [InlineData(SafetyCategories.SelfHarm)]
    [InlineData(SafetyCategories.GraphicViolence)]
    public async Task ANonReportableCategory_Should_NotEscalate(string category)
    {
        await using var context = CreateDbContext();

        await RecordBlockAsync(context, category);

        // Escalating everything would produce a queue nobody reads, which is indistinguishable from
        // not escalating at all — and the reporting duty attaches to one category, not to severity.
        (await context.SafetyEscalations.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task AReportableCategory_Should_PlaceTheIncidentUnderLegalHold()
    {
        await using var context = CreateDbContext();

        await RecordBlockAsync(context, SafetyCategories.Csam);

        (await context.SafetyIncidents.SingleAsync()).IsUnderLegalHold.Should().BeTrue(
            "preservation is automatic on detection and does not depend on someone remembering");
    }

    // ── Access is restricted, and every attempt is logged ────────────────

    [Fact]
    public async Task ANonCustodian_Should_BeRefused_AndTheAttemptLogged()
    {
        await using var context = CreateDbContext();
        var incidentId = await RecordBlockAsync(context, SafetyCategories.Csam);

        var outcome = await CreateService(context, Custodian)
            .AccessAsync(Guid.NewGuid(), incidentId, "curiosity");

        outcome.Granted.Should().BeFalse();
        outcome.Reference.Should().BeNull();

        // The record most worth having. Somebody reaching for this material and being turned away is
        // exactly what a later review needs to see, and it is invisible if the log is only written
        // once the permission check has passed.
        var log = await context.PreservedMaterialAccesses.SingleAsync();
        log.WasGranted.Should().BeFalse();
        log.DenialReason.Should().Be("Not a named custodian.");
        log.Purpose.Should().Be("curiosity");
    }

    [Fact]
    public async Task ACustodian_Should_BeGranted_AndTheAccessLogged()
    {
        await using var context = CreateDbContext();
        var incidentId = await RecordBlockAsync(context, SafetyCategories.Csam);

        var outcome = await CreateService(context, Custodian)
            .AccessAsync(Custodian, incidentId, "reporting to authority");

        outcome.Granted.Should().BeTrue();
        outcome.Reference.Should().Be("blob://preserved");

        var log = await context.PreservedMaterialAccesses.SingleAsync();
        log.WasGranted.Should().BeTrue();
        log.ActorPartyId.Should().Be(Custodian);
    }

    [Fact]
    public async Task NobodyIsACustodian_ByDefault()
    {
        await using var context = CreateDbContext();
        var incidentId = await RecordBlockAsync(context, SafetyCategories.Csam);

        // F7 has not been resolved, so the material is unreachable rather than reachable by whoever
        // holds an admin claim. That is the safe state, not a lockout to work around.
        var outcome = await CreateService(context).AccessAsync(Custodian, incidentId, "any");

        outcome.Granted.Should().BeFalse();
    }

    [Fact]
    public async Task AGuardianOfTheChild_Should_NotBeACustodianByVirtueOfBeingAGuardian()
    {
        await using var context = CreateDbContext();
        var incidentId = await RecordBlockAsync(context, SafetyCategories.Csam);

        var incident = await context.SafetyIncidents.AsNoTracking().SingleAsync();
        var guardian = Guid.NewGuid();

        // §8 already says a guardian cannot view a non-overridable incident. This is the same rule at
        // the §12 layer: at this category the guardian is not automatically a safe party, and a
        // custodian list derived from relationships would quietly reintroduce the bypass.
        var outcome = await CreateService(context, Custodian)
            .AccessAsync(guardian, incident.Id, "I am the parent");

        outcome.Granted.Should().BeFalse();
    }

    [Fact]
    public async Task AccessToAnUnknownIncident_Should_StillBeLogged()
    {
        await using var context = CreateDbContext();

        await CreateService(context, Custodian).AccessAsync(Custodian, Guid.NewGuid(), "probing");

        // Probing for incident ids is itself worth a record.
        (await context.PreservedMaterialAccesses.SingleAsync()).WasGranted.Should().BeFalse();
    }

    // ── Acknowledgement ──────────────────────────────────────────────────

    [Fact]
    public async Task OnlyACustodian_Should_SeeOrAcknowledgeAnEscalation()
    {
        await using var context = CreateDbContext();
        await RecordBlockAsync(context, SafetyCategories.Csam);
        var service = CreateService(context, Custodian);

        (await service.ListOpenEscalationsAsync(Guid.NewGuid())).Should().BeEmpty();

        var open = await service.ListOpenEscalationsAsync(Custodian);
        open.Should().ContainSingle();

        (await service.AcknowledgeAsync(Guid.NewGuid(), open[0].EscalationId, "not me"))
            .Should().BeFalse();

        (await service.AcknowledgeAsync(Custodian, open[0].EscalationId, "reported"))
            .Should().BeTrue();

        (await service.ListOpenEscalationsAsync(Custodian)).Should().BeEmpty();
    }

    [Fact]
    public async Task AnAcknowledgedEscalation_Should_RecordWhoAndWhen()
    {
        await using var context = CreateDbContext();
        await RecordBlockAsync(context, SafetyCategories.Csam);
        var service = CreateService(context, Custodian);
        var open = await service.ListOpenEscalationsAsync(Custodian);

        await service.AcknowledgeAsync(Custodian, open[0].EscalationId, "reported to authority");

        var escalation = await context.SafetyEscalations.AsNoTracking().SingleAsync();
        escalation.AcknowledgedByPartyId.Should().Be(Custodian);
        escalation.AcknowledgedAt.Should().Be(Now);
        escalation.Notes.Should().Be("reported to authority");
    }

    // ── Deletion cannot destroy evidence ─────────────────────────────────

    [Fact]
    public async Task ALegalHold_Should_BeVisibleToAnyFutureErasurePath()
    {
        await using var context = CreateDbContext();
        await RecordBlockAsync(context, SafetyCategories.Csam);
        var incident = await context.SafetyIncidents.AsNoTracking().SingleAsync();

        // No subject-access erasure path exists in this codebase today. This contract exists so the
        // one somebody writes later cannot be written without encountering it — a deletion that
        // destroys evidence is not a privacy right being exercised.
        ILegalHoldReader reader = CreateService(context);

        (await reader.HasLegalHoldAsync(incident.SubjectPartyId)).Should().BeTrue();
        (await reader.HasLegalHoldAsync(Guid.NewGuid())).Should().BeFalse();
    }

    [Fact]
    public async Task AnOrdinaryBlock_Should_NotPlaceAnyoneUnderLegalHold()
    {
        await using var context = CreateDbContext();
        await RecordBlockAsync(context, SafetyCategories.GraphicViolence);
        var incident = await context.SafetyIncidents.AsNoTracking().SingleAsync();

        // The hold overrides a deletion request, so applying it to a blocked dragon fight would mean
        // a family cannot erase their child's ordinary data. The narrowness is the point.
        (await CreateService(context).HasLegalHoldAsync(incident.SubjectPartyId)).Should().BeFalse();
    }
}
