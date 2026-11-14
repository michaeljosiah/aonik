using System.Net;

using FluentAssertions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Moq;

using Aonik.Infrastructure.Integrations.Gmail;
using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Infrastructure.Tests.Integrations;

public class GmailMailboxSyncProviderTests
{
    [Fact]
    public async Task History_Should_UseFixedEndpoints_ExactCursorAndEncodedPage_AndOnlyMessageAddedChanges()
    {
        using var handler = new Handler("""{"historyId":"200","nextPageToken":"next","history":[{"id":"150","messagesAdded":[{"message":{"id":"mail-1","threadId":"thread-1"}}]}]}""");
        var provider = Provider(handler);

        MailboxHistoryPage page = await provider.ReadHistoryAsync(Call(), "100", "token&admin=true");

        handler.Uri!.GetLeftPart(UriPartial.Path).Should().Be("https://gmail.googleapis.com/gmail/v1/users/me/history");
        var query = QueryHelpers.ParseQuery(handler.Uri.Query);
        query["startHistoryId"].ToString().Should().Be("100"); query["pageToken"].ToString().Should().Be("token&admin=true");
        query["historyTypes"].ToString().Should().Be("messageAdded");
        handler.Authorization.Should().Be("Bearer synthetic-access"); page.AddedMessages.Should().ContainSingle().Which.MessageId.Should().Be("mail-1");
        page.HistoryId.Should().Be("200"); page.NextPageToken.Should().Be("next");
    }

    [Fact]
    public async Task History_Should_MapAnExpiredCursorToFullSynchronization_WithoutTryingAnotherAccount()
    {
        using var handler = new Handler("{}", HttpStatusCode.NotFound);
        Func<Task> read = () => Provider(handler).ReadHistoryAsync(Call(), "100", null);
        await read.Should().ThrowAsync<ConnectorProviderException>().Where(value => value.Code == "MailboxCursorExpired");
        handler.Calls.Should().Be(1);
    }

    [Theory]
    [InlineData("1&user=other")]
    [InlineData("../other")]
    [InlineData("-1")]
    public async Task History_Should_RejectMalformedCursorsBeforeProviderIO(string cursor)
    {
        using var handler = new Handler("{}"); Func<Task> read = () => Provider(handler).ReadHistoryAsync(Call(), cursor, null);
        await read.Should().ThrowAsync<ConnectorProviderException>(); handler.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Push_Should_RejectAnUnsignedEnvelope_AndRequireDeploymentOwnedConfiguration()
    {
        using var handler = new Handler("{}");
        var verifier = new GmailMailboxPushVerifier(Provider(handler), new FixedOptions<GmailMailboxOptions>(new()));
        Func<Task> accept = () => verifier.VerifyAsync("Bearer forged", "{}");
        await accept.Should().ThrowAsync<ConnectorAccessException>().Where(value => value.Code == "MailboxPushUnauthorized");
        handler.Calls.Should().Be(0);
    }

    private static MailboxProviderCall Call() => new(Guid.NewGuid(), "selected-account", "synthetic-access");
    private static GmailMailboxSyncProvider Provider(Handler handler)
    {
        var clients = new Mock<System.Net.Http.IHttpClientFactory>(); clients.Setup(value => value.CreateClient("Gmail")).Returns(() => new HttpClient(handler, false));
        return new(clients.Object, new FixedOptions<GmailConnectorOptions>(new() { ClientId = "reviewed-client" }), new FixedOptions<GmailMailboxOptions>(new()));
    }
    private sealed class Handler(string body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public Uri? Uri { get; private set; } public string? Authorization { get; private set; } public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Calls++; Uri = request.RequestUri; Authorization = request.Headers.Authorization?.ToString(); return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) }); }
    }
    private sealed class FixedOptions<T>(T value) : IOptionsMonitor<T> where T : class
    {
        public T CurrentValue => value;
        public T Get(string? name) => value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
