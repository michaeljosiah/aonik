using System.Net;
using System.Text;
using System.Text.Json;

using Aonik.Infrastructure.Authentication.Account;
using Aonik.Infrastructure.Authentication.Configuration;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Services.Settings;

using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Aonik.Infrastructure.Tests.Authentication;

public sealed class Auth0ConfirmedEmailChangeTests
{
    private const string Subject = "auth0|account-1";
    private const string Connection = "Username-Password-Authentication";
    private static User User() => new() { ExternalIssuer = "https://login.example/", ExternalSubject = Subject, Email = "old@example.com" };

    [Fact]
    public async Task Confirm_Should_VerifyExactSubjectBeforeAndAfterChangingProvedMailbox()
    {
        using var handler = new ScriptedHandler(Token(), Profile("old@example.com"), "{}", Profile("new@example.com"));

        await Service(handler).ConfirmVerifiedEmailAsync(User(), "old@example.com", "new@example.com");

        handler.Calls.Select(c => c.Method).Should().Equal("POST", "GET", "PATCH", "GET");
        handler.Calls.Skip(1).Should().OnlyContain(c => c.Uri == "https://tenant.auth0.example/api/v2/users/auth0%7Caccount-1");
        using var body = JsonDocument.Parse(handler.Calls[2].Body);
        body.RootElement.GetProperty("email").GetString().Should().Be("new@example.com");
        body.RootElement.GetProperty("email_verified").GetBoolean().Should().BeTrue();
        body.RootElement.GetProperty("verify_email").GetBoolean().Should().BeFalse();
        body.RootElement.GetProperty("connection").GetString().Should().Be(Connection);
    }

    [Fact]
    public async Task Confirm_Should_ReconcileAnAlreadyAppliedTargetWithoutAnotherPatch()
    {
        using var handler = new ScriptedHandler(Token(), Profile("new@example.com"));

        await Service(handler).ConfirmVerifiedEmailAsync(User(), "old@example.com", "new@example.com");

        handler.Calls.Select(c => c.Method).Should().Equal("POST", "GET");
    }

    [Theory]
    [InlineData("wrong-subject")]
    [InlineData("changed-email")]
    [InlineData("target-unverified")]
    [InlineData("blocked")]
    [InlineData("wrong-connection")]
    [InlineData("linked")]
    [InlineData("social")]
    public async Task Confirm_Should_RejectUnexpectedIdentityWithoutWriting(string scenario)
    {
        var profile = Profile("old@example.com");
        if (scenario == "wrong-subject") profile = profile.Replace(Subject, "auth0|another");
        if (scenario == "changed-email") profile = Profile("elsewhere@example.com");
        if (scenario == "target-unverified") profile = Profile("new@example.com", verified: false);
        if (scenario == "blocked") profile = profile.Replace("\"blocked\":false", "\"blocked\":true");
        if (scenario == "wrong-connection") profile = profile.Replace(Connection, "Other");
        if (scenario == "linked") profile = profile.Replace("\"identities\":[", "\"identities\":[{},");
        if (scenario == "social") profile = profile.Replace("\"isSocial\":false", "\"isSocial\":true");
        using var handler = new ScriptedHandler(Token(), profile);

        await Service(handler).Invoking(s => s.ConfirmVerifiedEmailAsync(User(), "old@example.com", "new@example.com"))
            .Should().ThrowAsync<InvalidOperationException>();

        handler.Calls.Should().NotContain(c => c.Method == "PATCH");
    }

    [Fact]
    public async Task Confirm_Should_RequireReadBackOfVerifiedTarget_AfterProviderAcceptance()
    {
        using var handler = new ScriptedHandler(Token(), Profile("old@example.com"), "{}", Profile("new@example.com", verified: false));

        await Service(handler).Invoking(s => s.ConfirmVerifiedEmailAsync(User(), "old@example.com", "new@example.com"))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*could not be reconciled*");
    }

    [Fact]
    public async Task Confirm_Should_KeepUncertainPatchRetryBoundToTheSameTarget()
    {
        using var handler = new ScriptedHandler(Token(), Profile("old@example.com"), "timeout", Token(), Profile("new@example.com"));
        var service = Service(handler);
        await service.Invoking(s => s.ConfirmVerifiedEmailAsync(User(), "old@example.com", "new@example.com"))
            .Should().ThrowAsync<TaskCanceledException>();

        await service.ConfirmVerifiedEmailAsync(User(), "old@example.com", "new@example.com");

        handler.Calls.Count(c => c.Method == "PATCH").Should().Be(1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Confirm_Should_RejectWrongIssuerOrNonDatabaseSubjectBeforeHttp(bool wrongIssuer)
    {
        var user = User();
        if (wrongIssuer) user.ExternalIssuer = "https://different.example/";
        else user.ExternalSubject = "google-oauth2|account-1";
        using var handler = new ScriptedHandler();

        await Service(handler).Invoking(s => s.ConfirmVerifiedEmailAsync(user, "old@example.com", "new@example.com"))
            .Should().ThrowAsync<InvalidOperationException>();
        handler.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task Confirm_Should_PreserveCallerCancellationWithoutHttp()
    {
        using var handler = new ScriptedHandler();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Service(handler).Invoking(s => s.ConfirmVerifiedEmailAsync(User(), "old@example.com", "new@example.com", cancellation.Token))
            .Should().ThrowAsync<OperationCanceledException>();
        handler.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task Confirm_Should_NotExposeProviderErrorBodies()
    {
        using var handler = new ScriptedHandler("error");

        var failure = await Service(handler).Invoking(s => s.ConfirmVerifiedEmailAsync(User(), "old@example.com", "new@example.com"))
            .Should().ThrowAsync<InvalidOperationException>();
        failure.Which.Message.Should().NotContain("sensitive@example.com").And.NotContain("secret");
    }

    private static Auth0AccountService Service(ScriptedHandler handler)
    {
        var settings = new InMemorySettingProvider();
        settings.Set(AuthSettingNames.Auth0Domain, "tenant.auth0.example");
        settings.Set(AuthSettingNames.Auth0Connection, Connection);
        settings.Set(AuthSettingNames.Auth0ManagementClientId, "management-client");
        settings.Set(AuthSettingNames.Auth0ManagementClientSecret, "secret");
        return new(new HttpClient(handler), settings, Options.Create(new AuthOptions
        {
            Provider = "Auth0", Auth0 = new Auth0Options { Authority = "https://login.example/" }
        }));
    }

    private static string Token() => """{"access_token":"management-token"}""";
    private static string Profile(string email, bool verified = true) => JsonSerializer.Serialize(new
    {
        user_id = Subject, email, email_verified = verified, blocked = false,
        identities = new[] { new { provider = "auth0", connection = Connection, user_id = "account-1", isSocial = false } }
    });

    private sealed class ScriptedHandler(params string[] replies) : HttpMessageHandler
    {
        private readonly Queue<string> _replies = new(replies);
        public List<(string Method, string Uri, string Body)> Calls { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls.Add((request.Method.Method, request.RequestUri!.AbsoluteUri,
                request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken)));
            var reply = _replies.Dequeue();
            if (reply == "timeout") throw new TaskCanceledException("Uncertain provider response");
            return new HttpResponseMessage(reply == "error" ? HttpStatusCode.BadRequest : HttpStatusCode.OK)
            {
                Content = new StringContent(reply == "error" ? "sensitive@example.com secret" : reply, Encoding.UTF8, "application/json")
            };
        }
    }
}
