using System.Text;

using Aonik.SharedKernel.Abstractions.Workspaces;
using Aonik.Workspaces.Services;

using FluentAssertions;

namespace Aonik.Application.Tests.Workspaces;

/// <summary>
/// Spec 089 §6.1.1 and §12 — the request hash that makes a retry safe, and the path normalisation §12 treats as
/// a security property rather than tidiness.
/// </summary>
public class ManifestNormaliserTests
{
    private static ManifestEntry Entry(string path, string hash = "aa", long size = 16)
        => new(path, hash.PadRight(64, '0'), size, "text/plain");

    // ── Paths ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("scenes\\one.md", "scenes/one.md")]
    [InlineData("/scenes/one.md", "scenes/one.md")]
    [InlineData("scenes//one.md", "scenes/one.md")]
    [InlineData("  scenes/one.md  ", "scenes/one.md")]
    public void APath_Should_NormaliseToForwardSlashes(string input, string expected)
        => ManifestNormaliser.NormalisePath(input).Should().Be(expected);

    [Theory]
    [InlineData("../secrets")]
    [InlineData("scenes/../../etc/passwd")]
    [InlineData("./scenes/one.md")]
    public void ARelativeSegment_Should_BeRefusedRatherThanResolved(string path)
    {
        // Resolving server-side would mean accepting a path that was trying to leave the tree and
        // quietly deciding what it meant.
        var act = () => ManifestNormaliser.NormalisePath(path);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TwoUnicodeCompositions_Should_NormaliseToTheSamePath()
    {
        var precomposed = "scènes/one.md";          // è
        var decomposed = "scènes/one.md";           // e + combining grave

        // The same filename to a user, and different strings to a database. A manifest carrying both
        // would present one file twice and let the second silently overwrite the first on any client
        // that normalises.
        ManifestNormaliser.NormalisePath(decomposed)
            .Should().Be(ManifestNormaliser.NormalisePath(precomposed));
    }

    [Fact]
    public void AManifest_Should_RefuseTwoEntriesThatNormaliseToOnePath()
    {
        var act = () => ManifestNormaliser.Normalise(
            [Entry("scenes/one.md"), Entry("scenes\\one.md")]);

        // Duplicates matter after normalisation, not before. Accepting them stores a tree that cannot
        // be materialised on any filesystem, and the second write wins by accident.
        act.Should().Throw<ArgumentException>();
    }

    // ── The request hash ─────────────────────────────────────────────────

    [Fact]
    public void TheSameTree_InADifferentOrder_Should_HashTheSame()
    {
        var workspaceId = Guid.NewGuid();
        var parent = Guid.NewGuid();

        var forwards = ManifestNormaliser.ComputeRequestHash(
            workspaceId, parent, [Entry("a.md", "11"), Entry("b.md", "22")]);
        var backwards = ManifestNormaliser.ComputeRequestHash(
            workspaceId, parent, [Entry("b.md", "22"), Entry("a.md", "11")]);

        // Two clients enumerating the same tree in different orders describe the same tree. Order
        // sensitivity would turn an honest retry into a 409.
        forwards.Should().Be(backwards);
    }

    [Fact]
    public void ADifferentTree_Should_HashDifferently()
    {
        var workspaceId = Guid.NewGuid();
        var parent = Guid.NewGuid();

        var original = ManifestNormaliser.ComputeRequestHash(
            workspaceId, parent, [Entry("a.md", "11")]);
        var edited = ManifestNormaliser.ComputeRequestHash(
            workspaceId, parent, [Entry("a.md", "22")]);

        // This is the whole point of §6.1.1: after a timeout the author may have kept working, and the
        // client correctly reuses its CommitId while rebuilding from a tree that has changed.
        original.Should().NotBe(edited);
    }

    [Fact]
    public void ADifferentParent_Should_HashDifferently()
    {
        var workspaceId = Guid.NewGuid();
        var manifest = new[] { Entry("a.md") };

        ManifestNormaliser.ComputeRequestHash(workspaceId, Guid.NewGuid(), manifest)
            .Should().NotBe(ManifestNormaliser.ComputeRequestHash(workspaceId, Guid.NewGuid(), manifest));
    }

    [Fact]
    public void ARootCommit_Should_HashDifferentlyFromAParentedOne()
    {
        var workspaceId = Guid.NewGuid();
        var manifest = new[] { Entry("a.md") };

        ManifestNormaliser.ComputeRequestHash(workspaceId, null, manifest)
            .Should().NotBe(ManifestNormaliser.ComputeRequestHash(workspaceId, Guid.NewGuid(), manifest));
    }

    [Fact]
    public void ADifferentSize_ForTheSameHash_Should_HashDifferently()
    {
        var workspaceId = Guid.NewGuid();

        // Size is part of the request even though the content hash already determines the bytes: a
        // manifest whose declared size disagrees with its hash is a different request, and quietly
        // treating it as a retry would replay an outcome for a tree the client did not describe.
        ManifestNormaliser.ComputeRequestHash(workspaceId, null, [Entry("a.md", size: 16)])
            .Should().NotBe(
                ManifestNormaliser.ComputeRequestHash(workspaceId, null, [Entry("a.md", size: 32)]));
    }

    [Fact]
    public void TheHash_Should_BeLowercaseHexSha256()
    {
        var hash = ManifestNormaliser.ComputeRequestHash(Guid.NewGuid(), null, [Entry("a.md")]);

        hash.Should().HaveLength(64);
        hash.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void AManifest_Should_LowercaseContentHashes()
    {
        var normalised = ManifestNormaliser.Normalise(
            [new ManifestEntry("a.md", new string('A', 64), 16, null)]);

        // A client that upper-cases its hex would otherwise describe the same tree twice over and get a
        // 409 for a retry it made correctly.
        normalised[0].ContentHash.Should().Be(new string('a', 64));
    }

    [Fact]
    public void TheHash_Should_NotBeAffectedByContentType()
    {
        var workspaceId = Guid.NewGuid();

        var withType = ManifestNormaliser.ComputeRequestHash(
            workspaceId, null, [new ManifestEntry("a.md", new string('a', 64), 16, "text/markdown")]);
        var withoutType = ManifestNormaliser.ComputeRequestHash(
            workspaceId, null, [new ManifestEntry("a.md", new string('a', 64), 16, null)]);

        // Content type is a hint the client may sniff differently between attempts. Including it would
        // make a retry fail on a detail that does not change what was committed.
        withType.Should().Be(withoutType);
    }

    [Fact]
    public void AnEmptyPath_Should_BeRefused()
    {
        var act = () => ManifestNormaliser.NormalisePath("   ");

        act.Should().Throw<ArgumentException>();
    }
}
