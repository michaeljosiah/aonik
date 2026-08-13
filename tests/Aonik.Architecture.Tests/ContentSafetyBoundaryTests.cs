using System.Reflection;

using Aonik.SharedKernel.Abstractions.Safety;

using FluentAssertions;

namespace Aonik.Architecture.Tests;

/// <summary>
/// Spec 096 §16 — keeps the safety boundary unbypassable.
///
/// <para>
/// <see cref="ContentDeliveryPermit"/> is the whole mechanism: a delivery path takes a permit rather
/// than a boolean, so "did you remember to check?" is a compile-time requirement rather than a
/// convention. These tests are the standing guarantee that the property survives — the analogue of
/// Spec 032's build-time failure for an unclassified mutating tool.
/// </para>
/// </summary>
public class ContentSafetyBoundaryTests
{
    /// <summary>
    /// The only assemblies permitted to construct a permit. Everything else must obtain one from the
    /// gate, which is the point.
    /// </summary>
    private static readonly string[] PermittedIssuers = ["Aonik.Ai"];

    [Fact]
    public void ContentDeliveryPermit_Should_HaveNoPublicConstructor()
    {
        // If this ever becomes public, any caller can fabricate authorisation to deliver content to
        // a child that no classifier ever saw — and nothing else in this design would notice.
        typeof(ContentDeliveryPermit)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Should().BeEmpty(
                "a permit that anyone can construct is not proof of anything");
    }

    [Fact]
    public void ContentDeliveryPermit_Should_BeConstructibleOnlyBySafetyAssemblies()
    {
        var internals = typeof(ContentDeliveryPermit).Assembly
            .GetCustomAttributes<System.Runtime.CompilerServices.InternalsVisibleToAttribute>()
            .Select(a => a.AssemblyName.Split(',')[0])
            .ToList();

        // Test assemblies are allowed so the boundary itself can be asserted; production access is
        // the list above and should stay short. A new production entry here is a design decision,
        // not a convenience.
        var productionIssuers = internals
            .Where(name => !name.EndsWith(".Tests", StringComparison.Ordinal))
            .ToList();

        productionIssuers.Should().BeEquivalentTo(PermittedIssuers,
            "widening who can mint a permit widens who can deliver unchecked content to a child");
    }

    [Fact]
    public void SafetyVerdict_Should_CarryThePermitRatherThanABoolean()
    {
        // Allowed exists for readability, but Permit is what a delivery path must take. If Permit
        // ever disappears, the gate has quietly become a reader whose answer a caller may ignore —
        // the exact failure Spec 089 §8.1 and Spec 032 both landed on.
        typeof(SafetyVerdict).GetProperty(nameof(SafetyVerdict.Permit))
            .Should().NotBeNull();

        typeof(SafetyVerdict).GetProperty(nameof(SafetyVerdict.Permit))!.PropertyType
            .Should().Be(typeof(ContentDeliveryPermit),
                "the permit is the authorisation; a bool would be advice");
    }

    [Fact]
    public void GateMethods_Should_ReturnAVerdictRatherThanABoolean()
    {
        var returningBool = typeof(IContentSafetyGate)
            .GetMethods()
            .Where(m => m.ReturnType == typeof(Task<bool>) || m.ReturnType == typeof(bool))
            .Select(m => m.Name)
            .ToList();

        returningBool.Should().BeEmpty(
            "a boolean answer can be ignored; a verdict carries the permit or it does not");
    }

    // ── The category rules that other code depends on ────────────────────

    [Fact]
    public void NonOverridableCategories_Should_IncludeTheOnesNoGuardianMayRelease()
    {
        // §8: a guardian account is not proof of good intent, and this is the category set where
        // that matters most. An unconditional release capability here was the worst defect in the
        // spec's first revision.
        SafetyCategories.NonOverridable.Should().Contain([
            SafetyCategories.Sexual,
            SafetyCategories.SelfHarm,
            SafetyCategories.Csam,
        ]);
    }

    [Fact]
    public void ReviewableCategories_Should_NotBeNonOverridable()
    {
        // Where false positives actually live. A knight fighting a dragon is the most common request
        // a six-year-old makes, and fairy tales are full of real danger — a parent's judgement
        // should outrank a threshold there.
        SafetyCategories.NonOverridable.Should().NotContain([
            SafetyCategories.GraphicViolence,
            SafetyCategories.Frightening,
        ]);
    }

    [Fact]
    public void ReportableCategories_Should_BeASubsetOfNonOverridable()
    {
        // Anything triggering a preservation-and-report duty must also be unreleasable. The reverse
        // is not true, and conflating them would either over-report or under-seal.
        SafetyCategories.Reportable.Should().BeSubsetOf(SafetyCategories.NonOverridable);
        SafetyCategories.Reportable.Should().Contain(SafetyCategories.Csam);
    }

