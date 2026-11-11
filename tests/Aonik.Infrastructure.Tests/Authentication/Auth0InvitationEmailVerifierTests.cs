using System.Net;
using System.Text;
using Aonik.Infrastructure.Authentication;
using FluentAssertions;

namespace Aonik.Infrastructure.Tests.Authentication;

public class Auth0InvitationEmailVerifierTests
{
    [Theory]
    [InlineData("{\"sub\":\"auth0|creator\",\"email\":\"creator@example.test\",\"email_verified\":true}", true)]
    [InlineData("{\"sub\":\"auth0|creator\",\"email\":\"creator@example.test\",\"email_verified\":false}", false)]
    [InlineData("{\"sub\":\"auth0|attacker\",\"email\":\"creator@example.test\",\"email_verified\":true}", false)]
    [InlineData("{\"sub\":\"auth0|creator\",\"email\":\"other@example.test\",\"email_verified\":true}", false)]
    [InlineData("{\"sub\":\"auth0|creator\",\"email\":\"creator@example.test\",\"email_verified\":\"true\"}", false)]
    [InlineData("{}", false)]
    [InlineData("not json", false)]
    public async Task VerifyAsync_Should_RequireVerifiedMatchingIdentityAndMailbox(string body, bool expected)
    {
        using var handler = new ProfileHandler(body);
        using var client = new HttpClient(handler);
        var result = await Auth0InvitationEmailVerifier.VerifyAsync(client, "https://issuer.example/", "test-bearer", "auth0|creator", "creator@example.test");
        result.Should().Be(expected);
        handler.RequestUri.Should().Be(new Uri("https://issuer.example/userinfo"));
        handler.Authorization.Should().Be("Bearer test-bearer");
    }

    [Fact]
    public async Task VerifyAsync_Should_RefuseUnavailableProfileOrInsecureAuthority()
    {
        using var handler = new ProfileHandler("{}", HttpStatusCode.ServiceUnavailable);
        using var client = new HttpClient(handler);
        (await Auth0InvitationEmailVerifier.VerifyAsync(client, "http://issuer.example/", "token", "creator", "creator@example.test")).Should().BeFalse();
        handler.RequestUri.Should().BeNull();
        (await Auth0InvitationEmailVerifier.VerifyAsync(client, "https://issuer.example/", "token", "creator", "creator@example.test")).Should().BeFalse();
    }

    private sealed class ProfileHandler(string body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string? Authorization { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            Authorization = request.Headers.Authorization?.ToString();
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
