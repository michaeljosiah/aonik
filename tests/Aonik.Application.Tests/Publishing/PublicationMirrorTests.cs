using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Aonik.Publishing.Contracts;
using Aonik.Publishing.Services;
using Aonik.Publishing.Testing;
using Aonik.SharedKernel.Events.Integration;
using Aonik.TestSupport.Multitenancy;

namespace Aonik.Application.Tests.Publishing;

public class PublicationMirrorTests
{
    [Fact]
    public async Task HistoricalMirrorEvent_Should_ReconcileLatestHead_AndRecoverLostResponse()
    {
        using var h=new PublicationScenario();h.Settings.Channels["arke-worlds"].LegacyMirrorEnabled=true;await h.InitialiseAsync();
        var historical=await h.Db.CatalogueSnapshots.SingleAsync();var r=await h.ApproveAsync();var op=await h.Service.PublishAsync(r.Id,await h.RequestAsync(r.Id));await h.DeliverAsync(op.Id);
        var mirror=new CasMirror{LoseResponse=true};var handler=new PublicationMirrorHandler(h.Db,mirror,Microsoft.Extensions.Options.Options.Create(h.Settings),new TestTenantProvider(h.Tenant),h.Clock);
        var message=new PublicationCatalogueCommittedEvent(historical.Id,h.Clock.UtcNow,h.Tenant,"arke-worlds",52);
        await handler.Invoking(x=>x.HandleAsync(message)).Should().ThrowAsync<IOException>();
        mirror.Json.Should().Contain("\"serial\":53");await handler.HandleAsync(message);
        mirror.Writes.Should().Be(1);(await h.Db.PublicationChannels.SingleAsync()).MirroredSerial.Should().Be(53);
    }
    [Theory]
    [InlineData(60,"remote-ahead")]
    [InlineData(52,"same-serial-conflict")]
    public async Task RemoteConflict_Should_HoldMirrorWithoutChangingCanonical(int serial,string code)
    {
        using var h=new PublicationScenario();h.Settings.Channels["arke-worlds"].LegacyMirrorEnabled=true;await h.InitialiseAsync();var snapshot=await h.Db.CatalogueSnapshots.SingleAsync();
        var mirror=new CasMirror{Json="{\"version\":2,\"serial\":"+serial+",\"worlds\":[],\"band\":\"other\"}"};
        var handler=new PublicationMirrorHandler(h.Db,mirror,Microsoft.Extensions.Options.Options.Create(h.Settings),new TestTenantProvider(h.Tenant),h.Clock);
        await handler.HandleAsync(new(snapshot.Id,h.Clock.UtcNow,h.Tenant,"arke-worlds",52));
        (await h.Db.PublicationChannels.SingleAsync()).MirrorCode.Should().Be(code);mirror.Writes.Should().Be(0);(await h.HeadAsync()).Should().Contain("\"serial\":52");
    }
    [Fact]
    public async Task ExpiredOldMirrorWorker_Should_NotRegressRemoteOrAcknowledgeNewClaim()
    {
        using var h=new PublicationScenario();h.Settings.Channels["arke-worlds"].LegacyMirrorEnabled=true;await h.InitialiseAsync();var snapshot=await h.Db.CatalogueSnapshots.SingleAsync();
        var mirror=new CasMirror();var handler=new PublicationMirrorHandler(h.Db,mirror,Microsoft.Extensions.Options.Options.Create(h.Settings),new TestTenantProvider(h.Tenant),h.Clock);
        var message=new PublicationCatalogueCommittedEvent(snapshot.Id,h.Clock.UtcNow,h.Tenant,"arke-worlds",52);
        mirror.BeforeCas=async()=>{mirror.BeforeCas=null;h.Clock.UtcNow=h.Clock.UtcNow.AddMinutes(3);var r=await h.ApproveAsync();var op=await h.Service.PublishAsync(r.Id,await h.RequestAsync(r.Id));await h.DeliverAsync(op.Id);await handler.HandleAsync(message);};
        await handler.HandleAsync(message);
        mirror.Json.Should().Contain("\"serial\":53");mirror.Writes.Should().Be(1);(await h.Db.PublicationChannels.SingleAsync()).MirroredSerial.Should().Be(53);
    }
    private sealed class CasMirror:ILegacyCatalogueMirror
    {
        public string? Json;public int Writes;public bool LoseResponse;public Func<Task>? BeforeCas;
        public Task<LegacyMirrorHead> ReadAsync(PublishingChannelOptions channel,CancellationToken cancellationToken=default)=>Task.FromResult(new LegacyMirrorHead(Json is null?null:Writes.ToString(),Json));
        public async Task CompareAndSwapAsync(PublishingChannelOptions channel,string? expectedRevision,string json,CancellationToken cancellationToken=default)
        {if(BeforeCas is not null)await BeforeCas();if(expectedRevision!=(Json is null?null:Writes.ToString()))throw new PublishingException(409,"mirror-head-conflict","CAS conflict");Json=json;Writes++;if(LoseResponse){LoseResponse=false;throw new IOException("Committed remote response lost");}}
    }
}
