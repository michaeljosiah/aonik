using System.Net;
using System.Security.Cryptography;

using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;

using Aonik.Infrastructure.Publishing;
using Aonik.Publishing.Contracts;
using Aonik.Publishing.Entities;
using Aonik.Publishing.Services;

namespace Aonik.Application.Tests.Publishing;

public class PublicArtifactStoreTests
{
    private sealed class Scope:IDisposable
    {
        public readonly string Root=Path.Combine(Path.GetTempPath(),"aonik-public-store-test-"+Guid.NewGuid().ToString("N"));
        public readonly PublishingOptions Settings=new();public readonly byte[] Bytes=[1,2,3,4];
        public readonly PublicationArtifact Artifact=new(){TenantId=Guid.NewGuid(),OperationId=Guid.NewGuid(),ChannelKey="arke-worlds",Bytes=4,MediaType="application/zip"};
        public readonly ResponseHandler Handler=new();public readonly ImmutablePublicationArtifactStore Store;
        public Scope()
        {
            Settings.Delivery.PublicRoot=Path.Combine(Root,"public");Settings.Intake.PrivateRoot=Path.Combine(Root,"private");Settings.Channels["arke-worlds"]=new(){TenantId=Artifact.TenantId,PublicBaseUrl="https://public.example.test"};
            var environment=Mock.Of<IHostEnvironment>(e=>e.ContentRootPath==Path.Combine(Root,"application"));
            var clients=new Mock<IHttpClientFactory>();clients.Setup(c=>c.CreateClient("Publishing.PublicReadback")).Returns(()=>new HttpClient(Handler,false));
            Store=new(Microsoft.Extensions.Options.Options.Create(Settings),environment,new ConfigurationBuilder().Build(),clients.Object);
            Artifact.StorageLocationId=Store.LocationId;Artifact.Sha256=Convert.ToHexStringLower(SHA256.HashData(Bytes));Artifact.Url=PublicationBytes.Url(Settings.Channels["arke-worlds"],Artifact.ChannelKey,Artifact.Id);
        }
        public void Dispose(){var root=Path.GetFullPath(Root);if(Path.GetDirectoryName(root)!=Path.TrimEndingDirectorySeparator(Path.GetTempPath())||!Path.GetFileName(root).StartsWith("aonik-public-store-test-"))throw new InvalidOperationException();if(Directory.Exists(root))Directory.Delete(root,true);Handler.Dispose();}
    }
    private sealed class ResponseHandler:HttpMessageHandler
    {
        public HttpStatusCode Status{get;set;}=HttpStatusCode.OK;public byte[] Data{get;set;}=[1,2,3,4];public string Type{get;set;}="application/zip";
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {var response=new HttpResponseMessage(Status){Content=new ByteArrayContent(Data)};response.Content.Headers.ContentType=new(Type);return Task.FromResult(response);}
    }
    [Fact]
    public async Task ImmutableStore_Should_ReconcileRepeatedCopies_AndRejectDifferentBytes()
    {
        using var h=new Scope();await h.Store.WriteAsync(h.Artifact,new MemoryStream(h.Bytes));await h.Store.WriteAsync(h.Artifact,new MemoryStream(h.Bytes));
        await h.Store.Invoking(s=>s.WriteAsync(h.Artifact,new MemoryStream([9,8,7,6]))).Should().ThrowAsync<PublishingException>().Where(e=>e.Code=="publication-bytes-mismatch");
        await using var source=await h.Store.OpenAsync(h.Artifact);using var copied=new MemoryStream();await source.CopyToAsync(copied);copied.ToArray().Should().Equal(h.Bytes);
        await h.Store.VerifyPublicAsync(h.Artifact);
    }
    [Theory]
    [InlineData("redirect")]
    [InlineData("type")]
    [InlineData("length")]
    [InlineData("hash")]
    public async Task Readback_Should_FailUnlessActualPublicResponseMatchesDescriptor(string fault)
    {
        using var h=new Scope();if(fault=="redirect")h.Handler.Status=HttpStatusCode.Redirect;if(fault=="type")h.Handler.Type="text/html";
        if(fault=="length")h.Handler.Data=[1,2];if(fault=="hash")h.Handler.Data=[4,3,2,1];
        if(fault=="hash")await h.Store.Invoking(s=>s.VerifyPublicAsync(h.Artifact)).Should().ThrowAsync<PublishingException>().Where(e=>e.Code=="publication-bytes-mismatch");
        else await h.Store.Invoking(s=>s.VerifyPublicAsync(h.Artifact)).Should().ThrowAsync<IOException>();
    }
    [Fact]
    public void PublicRoot_Should_RejectOverlapWithPrivateOrGenericStorage()
    {using var h=new Scope();h.Settings.Delivery.PublicRoot=h.Settings.Intake.PrivateRoot;h.Store.Invoking(s=>{_=s.LocationId;}).Should().Throw<PublishingException>();}
}
