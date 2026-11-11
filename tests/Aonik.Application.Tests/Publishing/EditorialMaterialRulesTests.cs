using System.Text.Json;

using Aonik.Publishing.Contracts;
using Aonik.Publishing.Services;

using FluentAssertions;

namespace Aonik.Application.Tests.Publishing;

public class EditorialMaterialRulesTests
{
    [Fact]
    public void LargeInventory_Should_RequireCoverageOfEveryAssetAcrossBoundedGroups()
    {
        var assets = Enumerable.Range(0, 2048).Select(i => new PackageAsset($"p{i:0000}", $"p{i:0000}.png", "image/png", 4, new string('a', 64))).ToArray();
        var groups = assets.Chunk(1024).Select(group => new PublicationRightsEvidence("Original synthetic artwork", "dev-fixture", group.Select(a => a.Id).ToArray())).ToArray();
        var material = EditorialMaterialRules.Normalize(new("general", "All ages", "comic", "p0000", "Original synthetic fixture", groups));
        var package = new PackageVerificationResult(true, "verified", "reader", new string('b', 64), 4, "world", "edition", new string('c', 64), "comic", null, null, "p0000", assets);
        EditorialMaterialRules.ApprovalBlock(new(true, material), package).Should().BeNull();
        var missing = material! with { RightsEvidence = groups.Take(1).ToArray() };
        EditorialMaterialRules.ApprovalBlock(new(true, missing), package).Should().Be("rights-asset-coverage-required");
    }

    [Theory]
    [InlineData("part", "", true, null)]
    [InlineData("part", "missing", true, "artwork-evidence-required")]
    [InlineData("world", "", true, "artwork-evidence-required")]
    [InlineData("guide", "", true, "artwork-evidence-required")]
    [InlineData("part", "", false, "rights-asset-coverage-required")]
    public async Task Approval_Should_AllowInheritedArtworkOnlyForParts_AndStillRequireExactRights(
        string kind, string artwork, bool coversAssets, string? expectedBlock)
    {
        // Arrange: a verified part with narration and no image assets.
        var path = Path.Combine(AppContext.BaseDirectory, "PublishingFixtures", "part-no-artwork.json");
        var package = JsonSerializer.Deserialize<PackageVerificationResult>(await File.ReadAllTextAsync(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        package = package with { Kind = kind };
        var rights = new PublicationRightsEvidence("Synthetic assets", "fixture-provenance",
            coversAssets ? package.Assets.Select(a => a.Id).ToArray() : []);
        var material = new PublicationEditorialMaterial("general", "All ages", kind, artwork, "Synthetic fixture", [rights]);

        // Act
        var block = EditorialMaterialRules.ApprovalBlock(new(true, material), package);

        // Assert
        block.Should().Be(expectedBlock);
    }
}
