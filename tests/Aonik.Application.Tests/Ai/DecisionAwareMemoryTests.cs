using Aonik.Ai.Contracts.Services;
using Aonik.Ai.Entities;
using Aonik.Ai.Persistence;
using Aonik.Ai.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Events.Integration;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Ai;

/// <summary>
/// Spec 041 — decision-aware learning. Covers RQ1/RQ2 (rationale write + condition-relevance recall),
/// RQ4 (tenant pattern reinforce/supersede/rank/isolation) and RQ5 (outcome extraction from a resolved
/// event reinforces a pattern and writes a rationale).
/// </summary>
public class DecisionAwareMemoryTests
{
    private sealed class TenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;
        public TenantProvider(Guid tenantId) => _tenantId = tenantId;
        public Guid GetCurrentTenantId() => _tenantId;
        public bool TryGetCurrentTenantId(out Guid tenantId) { tenantId = _tenantId; return true; }
    }

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 6, 20, 12, 0, 0, DateTimeKind.Utc);
    }

    private static AiDbContext CreateDbContext(Guid tenantId, string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase(databaseName ?? $"DecisionMemoryTest_{Guid.NewGuid()}")
            .Options;
        return new AiDbContext(options, new TenantProvider(tenantId));
    }

    private static DecisionPatternService Patterns(AiDbContext db, Guid tenantId, TestClock? clock = null)
        => new(db, new TenantProvider(tenantId), clock ?? new TestClock());

    private static DecisionRationaleService Rationales(AiDbContext db, Guid tenantId, TestClock? clock = null)
        => new(new UserMemoryService(db, new TenantProvider(tenantId), clock ?? new TestClock()));

    private static Dictionary<string, string> Conditions(params (string Key, string Value)[] pairs)
        => pairs.ToDictionary(p => p.Key, p => p.Value);

    // ── DecisionPattern (RQ4) ───────────────────────────────────────────

    [Fact]
    public async Task ReinforceAsync_Should_SeedNewPattern_When_NoneExists()
    {
        var tenantId = Guid.NewGuid();
        using var db = CreateDbContext(tenantId);
        var service = Patterns(db, tenantId);

        var view = await service.ReinforceAsync(new ReinforceDecisionPatternRequest(
            "dunning", "smb/low-risk", "Soft reminder at day 3 cleared most invoices."));

        view.ObservationCount.Should().Be(1);
        view.Confidence.Should().Be(0.5m);
        view.Segment.Should().Be("smb/low-risk");
    }

    [Fact]
    public async Task ReinforceAsync_Should_ReinforceInPlace_When_PatternConfirmed()
    {
        var tenantId = Guid.NewGuid();
        using var db = CreateDbContext(tenantId);
        var service = Patterns(db, tenantId);
        var req = new ReinforceDecisionPatternRequest("dunning", null, "Day-3 soft reminder works.");

        await service.ReinforceAsync(req);
        var second = await service.ReinforceAsync(req);

        second.ObservationCount.Should().Be(2);
        second.Confidence.Should().BeGreaterThan(0.5m, "a confirming outcome raises confidence");
        (await db.DecisionPatterns.CountAsync()).Should().Be(1, "the pattern is reinforced in place, not duplicated");
    }

    [Fact]
    public async Task ReinforceAsync_Should_SupersedeAndRestart_When_Contradicted()
    {
        var tenantId = Guid.NewGuid();
        using var db = CreateDbContext(tenantId);
        var service = Patterns(db, tenantId);

        await service.ReinforceAsync(new ReinforceDecisionPatternRequest("dunning", null, "Day-3 reminder works."));
        var reversal = await service.ReinforceAsync(new ReinforceDecisionPatternRequest(
            "dunning", null, "Day-3 reminder no longer clears invoices.", Contradicts: true));

        reversal.Statement.Should().Contain("no longer");
        var current = await db.DecisionPatterns.Where(p => p.SupersededAtUtc == null).ToListAsync();
        current.Should().HaveCount(1, "the contradicted pattern is superseded, the new one is current");
        (await db.DecisionPatterns.CountAsync()).Should().Be(2, "history is preserved (supersede, not delete)");
    }

    [Fact]
    public async Task GetTopPatternsAsync_Should_RankSegmentSpecificAheadOfTenantWide()
    {
        var tenantId = Guid.NewGuid();
        using var db = CreateDbContext(tenantId);
        var service = Patterns(db, tenantId);

        await service.ReinforceAsync(new ReinforceDecisionPatternRequest("dunning", null, "Tenant-wide approach."));
        await service.ReinforceAsync(new ReinforceDecisionPatternRequest("dunning", "smb", "Segment approach."));

        var top = await service.GetTopPatternsAsync("dunning", "smb");

        top.Should().HaveCount(2);
        top[0].Segment.Should().Be("smb", "segment-specific patterns outrank tenant-wide fallbacks");
    }

    [Fact]
    public async Task GetTopPatternsAsync_Should_IsolateTenants()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var shared = $"DecisionPatternIsolation_{Guid.NewGuid()}";

        using var dbA = CreateDbContext(tenantA, shared);
        await Patterns(dbA, tenantA).ReinforceAsync(new ReinforceDecisionPatternRequest("dunning", null, "Tenant A pattern."));

        using var dbB = CreateDbContext(tenantB, shared);
        var top = await Patterns(dbB, tenantB).GetTopPatternsAsync("dunning");

        top.Should().BeEmpty("a tenant never sees another tenant's patterns");
    }

    [Fact]
    public async Task SupersedeAsync_Should_StampAndExclude_Then_ReturnFalseForUnknown()
    {
        var tenantId = Guid.NewGuid();
        using var db = CreateDbContext(tenantId);
        var service = Patterns(db, tenantId);
        var view = await service.ReinforceAsync(new ReinforceDecisionPatternRequest("routing", null, "Cheap corridor."));

        (await service.SupersedeAsync(view.Id)).Should().BeTrue();
        (await service.GetTopPatternsAsync("routing")).Should().BeEmpty();
        (await service.SupersedeAsync(view.Id)).Should().BeFalse("already superseded");
        (await service.SupersedeAsync(Guid.NewGuid())).Should().BeFalse("unknown id");
    }

    // ── Rationale (RQ1 / RQ2) ───────────────────────────────────────────

    [Fact]
    public async Task SaveRationaleAsync_Should_WriteRationaleEntry_With_DecisionKey()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var db = CreateDbContext(tenantId);
        var service = Rationales(db, tenantId);

        await service.SaveRationaleAsync(new SaveRationaleRequest(
            userId, "remittance-routing", "payee.123", "cheaper-slower-corridor",
            Conditions(("payeeVerified", "true"), ("urgency", "low")),
            "payee, corridor, urgency, or amount band changes"));

        var entry = await db.UserMemoryEntries.SingleAsync();
        entry.EntryType.Should().Be(UserMemoryEntryType.Rationale);
        entry.Key.Should().Be("decision.remittance-routing.payee.123");
        entry.ValueJson.Should().Contain("cheaper-slower-corridor");
    }

    [Fact]
    public async Task GetApplicableRationalesAsync_Should_ReturnMatch_When_ConditionsHold()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var db = CreateDbContext(tenantId);
        var service = Rationales(db, tenantId);
        await service.SaveRationaleAsync(new SaveRationaleRequest(
            userId, "remittance-routing", "payee.123", "cheaper-slower-corridor",
            Conditions(("payeeVerified", "true"), ("urgency", "low")), "inputs change"));

        var applicable = await service.GetApplicableRationalesAsync(
            userId, "remittance-routing", Conditions(("payeeVerified", "true"), ("urgency", "low")));

        applicable.Should().ContainSingle();
        applicable[0].Relevance.Should().Be(RationaleRelevance.Match);
        applicable[0].ChosenOption.Should().Be("cheaper-slower-corridor");
    }

    [Fact]
    public async Task SaveRationaleAsync_Should_PromoteDecisionFieldsToColumns()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var db = CreateDbContext(tenantId);
        var service = Rationales(db, tenantId);

        await service.SaveRationaleAsync(new SaveRationaleRequest(
            userId, "remittance-routing", "payee.123", "cheaper-slower-corridor",
            Conditions(("payeeVerified", "true")), "payee or corridor changes"));

        // The rationale fields are now first-class, queryable/indexable columns — not just JSON payload.
        var entry = await db.UserMemoryEntries.SingleAsync();
        entry.DecisionType.Should().Be("remittance-routing");
        entry.StaleWhen.Should().Be("payee or corridor changes");
        entry.ConditionsJson.Should().Contain("payeeVerified");
    }

    [Fact]
    public async Task GetApplicableRationalesAsync_Should_Caveat_When_PartialMatch()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var db = CreateDbContext(tenantId);
        var service = Rationales(db, tenantId);
        await service.SaveRationaleAsync(new SaveRationaleRequest(
            userId, "remittance-routing", "payee.123", "cheaper-slower-corridor",
            Conditions(("payeeVerified", "true"), ("urgency", "low"), ("corridor", "EU-NG")), "inputs change"));

        // Two of three hold; one (corridor) differs → Partial (not a majority conflict).
        var applicable = await service.GetApplicableRationalesAsync(
            userId, "remittance-routing", Conditions(("payeeVerified", "true"), ("urgency", "low"), ("corridor", "UK-NG")));

        applicable.Should().ContainSingle();
        applicable[0].Relevance.Should().Be(RationaleRelevance.Partial);
    }

    [Fact]
    public async Task GetApplicableRationalesAsync_Should_Withhold_When_Mismatch()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var db = CreateDbContext(tenantId);
        var service = Rationales(db, tenantId);
        await service.SaveRationaleAsync(new SaveRationaleRequest(
            userId, "remittance-routing", "payee.123", "cheaper-slower-corridor",
            Conditions(("payeeVerified", "true"), ("urgency", "low")), "inputs change"));

        // Both conditions conflict → majority mismatch → withheld.
        var applicable = await service.GetApplicableRationalesAsync(
            userId, "remittance-routing", Conditions(("payeeVerified", "false"), ("urgency", "high")));

        applicable.Should().BeEmpty("an inapplicable rationale is withheld from recall");
    }

    [Fact]
    public async Task GetApplicableRationalesAsync_Should_ExcludeOtherDecisionTypes()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var db = CreateDbContext(tenantId);
        var service = Rationales(db, tenantId);
        await service.SaveRationaleAsync(new SaveRationaleRequest(
            userId, "budget", "monthly", "envelope-method", Conditions(("income", "stable")), "income changes"));

        var applicable = await service.GetApplicableRationalesAsync(userId, "remittance-routing", Conditions());

        applicable.Should().BeEmpty();
    }

    [Fact]
    public async Task GetApplicableRationalesAsync_Should_Caveat_When_EvenConditionSplit()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var db = CreateDbContext(tenantId);
        var service = Rationales(db, tenantId);
        await service.SaveRationaleAsync(new SaveRationaleRequest(
            userId, "remittance-routing", "payee.123", "cheaper-slower-corridor",
            Conditions(("payeeVerified", "true"), ("urgency", "low")), "inputs change"));

        // 1 of 2 conditions conflicts — an even split is NOT a majority, so the prior is surfaced
        // with a caveat (Partial), not withheld (Mismatch).
        var applicable = await service.GetApplicableRationalesAsync(
            userId, "remittance-routing", Conditions(("payeeVerified", "true"), ("urgency", "high")));

        applicable.Should().ContainSingle();
        applicable[0].Relevance.Should().Be(RationaleRelevance.Partial);
    }

    // ── Outcome extraction (RQ5) ────────────────────────────────────────

    [Fact]
    public async Task ExtractAsync_Should_ReinforcePatternAndWriteRationale_From_ResolvedEvent()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var db = CreateDbContext(tenantId);
        var patterns = Patterns(db, tenantId);
        var rationales = Rationales(db, tenantId);
        var extractor = new DecisionOutcomeExtractionService(patterns, rationales);

        var contextJson = """
        {"statement":"Day-3 soft reminder cleared the invoice.","segment":"smb",
         "subjectGrain":"payee.55","chosenOption":"soft-reminder-day-3",
         "conditions":{"riskTier":"low"},"staleWhen":"risk tier changes"}
        """;
        var resolved = new DecisionResolvedEvent(
            tenantId, "dunning", "Invoice", Guid.NewGuid(), userId, AiRunId: null,
            Outcome: "Paid", Segment: "smb", ContextJson: contextJson);

        await extractor.ExtractAsync(resolved);

        var pattern = await db.DecisionPatterns.SingleAsync();
        pattern.DecisionType.Should().Be("dunning");
        pattern.Statement.Should().Contain("Day-3");

        var applicable = await rationales.GetApplicableRationalesAsync(userId, "dunning", Conditions(("riskTier", "low")));
        applicable.Should().ContainSingle();
        applicable[0].ChosenOption.Should().Be("soft-reminder-day-3");
    }

    [Fact]
    public async Task ExtractAsync_Should_SupersedePattern_When_OutcomeIsNegative()
    {
        var tenantId = Guid.NewGuid();
        using var db = CreateDbContext(tenantId);
        var patterns = Patterns(db, tenantId);
        var extractor = new DecisionOutcomeExtractionService(patterns, Rationales(db, tenantId));

        await patterns.ReinforceAsync(new ReinforceDecisionPatternRequest("dunning", null, "Day-3 reminder works."));

        var resolved = new DecisionResolvedEvent(
            tenantId, "dunning", "Proposal", Guid.NewGuid(), UserId: null, AiRunId: null,
            Outcome: "Failed", Segment: null, ContextJson: """{"statement":"Day-3 reminder failed to clear."}""");
        await extractor.ExtractAsync(resolved);

        var current = await db.DecisionPatterns.Where(p => p.SupersededAtUtc == null).ToListAsync();
        current.Should().ContainSingle();
        current[0].Statement.Should().Contain("failed");
    }
}
