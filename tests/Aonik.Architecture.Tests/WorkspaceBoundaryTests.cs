using System.Reflection;

using FluentAssertions;

namespace Aonik.Architecture.Tests;

/// <summary>
/// Spec 089 acceptance criteria 1 and 2 — the two rules that keep a platform primitive from quietly
/// becoming a product feature.
/// </summary>
public class WorkspaceBoundaryTests
{
    private static Assembly Workspaces => Assembly.Load("Aonik.Workspaces");

    [Fact]
    public void Workspaces_Should_ReferenceOnlySharedKernel()
    {
        var aonikReferences = Workspaces.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(name => name.StartsWith("Aonik.", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        // Acceptance criterion 1, and it is a real constraint rather than tidiness. Platform already
        // references PersonalFinance, while PersonalFinance deliberately references only SharedKernel;
        // a single edge from here into Platform would invert that and create a cycle. Aonik.Ordering
        // and Aonik.Groups sit in the same layer for the same reason.
        aonikReferences.Should().Equal(["Aonik.SharedKernel"]);
    }

    [Theory]
    [InlineData("sheet")]
    [InlineData("canon")]
    [InlineData("production")]
    [InlineData("world")]
    [InlineData("take")]
    [InlineData("arke")]
    public void NoTypeName_Should_NameAProductConcept(string productWord)
    {
        var offenders = Workspaces.GetTypes()
            .Where(t => t.Name.Contains(productWord, StringComparison.OrdinalIgnoreCase))
            .Select(t => t.FullName!)
            .ToList();

        // Acceptance criterion 2. "World" is Arke's word and belongs in Arke's UI (ADR-013); the
        // platform says workspace. Spec 086 paid three review rounds for the opposite choice, when
        // `Household` in platform code led the finance contributor to treat an Arke Kids family as a
        // household and refuse the second one — vocabulary in platform types is not cosmetic, it
        // teaches the next contributor what the thing is for.
        //
        // `world` survives as the VALUE of Kind, which is where product vocabulary is allowed to live.
        offenders.Should().BeEmpty(
            "'{0}' is product vocabulary; the platform names the container, not what it holds", productWord);
    }

    [Fact]
    public void TheReadOnlyProjection_Should_NotExposeFileContents()
    {
        var reader = typeof(SharedKernel.Abstractions.Workspaces.IWorkspaceReader);

        var contentReturning = reader.GetMethods()
            .Where(m => m.Name.Contains("Content", StringComparison.OrdinalIgnoreCase)
                || m.Name.Contains("Read", StringComparison.OrdinalIgnoreCase)
                || m.ReturnType.FullName?.Contains("Stream", StringComparison.Ordinal) == true)
            .Select(m => m.Name)
            .ToList();

        // ADR-016's seam, and the reason Spec 096 can say the storage layer's ignorance of content is
        // preserved: safety runs at the GENERATION boundary, in flight, because by the time bytes reach
        // here they have already been judged. A reader that could open a file would make that claim
        // false and put every other module one call away from inspecting a child's files.
        contentReturning.Should().BeEmpty(
            "workspaces store bytes; nothing outside the module gets to look at them");
    }

    [Fact]
    public void AccessLevel_Should_BeAClosedEnumWhereNoneIsTheDefault()
    {
        var values = Enum.GetValues<SharedKernel.Abstractions.Workspaces.WorkspaceAccessLevel>();

        // Three levels plus None, no extension point, no DSL — the answer to §8.1's P0, where an
        // earlier draft left read/write to TermsJson and therefore left the commit endpoint guarded by
        // nothing but a client that could be modified in five minutes.
        values.Should().HaveCount(4);

        default(SharedKernel.Abstractions.Workspaces.WorkspaceAccessLevel)
            .Should().Be(SharedKernel.Abstractions.Workspaces.WorkspaceAccessLevel.None,
                "an unset access level must not read as permission");
    }
}
