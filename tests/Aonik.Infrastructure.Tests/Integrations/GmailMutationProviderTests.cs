using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

using FluentAssertions;
using Microsoft.Extensions.Options;
using MimeKit;
using Moq;

using Aonik.Infrastructure.Integrations.Gmail;
using Aonik.Infrastructure.Storage;
using Aonik.Infrastructure.Tests.Storage;
using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Infrastructure.Tests.Integrations;

public class GmailMutationProviderTests
{
    [Fact]
    public async Task PreparedAttachments_Should_SendOnlyTheReviewedScannedBytesEvenIfTheSourceReferenceChanges()
    {
        await using var scanner = new ScriptedClamAvServer(expectedConnections: 3);
        using var host = new Host(scanner.Scanner);
        byte[] original = "%PDF-1.4 reviewed attachment"u8.ToArray();
        string attachment = host.StoreAttachment(original);
        GmailPreparedMessage prepared = await host.Prepare([attachment]);
        host.Content[attachment] = "%PDF-1.4 replacement attachment"u8.ToArray();

        await host.Provider.InvokeAsync(host.Call(GmailActionKeys.SendMessage, new GmailSendRequest("send-attachment", prepared.MessageReference, prepared.ContentHash)));

        byte[] raw = Decode(JsonDocument.Parse(host.Writes.Single().Body).RootElement.GetProperty("raw").GetString()!);
        using MimeMessage message = MimeMessage.Load(new MemoryStream(raw));
        using var output = new MemoryStream(); message.Attachments.OfType<MimePart>().Single().Content!.DecodeTo(output);
        output.ToArray().Should().Equal(original);
        JsonDocument.Parse(host.Content[prepared.MessageReference]).RootElement.GetProperty("evidence").GetProperty("attachments")[0].GetProperty("sha256").GetString()
            .Should().Be(Convert.ToHexStringLower(SHA256.HashData(original)));
    }

    [Fact]
    public async Task Prepare_Should_RejectAnAttachmentWhoseStoredBytesNoLongerMatchItsProtectedHash()
    {
        using var host = new Host();
        string attachment = host.StoreAttachment("%PDF-1.4 one"u8.ToArray());
        host.Content[attachment] = "%PDF-1.4 two"u8.ToArray();

        await ((Func<Task>)(() => host.Prepare([attachment]))).Should().ThrowAsync<ConnectorProviderException>().Where(error => error.Code == "AttachmentHashMismatch");

        host.Content.Should().ContainSingle(); host.Writes.Should().BeEmpty();
    }
    [Fact]
    public async Task PrepareAndSend_Should_ReviewAndTransmitTheSameBytesIncludingBcc()
    {
        using var host = new Host();
        GmailPreparedMessage prepared = await host.Prepare();
        var call = host.Call(GmailActionKeys.SendMessage, new GmailSendRequest("send-1", prepared.MessageReference, prepared.ContentHash));

        var preview = await host.Provider.PreviewAsync(call);
        GmailMutationReceipt result = ConnectorJson.Read<GmailMutationReceipt>(await host.Provider.InvokeAsync(call));

        preview!.After!.Value.GetRawText().Should().Contain("hidden@example.com").And.Contain("reviewed body");
        result.MessageIds.Should().Equal("sent-1");
        host.Writes.Should().ContainSingle();
        JsonElement sent = JsonDocument.Parse(host.Writes.Single().Body).RootElement;
        byte[] raw = Decode(sent.GetProperty("raw").GetString()!);
        using MimeMessage message = MimeMessage.Load(new MemoryStream(raw));
        message.Bcc.Mailboxes.Single().Address.Should().Be("hidden@example.com");
        message.TextBody!.TrimEnd('\r', '\n').Should().Be("reviewed body");
        message.MessageId.Should().Be(prepared.Rfc822MessageId);
        JsonElement envelope = JsonDocument.Parse(host.Content[prepared.MessageReference]).RootElement;
        raw.Should().Equal(Convert.FromBase64String(envelope.GetProperty("rawBase64").GetString()!));
        ConnectorJson.Serialize(prepared).Should().NotContain("reviewed body").And.NotContain("hidden@example.com");
    }

