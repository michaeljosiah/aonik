using System.Text.Json.Nodes;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Aonik.Publishing.Contracts;
using Aonik.Publishing.Entities;
using Aonik.Publishing.Services;
using Aonik.Publishing.Testing;

namespace Aonik.Application.Tests.Publishing;

public class PublicationDeliveryTests
{
    [Fact]
    public async Task PartWithoutArtwork_Should_KeepApprovedBaseCover_AndDeliverOnlyItsArchive()
    {
        using var h = new PublicationScenario();
        await h.InitialiseAsync();
        var baseReceipt = await h.ApproveAsync("listening");
        var baseOperation = await h.Service.PublishAsync(baseReceipt.Id, await h.RequestAsync(baseReceipt.Id));
        await h.DeliverAsync(baseOperation.Id);
        var before = JsonNode.Parse(await h.HeadAsync())!;
        var artwork = before["worlds"]![0]!["pins"]!["keyArt"]!.ToJsonString();
        var count = h.Store.Bytes.Count;
        var part = await h.ApproveAsync("part-no-artwork");

        var operation = await h.Service.PublishAsync(part.Id, await h.RequestAsync(part.Id));
        await h.DeliverAsync(operation.Id);

        var world = JsonNode.Parse(await h.HeadAsync())!["worlds"]![0]!;
        world["pins"]!["keyArt"]!.ToJsonString().Should().Be(artwork);
        world["package"]!["parts"]!.AsArray().Should().ContainSingle();
        h.Store.Bytes.Count.Should().Be(count + 1);
        (await h.Db.PublicationArtifacts.Where(a => a.OperationId == operation.Id).ToListAsync())
            .Should().ContainSingle(a => a.AssetId == "" && a.PublicVerifiedAt != null);
        await h.ExportAsync("parts-no-artwork");
    }

