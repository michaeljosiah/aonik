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