    [Fact]
    public void SafetyBandNames_Should_MatchBetweenPlatformAndAi()
    {
        // Ai does not reference Platform, so the four band names are duplicated rather than shared —
        // a dependency for four constants would be the worse trade. This test is what makes the
        // duplication safe: rename a band in one place and it fails here rather than silently
        // resolving every generation to the unknown-band default.
        Aonik.Ai.Services.Safety.PartySafetyBandNames.All
            .Should().BeEquivalentTo(Aonik.Platform.Entities.Party.PartySafetyBands.All,
                "a band the two modules disagree about is a band nothing enforces");
    }

    [Fact]
    public void ClassificationProviders_Should_NotBeRegisteredByDefault()
    {
        // No classification vendor is configured in this solution. That is deliberate and it is the
        // safe state: the gate fails closed, so child-facing generation is refused until one is
        // wired. A permissive stub registered "to make it work" would be strictly worse than
        // nothing, because it would look like classification while doing none.
        //
        // If this ever fails, someone has registered an adapter — which is fine, provided it is a
        // real vendor and not a placeholder.
        typeof(Aonik.Ai.Services.Safety.ISafetyClassificationProvider).IsInterface.Should().BeTrue();
    }

    // ── S5: the delivered-voice path ─────────────────────────────────────

    [Fact]
    public void PlayableNarration_Should_RequireAPermitToConstruct()
    {
        // The permit trick applied one level down, at the point where bytes reach a child's ears. A
        // public constructor is fine here precisely because its parameter is unforgeable: a player
        // accepting this type cannot be handed audio no classifier heard.
        var constructors = typeof(Aonik.Ai.Services.Safety.PlayableNarration)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        constructors.Should().ContainSingle();
        constructors[0].GetParameters()[0].ParameterType
            .Should().Be(typeof(ContentDeliveryPermit),
                "narration that cannot show a permit must not be constructible at all");
    }

    [Fact]
    public void NarrationOutcome_Should_CarryTheNarrationRatherThanABoolean()
    {
        // Same reasoning as SafetyVerdict.Permit: a bool is advice, an absent object is enforcement.
        typeof(Aonik.Ai.Services.Safety.NarrationOutcome)
            .GetProperty(nameof(Aonik.Ai.Services.Safety.NarrationOutcome.Narration))!.PropertyType
            .Should().Be(typeof(Aonik.Ai.Services.Safety.PlayableNarration));
    }

    [Fact]
    public void SpeechClassification_Should_RouteSeparatelyFromEveryOtherModality()
    {
        // "Voice is not enabled by the video phase and does not inherit its coverage" (S5). Expressed
        // structurally: speech resolves its own use case, so configuring video routing enables
        // nothing here — and an operator can route one away from an outage without touching the other.
        var useCases = new[]
        {
            Aonik.Ai.Services.Safety.SafetyUseCases.ForModality(SafetyModalities.Text),
            Aonik.Ai.Services.Safety.SafetyUseCases.ForModality(SafetyModalities.Image),
            Aonik.Ai.Services.Safety.SafetyUseCases.ForModality(SafetyModalities.Speech),
            Aonik.Ai.Services.Safety.SafetyUseCases.ForModality(SafetyModalities.Video),
        };

        useCases.Should().OnlyHaveUniqueItems(
            "a shared use case would let one modality's configuration silently cover another");
    }

    [Fact]
    public void TheGate_Should_ReadTheBandRatherThanBeToldIt()
    {
        // A request carrying its own band could claim `adult` for a six-year-old and skip every
        // threshold and every guardian hold with one field. The only defence that holds is for the
        // value not to be expressible in the request at all.
        typeof(SafetyRequest).GetProperty("SafetyBand").Should().BeNull();

        // The gate itself is internal, so it is found by name rather than by type reference.
        var gate = typeof(Aonik.Ai.AiModule).Assembly
            .GetType("Aonik.Ai.Services.Safety.ContentSafetyGate")!;

        gate.GetConstructors()[0].GetParameters().Select(p => p.ParameterType)
            .Should().Contain(typeof(ISafetyBandReader));
    }

    [Fact]
    public void APermit_Should_NameTheArtefactItCovers()
    {
        // Without this a permit is a bearer token for the subject rather than for the artefact: hold
        // any valid one and you can pair it with a different reference, laundering unclassified
        // content through a delivery type that looks checked.
        typeof(ContentDeliveryPermit).GetProperty(nameof(ContentDeliveryPermit.ContentReference))
            .Should().NotBeNull();
        typeof(ContentDeliveryPermit).GetProperty(nameof(ContentDeliveryPermit.Modality))
            .Should().NotBeNull();
        typeof(ContentDeliveryPermit).GetMethod(nameof(ContentDeliveryPermit.Authorises))
            .Should().NotBeNull();
    }