    [Theory]
    [InlineData(181)]
    [InlineData(200)]
    public async Task PublicTitle_Should_SupportTheFullDeclaredTitleLimit(int length)
    {
        using var h=new PublicationScenario();await h.InitialiseAsync();var title=new string('A',length);var receipt=await h.ApproveAsync("world",title);
        var queued=await h.Service.PublishAsync(receipt.Id,await h.RequestAsync(receipt.Id));await h.DeliverAsync(queued.Id);
        JsonNode.Parse(await h.HeadAsync())!["worlds"]![0]!["name"]!.GetValue<string>().Should().Be(title);
    }
    [Theory]
    [InlineData("world","package")]
    [InlineData("listening","package")]
    [InlineData("guide","guide")]
    [InlineData("music","package")]
    [InlineData("story","story")]
    [InlineData("comic","comic")]
    public async Task Release_Should_ExposeOnlyExactVerifiedPins_AfterSuccessfulPublicReadback(string kind,string key)
    {
        using var h=new PublicationScenario();await h.InitialiseAsync();var receipt=await h.ApproveAsync(kind);var request=await h.RequestAsync(receipt.Id);
        var queued=await h.Service.PublishAsync(receipt.Id,request);(await h.HeadAsync()).Should().NotContain(receipt.Revision.WorldId);
        await h.DeliverAsync(queued.Id);var root=JsonNode.Parse(await h.HeadAsync())!;
        root["serial"]!.GetValue<int>().Should().Be(53);var world=root["worlds"]![0]!;
        var pin=world[key]??world["productions"]![0]![key];pin.Should().NotBeNull();pin!["sha256"]!.GetValue<string>().Should().Be(receipt.Revision.DeclaredPackageSha256);
        if(kind is "story" or "comic"){world["productions"]![0]!["package"].Should().BeNull();pin["version"]!.GetValue<int>().Should().BeGreaterThan(0);}
        var artifacts=await h.Db.PublicationArtifacts.Where(a=>a.TenantId==h.Tenant).ToListAsync();artifacts.Should().OnlyContain(a=>a.ReadyAt!=null&&a.PublicVerifiedAt!=null);
        h.Store.Verifications.Should().Be(artifacts.Count);h.Store.Bytes.Values.Should().Contain(b=>b.LongLength==receipt.Revision.DeclaredPackageBytes);
        await h.DeliverAsync(queued.Id);(await h.Service.PublishAsync(receipt.Id,request)).Id.Should().Be(queued.Id);
        (await h.Db.PublicationChannels.SingleAsync()).Serial.Should().Be(53);(await h.Db.PublishedEditions.CountAsync()).Should().Be(1);
        await h.ExportAsync(kind);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LostCopyOrReadback_Should_KeepHeadUnchanged_ThenReconcileSameArtifacts(bool copyLost)
    {
        using var h=new PublicationScenario();await h.InitialiseAsync();var receipt=await h.ApproveAsync();var op=await h.Service.PublishAsync(receipt.Id,await h.RequestAsync(receipt.Id));
        h.Store.FailAfterWrite=copyLost;h.Store.FailReadback=!copyLost;
        await h.Invoking(x=>x.DeliverAsync(op.Id)).Should().ThrowAsync<IOException>();(await h.HeadAsync()).Should().Contain("\"serial\":52");
        var ids=h.Store.Bytes.Keys.ToArray();h.Store.FailAfterWrite=false;h.Store.FailReadback=false;await h.DeliverAsync(op.Id);
        h.Store.Bytes.Keys.Should().Contain(ids);(await h.Db.PublicationChannels.SingleAsync()).Serial.Should().Be(53);
    }
    [Theory]
    [InlineData("hold")]
    [InlineData("permission")]
    [InlineData("policy")]
    [InlineData("reviewer")]
    public async Task AuthorityChangedDuringReadback_Should_PreventFinalPromotion(string change)
    {
        using var h=new PublicationScenario();await h.InitialiseAsync();var receipt=await h.ApproveAsync();var op=await h.Service.PublishAsync(receipt.Id,await h.RequestAsync(receipt.Id));
        var changed=false;h.Store.BeforeVerify=async _=>{if(changed)return;changed=true;
            if(change=="hold"){var p=await h.Service.PreviewAsync(receipt.Id);await h.Service.HoldAsync(receipt.Id,new(Guid.NewGuid(),p.Version,"Safety concern",true));}
            if(change=="permission")h.Allowed=false;
            if(change=="policy")h.Settings.Channels["arke-worlds"].DeliveryPolicyVersion="changed";
            if(change=="reviewer")h.Settings.Channels["arke-worlds"].ReviewerUserIds=[];
        };
        await h.DeliverAsync(op.Id);(await h.HeadAsync()).Should().Contain("\"serial\":52");
        (await h.Db.PublicationOperations.SingleAsync(o=>o.Id==op.Id)).Status.Should().Be("held");(await h.Db.PublishedEditions.CountAsync()).Should().Be(0);
    }
    [Fact]
    public async Task Part_Should_AppendAgainstExactBase_AndWithdrawalShouldRemoveSequenceAtHigherSerial()
    {
        using var h=new PublicationScenario();await h.InitialiseAsync();var b=await h.ApproveAsync("listening");var first=await h.Service.PublishAsync(b.Id,await h.RequestAsync(b.Id));await h.DeliverAsync(first.Id);
        var p=await h.ApproveAsync("part");var next=await h.Service.PublishAsync(p.Id,await h.RequestAsync(p.Id));await h.DeliverAsync(next.Id);
        var root=JsonNode.Parse(await h.HeadAsync())!;root["worlds"]![0]!["package"]!["parts"]!.AsArray().Should().HaveCount(1);root["serial"]!.GetValue<int>().Should().Be(54);
        await h.ExportAsync("parts");
        var catalogue=await h.Service.CatalogueAsync("arke-worlds");var entry=catalogue.Editions.Single(e=>e.Kind=="part");h.Settings.Channels["arke-worlds"].DeliveryEnabled=false;
        var withdraw=await h.Service.WithdrawAsync(entry.Id,new(Guid.NewGuid(),entry.Version,"Safety withdrawal"));await h.DeliverAsync(withdraw.Id);
        root=JsonNode.Parse(await h.HeadAsync())!;root["serial"]!.GetValue<int>().Should().Be(55);root["worlds"]!.AsArray().Should().BeEmpty();h.Store.Bytes.Should().NotBeEmpty();
        (await h.Db.Submissions.SingleAsync(s=>s.Id==b.Id)).Status.Should().Be(SubmissionStatuses.Withdrawn);
        (await h.Db.Submissions.SingleAsync(s=>s.Id==p.Id)).Status.Should().Be(SubmissionStatuses.Withdrawn);
    }
    [Fact]
    public async Task ConcurrentUnrelatedWorlds_Should_MergeOntoLatestCompleteHead()
    {
        using var h=new PublicationScenario();await h.InitialiseAsync();var a=await h.ApproveAsync("world");var b=await h.ApproveAsync("story");
        var one=await h.Service.PublishAsync(a.Id,await h.RequestAsync(a.Id));var two=await h.Service.PublishAsync(b.Id,await h.RequestAsync(b.Id));
        await h.DeliverAsync(two.Id);await h.DeliverAsync(one.Id);var root=JsonNode.Parse(await h.HeadAsync())!;root["worlds"]!.AsArray().Should().HaveCount(2);root["serial"]!.GetValue<int>().Should().Be(54);
    }
    [Fact]
    public async Task Release_Should_RejectStaleHeadAndAlteredIdempotencyArguments()
    {
        using var h=new PublicationScenario();await h.InitialiseAsync();var r=await h.ApproveAsync();var request=await h.RequestAsync(r.Id);
        await h.Service.Invoking(s=>s.PublishAsync(r.Id,request with{ExpectedSerial=51})).Should().ThrowAsync<PublishingException>().Where(e=>e.Code=="catalogue-head-changed");
        await h.Service.PublishAsync(r.Id,request);
        await h.Service.Invoking(s=>s.PublishAsync(r.Id,request with{Reason="Different intent"})).Should().ThrowAsync<PublishingException>().Where(e=>e.Status==409);
    }
    [Fact]
    public async Task SnapshotAndArtifactIdentity_Should_BeImmutableThroughPersistence()
    {
        using var h=new PublicationScenario();await h.InitialiseAsync();var snapshot=await h.Db.CatalogueSnapshots.SingleAsync();snapshot.Json="{}";
        await h.Db.Invoking(db=>db.SaveChangesAsync()).Should().ThrowAsync<InvalidOperationException>();
    }
    [Fact]
    public async Task CompetingPartCommands_Should_CommitOnlyNextPartOnce()
    {
        using var h=new PublicationScenario();await h.InitialiseAsync();var b=await h.ApproveAsync("listening");var initial=await h.Service.PublishAsync(b.Id,await h.RequestAsync(b.Id));await h.DeliverAsync(initial.Id);
        var one=await h.ApproveAsync("part");var two=await h.ApproveAsync("part");
        var first=await h.Service.PublishAsync(one.Id,await h.RequestAsync(one.Id));var second=await h.Service.PublishAsync(two.Id,await h.RequestAsync(two.Id));
        await h.DeliverAsync(first.Id);await h.DeliverAsync(second.Id);var root=JsonNode.Parse(await h.HeadAsync())!;
        root["serial"]!.GetValue<int>().Should().Be(54);root["worlds"]![0]!["package"]!["parts"]!.AsArray().Should().ContainSingle();
        (await h.Db.PublicationOperations.SingleAsync(o=>o.Id==second.Id)).Status.Should().Be("held");
    }
    [Fact]
    public async Task AttemptLimit_Should_RequireAnAuditedExplicitRetry_WithoutChangingIntent()
    {
        using var h=new PublicationScenario();h.Settings.Delivery.MaxAttempts=1;await h.InitialiseAsync();var r=await h.ApproveAsync();var op=await h.Service.PublishAsync(r.Id,await h.RequestAsync(r.Id));
        h.Store.FailReadback=true;await h.Invoking(x=>x.DeliverAsync(op.Id)).Should().ThrowAsync<IOException>();await h.DeliverAsync(op.Id);
        var dead=await h.Db.PublicationOperations.SingleAsync(o=>o.Id==op.Id);dead.Status.Should().Be("dead-letter");
        var command=new PublicationActionRequest(Guid.NewGuid(),Convert.ToBase64String(dead.RowVersion),"Public transport restored; reconcile retained assets");
        await h.Service.RetryAsync(op.Id,command);h.Store.FailReadback=false;await h.DeliverAsync(op.Id);
        (await h.Service.RetryAsync(op.Id,command)).Status.Should().Be("committed");
        (await h.Db.PublicationOperations.CountAsync(o=>o.Kind=="retry"&&o.ParentOperationId==op.Id)).Should().Be(1);(await h.HeadAsync()).Should().Contain("\"serial\":53");
    }
    [Fact]
    public async Task Baseline_Should_RefuseUnqualifiedLegacyNamespace()
    {
        using var h=new PublicationScenario();var c=h.Settings.Channels["arke-worlds"];
        c.BaselineCatalogueJson="{\"version\":2,\"serial\":52,\"worlds\":[{\"id\":\"old-world\",\"name\":\"Old world\",\"productions\":[]}]}";
        c.BaselineSha256=PlayerCatalogueProjection.Hash(c.BaselineCatalogueJson);
        await h.Invoking(x=>x.InitialiseAsync()).Should().ThrowAsync<PublishingException>().Where(e=>e.Code=="baseline-ownership-required");
        (await h.Db.PublicationChannels.CountAsync()).Should().Be(0);
    }
}
