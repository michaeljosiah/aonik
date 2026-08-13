using Aonik.Ai.Entities.Safety;
using Aonik.Ai.Persistence;
using Aonik.Ai.Services.Safety;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Safety;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Ai;

/// <summary>
/// Spec 096 §7 — L1, the strongest lever.
///
/// <para>
/// Narrowing what can be generated beats filtering what was: a filter is an adversarial contest
/// against a model's whole output distribution, run on every request forever, while a constrained
/// request space is a design decision made once. These tests are mostly about the youngest band,
/// because <strong>free-text prompting by young children is the riskiest feature in the product</strong>
/// and the decision taken here is that they do not get it.
/// </para>
/// </summary>
public class RequestConstraintTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; } = new(2026, 8, 13, 12, 0, 0, DateTimeKind.Utc);
    }

    private static AiDbContext CreateDbContext()
        => new(
            new DbContextOptionsBuilder<AiDbContext>()
                .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
                .Options,
            new TestTenantProvider(TenantId),
            new TestCurrentUserProvider(Guid.NewGuid()),
            new TestClock());

    private static BandRequestConstraint CreateConstraint(AiDbContext context)
        => new(context, new TestTenantProvider(TenantId));

    private static void SeedCharacter(AiDbContext context, string key, string minimumBand)
    {
        context.CuratedCharacters.Add(new CuratedCharacter
        {
            Id = Guid.NewGuid(), TenantId = TenantId, CharacterKey = key,
            DisplayName = key, MinimumSafetyBand = minimumBand, IsActive = true
        });
        context.SaveChanges();
    }

    private static void SeedTemplate(AiDbContext context, string key, string minimumBand)
    {
        context.StoryTemplates.Add(new StoryTemplate
        {
            Id = Guid.NewGuid(), TenantId = TenantId, TemplateKey = key,
            DisplayName = key, Frame = "a journey to find the lost {thing}",
            MinimumSafetyBand = minimumBand, IsActive = true
        });
        context.SaveChanges();
    }

    // ── The under-6 rule ─────────────────────────────────────────────────

    [Fact]
    public async Task Under6_Should_NotBeAbleToSubmitFreeText()
    {
        await using var context = CreateDbContext();
        SeedTemplate(context, "quest", SafetyBandNames.Under6);
        SeedCharacter(context, "bramble", SafetyBandNames.Under6);

        var verdict = await CreateConstraint(context).EvaluateAsync(new ConstrainedRequest(
            Guid.NewGuid(), SafetyBandNames.Under6,
            FreeText: "a scary monster", TemplateId: "quest", CharacterIds: ["bramble"]));

        // Enforced SERVER-side. A UI that merely does not show a prompt box is presentation: the
        // request arrives over HTTP and a modified client can put anything in it.
        verdict.Allowed.Should().BeFalse();
        verdict.Reason.Should().Contain("free text");
    }

    [Fact]
    public async Task Under6_Should_BeAllowed_WithCuratedChoicesOnly()
    {
        await using var context = CreateDbContext();
        SeedTemplate(context, "quest", SafetyBandNames.Under6);
        SeedCharacter(context, "bramble", SafetyBandNames.Under6);

        var verdict = await CreateConstraint(context).EvaluateAsync(new ConstrainedRequest(
            Guid.NewGuid(), SafetyBandNames.Under6,
            TemplateId: "quest", CharacterIds: ["bramble"]));

        verdict.Allowed.Should().BeTrue(
            "a curated experience is the point, not a consolation");
    }

    [Fact]
    public async Task Under6_Should_RequireATemplate()
    {
        await using var context = CreateDbContext();
        SeedCharacter(context, "bramble", SafetyBandNames.Under6);

        var verdict = await CreateConstraint(context).EvaluateAsync(new ConstrainedRequest(
            Guid.NewGuid(), SafetyBandNames.Under6, CharacterIds: ["bramble"]));

        verdict.Allowed.Should().BeFalse();
        verdict.Reason.Should().Contain("template");
    }

    // ── Bounded free text ────────────────────────────────────────────────

    [Fact]
    public async Task Age6To9_Should_AllowShortFreeText_WithinATemplate()
    {
        await using var context = CreateDbContext();
        SeedTemplate(context, "quest", SafetyBandNames.Age6To9);
        SeedCharacter(context, "bramble", SafetyBandNames.Age6To9);

        var verdict = await CreateConstraint(context).EvaluateAsync(new ConstrainedRequest(
            Guid.NewGuid(), SafetyBandNames.Age6To9,
            FreeText: "a golden key", TemplateId: "quest", CharacterIds: ["bramble"]));

        verdict.Allowed.Should().BeTrue("structure supplied, the child supplies the variables");
    }

    [Fact]
    public async Task Age6To9_Should_RefuseFreeTextBeyondItsBound()
    {
        await using var context = CreateDbContext();
        SeedTemplate(context, "quest", SafetyBandNames.Age6To9);
        SeedCharacter(context, "bramble", SafetyBandNames.Age6To9);

        var verdict = await CreateConstraint(context).EvaluateAsync(new ConstrainedRequest(
            Guid.NewGuid(), SafetyBandNames.Age6To9,
            FreeText: new string('x', 200), TemplateId: "quest", CharacterIds: ["bramble"]));

        // Crude and real: a long prompt is where instructions get buried, and a bounded one is far
        // cheaper to classify well.
        verdict.Allowed.Should().BeFalse();
        verdict.Reason.Should().Contain("exceeds");
    }

    [Fact]
    public async Task Age10To12_Should_AllowOpenFreeText()
    {
        await using var context = CreateDbContext();

        var verdict = await CreateConstraint(context).EvaluateAsync(new ConstrainedRequest(
            Guid.NewGuid(), SafetyBandNames.Age10To12, FreeText: "a knight fights a dragon"));

        verdict.Allowed.Should().BeTrue(
            "the constraint must be narrow, or ordinary storytelling breaks");
    }

    // ── Approval flows upward only ───────────────────────────────────────

    [Fact]
    public async Task AnOlderBand_Should_UseContentApprovedForAYoungerOne()
    {
        await using var context = CreateDbContext();
        SeedTemplate(context, "quest", SafetyBandNames.Under6);
        SeedCharacter(context, "bramble", SafetyBandNames.Under6);

        var verdict = await CreateConstraint(context).EvaluateAsync(new ConstrainedRequest(
            Guid.NewGuid(), SafetyBandNames.Age6To9,
            TemplateId: "quest", CharacterIds: ["bramble"]));

        verdict.Allowed.Should().BeTrue("something fine for a six-year-old is fine for a nine-year-old");
    }

    [Fact]
    public async Task AYoungerBand_Should_NotUseContentApprovedForAnOlderOne()
    {
        await using var context = CreateDbContext();
        SeedTemplate(context, "quest", SafetyBandNames.Under6);
        SeedCharacter(context, "shadow", SafetyBandNames.Age10To12);

        var verdict = await CreateConstraint(context).EvaluateAsync(new ConstrainedRequest(
            Guid.NewGuid(), SafetyBandNames.Under6,
            TemplateId: "quest", CharacterIds: ["shadow"]));

        verdict.Allowed.Should().BeFalse("approval flows upward only; the reverse is the mistake");
    }

    [Fact]
    public async Task OneUnapprovedCharacter_Should_RefuseTheWholeRequest()
    {
        await using var context = CreateDbContext();
        SeedTemplate(context, "quest", SafetyBandNames.Under6);
        SeedCharacter(context, "bramble", SafetyBandNames.Under6);
        SeedCharacter(context, "pip", SafetyBandNames.Under6);

        var verdict = await CreateConstraint(context).EvaluateAsync(new ConstrainedRequest(
            Guid.NewGuid(), SafetyBandNames.Under6,
            TemplateId: "quest", CharacterIds: ["bramble", "pip", "not-a-character"]));

        // A request naming nine approved characters and one unapproved one is an unapproved
        // request. A count check would have passed it.
        verdict.Allowed.Should().BeFalse();
        verdict.Reason.Should().Contain("not-a-character");
    }

    [Fact]
    public async Task AnInactiveCharacter_Should_NotBeUsable()
    {
        await using var context = CreateDbContext();
        SeedTemplate(context, "quest", SafetyBandNames.Under6);
        context.CuratedCharacters.Add(new CuratedCharacter
        {
            Id = Guid.NewGuid(), TenantId = TenantId, CharacterKey = "retired",
            DisplayName = "retired", MinimumSafetyBand = SafetyBandNames.Under6, IsActive = false
        });
        await context.SaveChangesAsync();

        var verdict = await CreateConstraint(context).EvaluateAsync(new ConstrainedRequest(
            Guid.NewGuid(), SafetyBandNames.Under6,
            TemplateId: "quest", CharacterIds: ["retired"]));

        verdict.Allowed.Should().BeFalse("withdrawing a character must take effect immediately");
    }

    // ── Unknown band ─────────────────────────────────────────────────────

    [Fact]
    public async Task AnUnknownBand_Should_GetTheStrictestConstraints()
    {
        await using var context = CreateDbContext();

        var verdict = await CreateConstraint(context).EvaluateAsync(new ConstrainedRequest(
            Guid.NewGuid(), "toddler", FreeText: "anything"));

        verdict.Allowed.Should().BeFalse(
            "a band we cannot establish is treated as the youngest, not as the oldest");
    }

    [Theory]
    [InlineData(SafetyBandNames.Under6, false)]
    [InlineData(SafetyBandNames.Age6To9, true)]
    [InlineData(SafetyBandNames.Age10To12, true)]
    [InlineData(SafetyBandNames.Age13ToMajority, true)]
    [InlineData("unknown", false)]
    public void BandConstraints_Should_OnlyAllowFreeTextFromSixUpward(string band, bool allowsFreeText)
    {
        BandConstraints.For(band).AllowsFreeText.Should().Be(allowsFreeText);
    }
}
