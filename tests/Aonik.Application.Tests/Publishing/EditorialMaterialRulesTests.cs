using System.Text.Json;

using Aonik.Publishing.Contracts;
using Aonik.Publishing.Services;

using FluentAssertions;

namespace Aonik.Application.Tests.Publishing;

public class EditorialMaterialRulesTests
{
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
