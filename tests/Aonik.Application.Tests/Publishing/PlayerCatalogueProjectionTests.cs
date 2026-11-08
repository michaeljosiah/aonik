using System.Text.Json;
using System.Text.Json.Nodes;

using FluentAssertions;

using Aonik.Publishing.Contracts;
using Aonik.Publishing.Entities;
using Aonik.Publishing.Services;

namespace Aonik.Application.Tests.Publishing;

public class PlayerCatalogueProjectionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Part_Should_UpdateOnlyItsProductionSchedule_WhenVerifiedScheduleIsPresent(bool hasSchedule)
    {
        // Arrange: the verified part targets an existing listening production.
        var path = Path.Combine(AppContext.BaseDirectory, "PublishingFixtures", "part.json");
        var package = JsonSerializer.Deserialize<PackageVerificationResult>(await File.ReadAllTextAsync(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var manifest = JsonNode.Parse(package.Presentation!.Manifest.GetRawText())!.AsObject();
        var schedule = new JsonObject { ["day"] = "saturday", ["next"] = "2026-11-14" };
        if (hasSchedule) manifest["part"]!["schedule"] = schedule.DeepClone();
        package = package with { Presentation = package.Presentation with { Manifest = JsonSerializer.SerializeToElement(manifest) } };
        var root = JsonNode.Parse("""
            {"version":2,"serial":10,"worlds":[{"id":"lantern-garden","name":"Lantern garden",
            "package":{"url":"https://example.test/base.zip","byteLength":1,"sha256":"HASH","fingerprint":"BASE","parts":[]},
            "productions":[{"id":"story","title":"Story","kind":"Audiobook","schedule":{"day":"friday"}},
            {"id":"another","title":"Another","kind":"Book","schedule":{"day":"monday"}}]}]}
            """.Replace("HASH", new string('a', 64)).Replace("BASE", package.BaseFingerprint))!.AsObject();
        var material = new ApprovedPublicationMaterial(new PublicationSubmission(),
            new SubmissionRevision { WorldId = package.WorldId }, new EditorialDecision(), package,
            new PublicationEditorialMaterial("children", "7+", "part", package.CoverAssetId!, "Synthetic fixture", []));
        PublicationArtifact[] artifacts =
        [
            new() { AssetId = "", Url = "https://example.test/part.zip", Bytes = package.ArchiveBytes, Sha256 = package.ArchiveSha256 },
            new() { AssetId = package.CoverAssetId!, Url = "https://example.test/cover.png", Bytes = 1, Sha256 = new string('b', 64) }
        ];

        // Act
        var projected = PlayerCatalogueProjection.Apply(root, material, artifacts,
            new PublicationOperation { CreatedAt = new DateTime(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc) }, 11);
        var world = JsonNode.Parse(projected.Json)!["worlds"]![0]!;

        // Assert: an omitted optional schedule preserves the last declared schedule.
        world["productions"]![0]!["schedule"]!["day"]!.GetValue<string>().Should().Be(hasSchedule ? "saturday" : "friday");
        var nextRelease = world["productions"]![0]!["schedule"]?["next"]?.GetValue<string>();
        nextRelease.Should().Be(hasSchedule ? "2026-11-14" : null);
        world["productions"]![1]!["schedule"]!["day"]!.GetValue<string>().Should().Be("monday");
        world["package"]!["fingerprint"]!.GetValue<string>().Should().Be(package.BaseFingerprint);
        world["package"]!["parts"]!.AsArray().Should().ContainSingle();
    }
}
