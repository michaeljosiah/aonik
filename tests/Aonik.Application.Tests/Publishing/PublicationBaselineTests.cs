using System.Text.Json.Nodes;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;

using Aonik.Publishing.Services;
using Aonik.Publishing.Testing;

namespace Aonik.Application.Tests.Publishing;

public class PublicationBaselineTests
{
    private const string BaselineHash = "3fdc4558650817334e99f68d3364c9a3a00a2519930eaed016a30dff4d1229b4";

    [Fact]
    public async Task AuditedBaseline_Should_ImportEveryIdentityAndPin_WithoutRewritingBytesOrSerial()
    {
        using var h = new PublicationScenario();
        var source = await ConfigureAsync(h);

        var result = await h.InitialiseAsync();

        result.CommittedSerial.Should().Be(53);
        (await h.HeadAsync()).Should().Be(source);
        var state = await h.Db.PublicationChannels.SingleAsync();
        state.Serial.Should().Be(53);
        state.BaselineSha256.Should().Be(BaselineHash);
        var spaces = await h.Db.Namespaces.ToListAsync();
        spaces.Should().HaveCount(5).And.OnlyContain(x => x.CreatorPartyId == h.CreatorParty);
        var editions = await h.Db.PublishedEditions.ToListAsync();
        editions.Should().HaveCount(13).And.OnlyContain(x => x.IsLegacy && x.CreatorPartyId == h.CreatorParty);
        foreach(var edition in editions)
        {
            var world = PlayerCatalogueProjection.World(PlayerCatalogueProjection.Read(source), edition.WorldId);
            var original = PlayerCatalogueProjection.Pin(world, edition.Slot)!;
            edition.PackageSha256.Should().Be(original["sha256"]!.GetValue<string>());
            var saved = JsonNode.Parse(edition.PinJson)!;
            saved["url"]!.GetValue<string>().Should().Be(original["url"]!.GetValue<string>());
        }
        h.Store.Bytes.Should().BeEmpty("baseline import references existing published objects; it does not copy or re-approve them");
    }

    [Fact]
    public async Task AuditedBaseline_Should_RefuseMissingOwnershipBeforeSavingAnyChannel()
    {
        using var h = new PublicationScenario();
        await ConfigureAsync(h);
        h.Settings.Channels["arke-worlds"].BaselineOwnership.Clear();

        await h.Invoking(x => x.InitialiseAsync()).Should().ThrowAsync<PublishingException>()
            .Where(x => x.Code == "baseline-ownership-required");

        (await h.Db.PublicationChannels.CountAsync()).Should().Be(0);
        (await h.Db.CatalogueSnapshots.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AuditedBaseline_Should_RefuseChangedBytesAndReinitialisation()
    {
        using var h = new PublicationScenario();
        var source = await ConfigureAsync(h);
        h.Settings.Channels["arke-worlds"].BaselineCatalogueJson = source + " ";
        await h.Invoking(x => x.InitialiseAsync()).Should().ThrowAsync<PublishingException>()
            .Where(x => x.Code == "qualified-baseline-required");
        h.Settings.Channels["arke-worlds"].BaselineCatalogueJson = source;
        await h.InitialiseAsync();
        await h.Invoking(x => x.InitialiseAsync()).Should().ThrowAsync<PublishingException>()
            .Where(x => x.Code == "channel-already-initialised");
        (await h.HeadAsync()).Should().Be(source);
    }

    private static async Task<string> ConfigureAsync(PublicationScenario h)
    {
        var source = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "PublishingFixtures", "production-baseline-53.json"));
        PlayerCatalogueProjection.Hash(source).Should().Be(BaselineHash);
        var channel = h.Settings.Channels["arke-worlds"];
        channel.BaselineCatalogueJson = source;
        channel.BaselineSha256 = BaselineHash;
        channel.BaselineEvidenceReference = "isolated-import-test; not production rights clearance";
        foreach(var world in PlayerCatalogueProjection.Read(source)["worlds"]!.AsArray())
            channel.BaselineOwnership[world!["id"]!.GetValue<string>()] = new()
            { CreatorPartyId = h.CreatorParty, EvidenceReference = "synthetic test-only owner mapping" };
        return source;
    }
}