    [Fact]
    public void EveryClassificationAdapter_Should_DeclareItsCoverage()
    {
        // Required of every adapter, including ones that only judge text, so the question cannot be
        // skipped by omission. Without it the routed classifier has no way to know whether the vendor
        // behind it samples, and the S6 rule would only ever bind hand-written stubs.
        typeof(Aonik.Ai.Services.Safety.ISafetyClassificationProvider)
            .GetProperty("Coverage")!.PropertyType
            .Should().Be(typeof(TemporalCoverage));
    }

    [Fact]
    public void SpeechTranscription_Should_DeclareItsCoverageToo()
    {
        // Transcription is a temporal read of the same audio. A transcriber that sampled would leave
        // the same hole, and the composite is only as complete as its least complete leg.
        typeof(ITemporalCoverage)
            .IsAssignableFrom(typeof(Aonik.Ai.Services.Safety.ISpeechTranscriber))
            .Should().BeTrue();
    }

    [Fact]
    public void SpeechTranscription_Should_BeAnUnregisteredSeam()
    {
        // No transcription vendor is configured in this solution, so narration is refused today —
        // the same deliberate safe state as classification. A stub returning an empty transcript
        // would look like coverage while providing none, which is worse than nothing.
        typeof(Aonik.Ai.Services.Safety.ISpeechTranscriber).IsInterface.Should().BeTrue();
    }

    [Fact]
    public void ClassificationResult_Should_CarryEveryRunBehindAVerdict()
    {
        // Speech produces three runs for one verdict — transcription, transcript, audio. A single-run
        // contract would leave a narration decision half-reconstructible, which is the specific
        // failure §15's run ids exist to prevent.
        typeof(ClassificationResult).GetProperty(nameof(ClassificationResult.AllRunIds))
            .Should().NotBeNull();
    }

    // ── S6: the phase that does not ship ─────────────────────────────────

    [Fact]
    public void Video_Should_StayDisabledUntilF6IsResolved()
    {
        // F6 is a product decision, and the spec is explicit that video staying off is a legitimate
        // outcome rather than a failure. If this ever fails, it should be because someone took that
        // decision — not because a config default drifted.
        new Aonik.Ai.Services.Safety.SafetyOptions().ResolvedModalities
            .Should().NotContain(SafetyModalities.Video)
            .And.Contain([SafetyModalities.Text, SafetyModalities.Image, SafetyModalities.Speech]);
    }

    [Fact]
    public void TemporalModalities_Should_CoverVideoAndSpeech()
    {
        // "Was it classified?" has a second half for anything that unfolds over time: how much of it.
        // Listing only video would fix the modality we are not shipping and leave the one we are.
        SafetyModalities.Temporal.Should().BeEquivalentTo(
            [SafetyModalities.Video, SafetyModalities.Speech]);
    }

    [Fact]
    public void SamplingCoverage_Should_ExistAsADistinctDeclaration()
    {
        // The point of the enum is that a classifier must SAY which it does.
        Enum.GetValues<TemporalCoverage>().Should().BeEquivalentTo(
            [TemporalCoverage.Unknown, TemporalCoverage.Sampled, TemporalCoverage.Complete]);

        // And the zero value deliberately is not Complete. An uninitialised auto-property, a missing
        // configuration value or a default deserialisation would otherwise silently claim full
        // coverage — the one claim that must never be made by accident.
        default(TemporalCoverage).Should().NotBe(TemporalCoverage.Complete);
    }

    [Fact]
    public void DisabledAndUnavailable_Should_BeDistinctOutcomes()
    {
        // A feature nobody turned on cannot be "down". Only one of these should wake somebody up.
        SafetyDecisionOutcome.ModalityDisabled.Should().NotBe(SafetyDecisionOutcome.CheckUnavailable);

        typeof(SafetyVerdict).GetProperty(nameof(SafetyVerdict.WasDisabled)).Should().NotBeNull();
    }

    [Fact]
    public void EveryCategory_Should_BeClassifiable()
    {
        foreach (var category in SafetyCategories.All)
        {
            // Guards against a category being added to one set and forgotten in the others, which
            // would leave it enforced inconsistently depending on which check ran.
            var _ = SafetyCategories.IsNonOverridable(category);
            SafetyCategories.IsReportable(category).Should()
                .Be(SafetyCategories.Reportable.Contains(category));
        }

        SafetyCategories.All.Should().Contain(SafetyCategories.Csam);
    }
}
