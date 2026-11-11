using System.Net;
using System.Text;

using FluentAssertions;
using Microsoft.Extensions.Configuration;

using Aonik.Infrastructure.Publishing;
using Aonik.Publishing.Services;

namespace Aonik.Infrastructure.Tests.Publishing;

public class GitHubReleaseMirrorTests
{
    [Fact]
    public async Task ReleaseReadback_Should_FollowQualifiedRedirectWithoutCredentials()
    {
        var calls = new List<Uri>();
        using var handler = new Handler(request =>
        {
            request.Headers.Authorization.Should().BeNull();
            calls.Add(request.RequestUri!);
            if(calls.Count == 1)
                return new(HttpStatusCode.Found) { Headers = { Location = new("https://release-assets.githubusercontent.com/test") } };
            return new(HttpStatusCode.OK) { Content = new StringContent("{\"version\":2,\"serial\":26,\"worlds\":[]}") };
        });
        var mirror = Create(handler);
        var head = await mirror.ReadAsync(Channel());
        head.Json.Should().Contain("\"serial\":26");
        head.Revision.Should().HaveLength(64);
        calls[0].AbsoluteUri.Should().StartWith("https://github.com/example/player/releases/download/arke-player-worlds/catalogue.json?_arke_check=");
        calls.Should().HaveCount(2);
    }

    [Theory]
    [InlineData("http://release-assets.githubusercontent.com/test")]
    [InlineData("https://attacker.example/test")]
    [InlineData("https://token@github.com/test")]
    [InlineData("https://github.com:8443/test")]
    public async Task ReleaseReadback_Should_RefuseUnqualifiedRedirect(string url)
    {
        using var handler = new Handler(_ => new(HttpStatusCode.Found) { Headers = { Location = new(url) } });
        await Create(handler).Invoking(x => x.ReadAsync(Channel())).Should().ThrowAsync<IOException>();
        handler.Calls.Should().Be(1);
    }

    [Fact]
    public async Task ReleaseWrite_Should_StayPendingWithoutSendingAnyRequest()
    {
        using var handler = new Handler(_ => throw new InvalidOperationException("No request expected"));
        await Create(handler).Invoking(x => x.CompareAndSwapAsync(Channel(), null, "{}"))
            .Should().ThrowAsync<PublishingException>().Where(e => e.Code == "mirror-release-pending" && e.Status == 409);
        handler.Calls.Should().Be(0);
    }

    [Fact]
    public async Task MissingRelease_Should_RemainUnconfirmed()
    {
        using var handler = new Handler(_ => new(HttpStatusCode.NotFound));
        var head = await Create(handler).ReadAsync(Channel());
        head.Json.Should().BeNull();
        head.Revision.Should().BeNull();
    }

    [Fact]
    public async Task ReleaseReadback_Should_RefuseOversizedAndInvalidUtf8Content()
    {
        using var large = new Handler(_ => new(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[1048577]) });
        await Create(large).Invoking(x => x.ReadAsync(Channel())).Should().ThrowAsync<IOException>();
        using var invalid = new Handler(_ => new(HttpStatusCode.OK) { Content = new ByteArrayContent([0xff]) });
        await Create(invalid).Invoking(x => x.ReadAsync(Channel())).Should().ThrowAsync<DecoderFallbackException>();
    }

    [Theory]
    [InlineData("../tag", "catalogue.json")]
    [InlineData("tag", "another.json")]
    public async Task ReleaseReadback_Should_RejectUnqualifiedDestination(string tag, string path)
    {
        using var handler = new Handler(_ => throw new InvalidOperationException("No request expected"));
        var channel = Channel(); channel.LegacyMirrorReleaseTag = tag; channel.LegacyMirrorPath = path;
        await Create(handler).Invoking(x => x.ReadAsync(channel)).Should().ThrowAsync<PublishingException>();
        handler.Calls.Should().Be(0);
    }

    private static PublishingChannelOptions Channel() => new()
    {
        LegacyMirrorEnabled = true, LegacyMirrorMode = "release", LegacyMirrorRepository = "example/player",
        LegacyMirrorReleaseTag = "arke-player-worlds", LegacyMirrorPath = "catalogue.json"
    };

    private static GitHubLegacyCatalogueMirror Create(Handler handler) => new(new Factory(handler),
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        { ["Publishing:LegacyMirrorToken"] = "must-never-be-sent-to-release-downloads" }).Build());

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, false);
    }
    private sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Calls++; return Task.FromResult(respond(request)); }
    }
}
