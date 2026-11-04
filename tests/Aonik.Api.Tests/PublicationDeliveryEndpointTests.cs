using System.Net;
using System.Net.Http.Json;

using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Aonik.Publishing.Contracts;
using Aonik.Publishing.Entities;
using Aonik.Publishing.Persistence;
using Aonik.Publishing.Services;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Api.Tests;

public sealed class PublicationHttpFactory:CustomWebApplicationFactory
{
    public readonly Guid Publisher=Guid.NewGuid(),Other=Guid.NewGuid();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);builder.ConfigureServices(s=>s.Configure<PublishingOptions>(o=>
        {o.Channels["arke-worlds"]=new(){TenantId=Publisher,PublicCatalogueEnabled=true};o.Channels["private-channel"]=new(){TenantId=Other};}));
    }
    public async Task SeedAsync()
    {
        using var scope=Services.CreateScope();var context=scope.ServiceProvider.GetRequiredService<ITenantContext>();context.TenantId=Publisher;
        var db=scope.ServiceProvider.GetRequiredService<IPublishingDataContext>();if(await db.PublicationChannels.AnyAsync(c=>c.TenantId==Publisher))return;
        foreach(var tenant in new[]{Publisher,Other})
        {
            var op=new PublicationOperation{TenantId=tenant,ChannelKey="arke-worlds",ClientOperationId=Guid.NewGuid(),CommandDigest=new string('a',64),Kind="initialise",Status="committed",ActorUserId=Guid.NewGuid(),ActorPartyId=Guid.NewGuid(),Reason="Synthetic public scope test"};
            var json="{\"version\":2,\"serial\":"+(tenant==Publisher?52:999)+",\"worlds\":[]}";
            var snapshot=new PublicationCatalogueSnapshot{TenantId=tenant,ChannelKey="arke-worlds",Serial=tenant==Publisher?52:999,OperationId=op.Id,Json=json,Sha256=new string('b',64)};
            db.PublicationOperations.Add(op);db.CatalogueSnapshots.Add(snapshot);db.PublicationChannels.Add(new(){TenantId=tenant,ChannelKey="arke-worlds",Serial=snapshot.Serial,HeadSnapshotId=snapshot.Id,BaselineSha256=new string('b',64),PublicBaseUrl="https://example.invalid"});
        }
        await db.SaveChangesAsync();
    }
}
public class PublicationDeliveryEndpointTests(PublicationHttpFactory factory):IClassFixture<PublicationHttpFactory>
{
    [Fact]
    public async Task AnonymousCatalogue_Should_UseOnlyServerPublisher_DespiteCallerTenantHeader()
    {
        await factory.SeedAsync();using var client=factory.CreateClient();client.DefaultRequestHeaders.Add("X-Tenant-Id",factory.Other.ToString());
        var response=await client.GetAsync("/publishing/channels/arke-worlds/catalogue.json");response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"serial\":52").And.NotContain("999");response.Headers.CacheControl!.MaxAge.Should().Be(TimeSpan.FromSeconds(60));
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");response.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
    }
    [Theory]
    [InlineData("/publishing/channels/private-channel/catalogue.json")]
    [InlineData("/publishing/channels/unknown/catalogue.json")]
    [InlineData("/publishing/channels/arke-worlds/assets/11111111-1111-4111-8111-111111111111")]
    public async Task PublicRoutes_Should_NotExposeUnconfiguredChannelsOrUnknownArtifacts(string path)
    {using var client=factory.CreateClient();(await client.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.NotFound);}
    [Theory]
    [InlineData("/publishing/review/submissions?channelKey=arke-worlds")]
    [InlineData("/publishing/management/channels")]
    [InlineData("/publishing/management/channels/arke-worlds/catalogue")]
    public async Task PrivateStaffRoutes_Should_RequireAuthentication(string path)
    {using var client=factory.CreateClient();(await client.GetAsync(path)).StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized,HttpStatusCode.Forbidden);}
    [Fact]
    public async Task Management_Should_RequireNamedPermissionAndAssignedActiveParty()
    {
        var denied=await factory.CreateAuthenticatedClientAsync(TestAuthOptions.Create().WithTenant(factory.Publisher).WithRoles("Operations").WithPermissions("Workspace.Use"));
        (await denied.GetAsync("/publishing/management/channels")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var auth=TestAuthOptions.Create().WithTenant(factory.Publisher).WithRoles("Operations").WithPermissions("Publishing.Publish");
        var client=await factory.CreateAuthenticatedClientAsync(auth);await WorkspaceTestSeeding.SeedPartyAsync(factory,factory.Publisher,auth.UserId,"Assigned publisher");
        var options=factory.Services.GetRequiredService<IOptions<PublishingOptions>>().Value;options.Channels["arke-worlds"].PublisherUserIds.Add(auth.UserId);
        var response=await client.GetAsync("/publishing/management/channels");response.StatusCode.Should().Be(HttpStatusCode.OK);
        var channels=await response.Content.ReadFromJsonAsync<PublicationChannelResponse[]>();channels!.Should().ContainSingle(c=>c.CanPublish&&!c.CanReview&&!c.CanWithdraw);
        (await client.PostAsJsonAsync("/publishing/editions/11111111-1111-4111-8111-111111111111/withdraw",new PublicationActionRequest(Guid.NewGuid(),"version","No withdrawal permission"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