    [Theory]
    [InlineData("subject\r\nBcc: injected@example.com", "normal@example.com")]
    [InlineData("invoice", "Name <target@example.com>")]
    [InlineData("invoice", "target@example.com\r\nBcc: injected@example.com")]
    public async Task Prepare_Should_RejectHeaderInjectionBeforeStorageOrProviderIo(string subject, string recipient)
    {
        using var host = new Host();
        Func<Task> prepare = () => host.Provider.InvokeAsync(host.Call(GmailActionKeys.PrepareMessage,
            new GmailPrepareMessageRequest(GmailActionKeys.SendMessage, [recipient], subject, "body")));

        await prepare.Should().ThrowAsync<ArgumentException>();
        host.Content.Should().BeEmpty(); host.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Send_Should_RejectAChangedImmutableReferenceOrDifferentAccount()
    {
        using var host = new Host();
        GmailPreparedMessage prepared = await host.Prepare();
        var call = host.Call(GmailActionKeys.SendMessage, new GmailSendRequest("send-1", prepared.MessageReference, new string('0', 64)));

        await ((Func<Task>)(() => host.Provider.InvokeAsync(call))).Should().ThrowAsync<ConnectorAccessException>();
        call = host.Call(GmailActionKeys.SendMessage, new GmailSendRequest("send-1", prepared.MessageReference, prepared.ContentHash)) with { ConnectionId = Guid.NewGuid() };
        await ((Func<Task>)(() => host.Provider.InvokeAsync(call))).Should().ThrowAsync<ConnectorAccessException>();
        host.Writes.Should().BeEmpty();
    }

    [Fact]
    public async Task SendDraft_Should_RefuseAnExternallyEditedDraftWithoutWriting()
    {
        using var host = new Host();
        GmailPreparedMessage prepared = await host.Prepare();
        byte[] original = host.Raw(prepared);
        host.Response = _ => Host.Json(new { id = "draft-1", message = new { id = "message-1", threadId = "thread-1", raw = Convert.ToBase64String(original) } });
        GmailDraftResult draft = ConnectorJson.Read<GmailDraftResult>(await host.Provider.InvokeAsync(host.Call(GmailActionKeys.GetDraft, new GmailGetDraftRequest("draft-1"))));
        byte[] changed = [.. original, 13, 10];
        host.Response = _ => Host.Json(new { id = "draft-1", message = new { id = "message-2", threadId = "thread-1", raw = Convert.ToBase64String(changed) } });

        Func<Task> send = () => host.Provider.InvokeAsync(host.Call(GmailActionKeys.SendDraft,
            new GmailDraftMessageRequest("draft-send-1", draft.DraftId, draft.ContentHash, draft.MessageReference, draft.ReviewedContentHash)));

        (await send.Should().ThrowAsync<ConnectorAccessException>()).Which.Code.Should().Be("DraftChanged");
        host.Writes.Should().BeEmpty();
    }

    [Theory]
    [InlineData("matching", true)]
    [InlineData("changed-body", false)]
    [InlineData("not-sent", false)]
    [InlineData("duplicate", false)]
    public async Task ReconcileSend_Should_RequireOneSentItemAndExactContentWithoutWriting(string evidence, bool completed)
    {
        using var host = new Host();
        GmailPreparedMessage prepared = await host.Prepare();
        byte[] raw = host.Raw(prepared);
        if (evidence == "changed-body")
        {
            using MimeMessage changed = MimeMessage.Load(new MemoryStream(raw));
            changed.Body = new TextPart("plain") { Text = "different body" };
            using var output = new MemoryStream(); changed.WriteTo(output); raw = output.ToArray();
        }
        host.Response = request => request.RequestUri!.AbsolutePath.EndsWith("/messages", StringComparison.Ordinal)
            ? Host.Json(new { messages = evidence == "duplicate" ? new[] { new { id = "sent-1" }, new { id = "sent-2" } } : [new { id = "sent-1" }] })
            : Host.Json(new { id = "sent-1", threadId = "thread-1", labelIds = evidence == "not-sent" ? new[] { "INBOX" } : ["SENT"], raw = Convert.ToBase64String(raw) });

        ConnectorMutationReconciliation result = await host.Provider.ReconcileAsync(host.Call(GmailActionKeys.SendMessage,
            new GmailSendRequest("send-1", prepared.MessageReference, prepared.ContentHash)));

        result.Result.HasValue.Should().Be(completed); host.Writes.Should().BeEmpty();
        if (completed) ConnectorJson.Read<GmailMutationReceipt>(result.Result!.Value).Reconciled.Should().BeTrue();
    }

    [Fact]
    public async Task BatchDelete_Should_UseOnlyTheExactReviewedIds_AndAcceptAnEmpty204()
    {
        using var host = new Host();
        host.Response = _ => new(HttpStatusCode.NoContent);
        var call = host.Call(GmailActionKeys.PermanentDelete, new GmailMessageTargets("delete-1", ["message-1", "message-2"]));

        GmailMutationReceipt result = ConnectorJson.Read<GmailMutationReceipt>(await host.Provider.InvokeAsync(call));

        result.MessageIds.Should().Equal("message-1", "message-2");
        host.Writes.Should().ContainSingle().Which.Path.Should().EndWith("/messages/batchDelete");
        JsonDocument.Parse(host.Writes.Single().Body).RootElement.GetProperty("ids").EnumerateArray().Select(item => item.GetString()).Should().Equal("message-1", "message-2");
    }

    [Fact]
    public async Task PartialTrash_Should_HoldForReconciliationAndNeverReplayProviderWrites()
    {
        using var host = new Host();
        host.Response = request => request.RequestUri!.AbsolutePath.EndsWith("message-1/trash", StringComparison.Ordinal)
            ? Host.Json(new { id = "message-1", labelIds = new[] { "TRASH" } }) : new(HttpStatusCode.ServiceUnavailable);
        var call = host.Call(GmailActionKeys.Trash, new GmailMessageTargets("trash-1", ["message-1", "message-2"]));
        await ((Func<Task>)(() => host.Provider.InvokeAsync(call))).Should().ThrowAsync<ConnectorProviderException>();
        host.Response = request => Host.Json(new { id = request.RequestUri!.AbsolutePath.Split('/').Last(), labelIds = request.RequestUri.AbsolutePath.EndsWith("message-1", StringComparison.Ordinal) ? new[] { "TRASH" } : [] });

        (await host.Provider.ReconcileAsync(call)).Result.Should().BeNull();

        host.Writes.Should().HaveCount(2);
        host.Response = request => Host.Json(new { id = request.RequestUri!.AbsolutePath.Split('/').Last(), labelIds = new[] { "TRASH" } });
        (await host.Provider.ReconcileAsync(call)).Result.Should().NotBeNull();
        host.Writes.Should().HaveCount(2);
    }

    private static byte[] Decode(string input) => Convert.FromBase64String(input.Replace('-', '+').Replace('_', '/').PadRight((input.Length + 3) / 4 * 4, '='));

    private sealed class Host : HttpMessageHandler, IHttpClientFactory
    {
        public Guid ConnectionId { get; } = Guid.NewGuid();
        public Dictionary<string, byte[]> Content { get; } = [];
        private Dictionary<string, ConnectorStoredContent> Metadata { get; } = [];
        public List<(string Method, string Path, string Body)> Requests { get; } = [];
        public IEnumerable<(string Method, string Path, string Body)> Writes => Requests.Where(request => request.Method != "GET");
        public Func<HttpRequestMessage, HttpResponseMessage> Response { get; set; } = _ => Json(new { id = "sent-1", threadId = "thread-1" });
        public GmailActionProvider Provider { get; }
        public Host(ClamAvScanner? scanner = null)
        {
            var store = new Mock<IConnectorContentStore>();
            store.Setup(value => value.SaveAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid connection, string source, string? attachment, byte[] bytes, string name, string type, CancellationToken _) =>
                {
                    string reference = Guid.NewGuid().ToString(); Content.Add(reference, bytes);
                    var result = new ConnectorStoredContent(reference, connection, source, attachment, name, type, bytes.Length,
                        Convert.ToHexStringLower(SHA256.HashData(bytes)), DateTime.UtcNow.AddDays(7)); Metadata.Add(reference, result); return result;
                });
            store.Setup(value => value.Describe(It.IsAny<string>())).Returns((string reference) => Metadata[reference]);
            store.Setup(value => value.OpenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((string reference, CancellationToken _) => (Stream)new MemoryStream(Content[reference]));
            Provider = new(this, store.Object, scanner ?? new ClamAvScanner(Options.Create(new ClamAvOptions())));
        }
        public ConnectorProviderCall Call<T>(string key, T arguments) => new(ConnectionId, "provider-account", "synthetic", new(key, 1), ConnectorJson.Element(arguments), Guid.NewGuid(), "owner@example.com");
        public async Task<GmailPreparedMessage> Prepare(IReadOnlyList<string>? attachments = null) => ConnectorJson.Read<GmailPreparedMessage>(await Provider.InvokeAsync(Call(GmailActionKeys.PrepareMessage,
            new GmailPrepareMessageRequest(GmailActionKeys.SendMessage, ["to@example.com"], "Invoice", "reviewed body", Bcc: ["hidden@example.com"], AttachmentReferences: attachments))));
        public string StoreAttachment(byte[] bytes)
        {
            string id = Guid.NewGuid().ToString(); Content.Add(id, bytes);
            Metadata.Add(id, new(id, ConnectionId, "message-1", "attachment-1", "invoice.pdf", "application/pdf", bytes.Length, Convert.ToHexStringLower(SHA256.HashData(bytes)), DateTime.UtcNow.AddDays(7)));
            return id;
        }
        public byte[] Raw(GmailPreparedMessage message) => Convert.FromBase64String(JsonDocument.Parse(Content[message.MessageReference]).RootElement.GetProperty("rawBase64").GetString()!);
        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);
        public static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK) { Content = new StringContent(ConnectorJson.Serialize(value)) };
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Requests.Add((request.Method.Method, request.RequestUri!.AbsolutePath, request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken))); return Response(request); }
    }
}
