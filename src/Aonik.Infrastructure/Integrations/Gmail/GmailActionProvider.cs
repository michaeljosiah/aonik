using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Aonik.Infrastructure.Storage;
using Aonik.Platform.Contracts.Models.ContactEnquiries;
using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Infrastructure.Integrations.Gmail;

internal sealed class GmailActionProvider(System.Net.Http.IHttpClientFactory clients, IConnectorContentStore contentStore,
    ClamAvScanner scanner) : IConnectorActionProvider
{
    internal const int MaximumAttachmentBytes = 10 * 1024 * 1024;
    private const int MaximumTextBytes = 65_536;
    public string ConnectorKey => GmailActionKeys.Connector;

    public void ValidateArguments(ConnectorActionRef action, JsonElement arguments)
    {
        if (action.Version != 1) throw new ArgumentException("Unsupported Gmail action version.");
        switch (action.Key)
        {
            case GmailActionKeys.Search:
                GmailSearchRequest search = ConnectorJson.Read<GmailSearchRequest>(arguments);
                if (search.Query is null || search.Query.Length > 1024 || search.Query.Any(char.IsControl) || search.MaxResults is < 1 or > 50
                    || search.PageToken?.Length > 4096) throw new ArgumentException("Use a bounded query, page token and 1–50 messages.");
                break;
            case GmailActionKeys.GetMessage:
                Identifier(ConnectorJson.Read<GmailGetMessageRequest>(arguments).MessageId, 256);
                break;
            case GmailActionKeys.GetAttachment:
                GmailGetAttachmentRequest attachment = ConnectorJson.Read<GmailGetAttachmentRequest>(arguments);
                Identifier(attachment.MessageId, 256); Identifier(attachment.AttachmentId, 4096);
                break;
            default: throw new ArgumentException("Unsupported Gmail action.");
        }
    }

    public async Task<JsonElement> InvokeAsync(ConnectorProviderCall call, CancellationToken cancellationToken = default)
    {
        ValidateArguments(call.Action, call.Arguments);
        return call.Action.Key switch
        {
            GmailActionKeys.Search => ConnectorJson.Element(await SearchAsync(call, cancellationToken)),
            GmailActionKeys.GetMessage => ConnectorJson.Element(await MessageAsync(call, ConnectorJson.Read<GmailGetMessageRequest>(call.Arguments).MessageId, cancellationToken)),
            GmailActionKeys.GetAttachment => ConnectorJson.Element(await AttachmentAsync(call, cancellationToken)),
            _ => throw new ArgumentException("Unsupported Gmail action."),
        };
    }

    private async Task<GmailSearchResult> SearchAsync(ConnectorProviderCall call, CancellationToken cancellationToken)
    {
        GmailSearchRequest input = ConnectorJson.Read<GmailSearchRequest>(call.Arguments);
        string path = $"messages?maxResults={input.MaxResults}&q={Uri.EscapeDataString(input.Query)}&fields=messages(id,threadId),nextPageToken";
        if (input.PageToken is { Length: > 0 } page) path += "&pageToken=" + Uri.EscapeDataString(page);
        JsonElement json = await GetAsync(call, path, 128 * 1024, cancellationToken);
        var messages = new List<GmailMessageRef>();
        if (json.TryGetProperty("messages", out JsonElement array))
            foreach (JsonElement item in array.EnumerateArray().Take(input.MaxResults))
                messages.Add(new(Text(item, "id", 256), Text(item, "threadId", 256)));
        return new(messages, OptionalText(json, "nextPageToken", 4096));
    }

    private async Task<GmailMessageResult> MessageAsync(ConnectorProviderCall call, string messageId, CancellationToken cancellationToken)
    {
        JsonElement json = await GetAsync(call, $"messages/{Uri.EscapeDataString(messageId)}?format=full&fields=id,threadId,payload", 2 * 1024 * 1024, cancellationToken);
        string id = Text(json, "id", 256);
        if (id != messageId) throw new ConnectorProviderException("ProviderItemMismatch");
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var attachments = new List<GmailAttachmentMetadata>();
        var plain = new StringBuilder();
        var html = new StringBuilder();
        bool truncated = false;
        int parts = 0;
        if (json.TryGetProperty("payload", out JsonElement payload))
        {
            if (payload.TryGetProperty("headers", out JsonElement sourceHeaders))
                foreach (JsonElement header in sourceHeaders.EnumerateArray().Take(100))
                {
                    string name = Text(header, "name", 100);
                    if (name.Equals("Subject", StringComparison.OrdinalIgnoreCase) || name.Equals("From", StringComparison.OrdinalIgnoreCase)
                        || name.Equals("To", StringComparison.OrdinalIgnoreCase) || name.Equals("Date", StringComparison.OrdinalIgnoreCase)
                        || name.Equals("Message-ID", StringComparison.OrdinalIgnoreCase))
                        headers.TryAdd(name, Clean(Text(header, "value", 2048)));
                }
            Visit(payload, 0);
        }
        string text = plain.Length > 0 ? plain.ToString() : WebUtility.HtmlDecode(Regex.Replace(html.ToString(), "<[^>]+>", " ", RegexOptions.NonBacktracking));
        byte[] encoded = Encoding.UTF8.GetBytes(Clean(text));
        if (encoded.Length > MaximumTextBytes) truncated = true;
        int byteCount = Math.Min(encoded.Length, MaximumTextBytes);
        while (byteCount > 0 && byteCount < encoded.Length && (encoded[byteCount] & 0xc0) == 0x80) byteCount--;
        text = Encoding.UTF8.GetString(encoded, 0, byteCount);
        return new(id, Text(json, "threadId", 256), headers, text, truncated, attachments);

        void Visit(JsonElement part, int depth)
        {
            if (++parts > 100 || depth > 12) { truncated = true; return; }
            string mime = OptionalText(part, "mimeType", 100) ?? "";
            string fileName = OptionalText(part, "filename", 500) ?? "";
            if (part.TryGetProperty("body", out JsonElement body))
            {
                string? attachmentId = OptionalText(body, "attachmentId", 4096);
                long size = body.TryGetProperty("size", out JsonElement length) && length.TryGetInt64(out long count) ? count : 0;
                if (attachmentId is not null) attachments.Add(new(attachmentId, FileName(fileName, mime), mime, size));
                if (fileName.Length == 0 && (mime == "text/plain" || mime == "text/html") && body.TryGetProperty("data", out JsonElement data))
                {
                    byte[] decoded = Decode(data.GetString() ?? "", 2 * 1024 * 1024);
                    StringBuilder target = mime == "text/plain" ? plain : html;
                    int remaining = MaximumTextBytes - target.Length;
                    if (decoded.Length > remaining) truncated = true;
                    if (remaining > 0) target.Append(Encoding.UTF8.GetString(decoded, 0, Math.Min(decoded.Length, remaining)));
                }
            }
            if (part.TryGetProperty("parts", out JsonElement children)) foreach (JsonElement child in children.EnumerateArray()) Visit(child, depth + 1);
        }
    }

    private async Task<GmailAttachmentResult> AttachmentAsync(ConnectorProviderCall call, CancellationToken cancellationToken)
    {
        GmailGetAttachmentRequest input = ConnectorJson.Read<GmailGetAttachmentRequest>(call.Arguments);
        GmailMessageResult message = await MessageAsync(call, input.MessageId, cancellationToken);
        GmailAttachmentMetadata metadata = message.Attachments.SingleOrDefault(attachment => attachment.Id == input.AttachmentId)
            ?? throw new ConnectorProviderException("ProviderItemNotFound");
        if (metadata.SizeBytes is < 1 or > MaximumAttachmentBytes) throw new ConnectorProviderException("AttachmentTooLarge");
        JsonElement json = await GetAsync(call, $"messages/{Uri.EscapeDataString(input.MessageId)}/attachments/{Uri.EscapeDataString(input.AttachmentId)}",
            14 * 1024 * 1024 + 4096, cancellationToken);
        byte[] bytes = Decode(Text(json, "data", 14 * 1024 * 1024), MaximumAttachmentBytes);
        if (bytes.Length != metadata.SizeBytes) throw new ConnectorProviderException("AttachmentSizeMismatch");
        string mime = SafeType(bytes, metadata.ContentType);
        try { await scanner.ScanAsync(bytes, cancellationToken); }
        catch (ContactImageValidationException) { throw new ConnectorProviderException("UnsafeAttachment"); }
        catch (ContactEnquiryUnavailableException) { throw new ConnectorProviderException("AttachmentSafetyUnavailable"); }
        ConnectorStoredContent stored = await contentStore.SaveAsync(call.ConnectionId, input.MessageId, input.AttachmentId, bytes,
            FileName(metadata.FileName, mime), mime, cancellationToken);
        return new(input.MessageId, input.AttachmentId, stored.FileReference, stored.ContentType, stored.SizeBytes, stored.Sha256);
    }

    private Task<JsonElement> GetAsync(ConnectorProviderCall call, string path, int maxBytes, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://gmail.googleapis.com/gmail/v1/users/me/" + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", call.AccessToken);
        return SendAsync();
        async Task<JsonElement> SendAsync()
        {
            using (request) return await GmailHttp.JsonAsync(clients.CreateClient("Gmail"), request, maxBytes, cancellationToken);
        }
    }
    private static void Identifier(string value, int max)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > max || !Regex.IsMatch(value, "^[a-zA-Z0-9_-]+$", RegexOptions.NonBacktracking))
            throw new ArgumentException("A valid provider item identifier is required.");
    }
    private static string Text(JsonElement json, string name, int max) => OptionalText(json, name, max)
        ?? throw new ConnectorProviderException("ProviderResponseInvalid");
    private static string? OptionalText(JsonElement json, string name, int max) => json.TryGetProperty(name, out JsonElement value)
        && value.ValueKind == JsonValueKind.String ? value.GetString() is { } text && text.Length <= max ? text
            : throw new ConnectorProviderException("ProviderResponseTooLarge") : null;
    private static byte[] Decode(string value, int max)
    {
        if (value.Length > (long)max * 4 / 3 + 4) throw new ConnectorProviderException("ProviderResponseTooLarge");
        try
        {
            byte[] bytes = Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/').PadRight((value.Length + 3) / 4 * 4, '='));
            if (bytes.Length > max) throw new ConnectorProviderException("ProviderResponseTooLarge");
            return bytes;
        }
        catch (FormatException) { throw new ConnectorProviderException("ProviderResponseInvalid"); }
    }
    private static string Clean(string value) => new(value.Where(character => !char.IsControl(character) || character is '\n' or '\r' or '\t').ToArray());
    private static string FileName(string value, string mime)
    {
        string safe = Clean(value.Replace('\\', '/').Split('/').Last()).Replace("\r", "").Replace("\n", "").Trim();
        return safe.Length > 0 ? safe[..Math.Min(safe.Length, 160)] : mime == "application/pdf" ? "attachment.pdf" : "attachment";
    }
    private static string SafeType(byte[] content, string declared)
    {
        if (declared == "application/pdf" && content.AsSpan().StartsWith("%PDF-"u8)) return declared;
        if (declared == "image/png" && content.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return declared;
        if (declared == "image/jpeg" && content.Length >= 3 && content[0] == 0xff && content[1] == 0xd8 && content[2] == 0xff) return declared;
        if (declared == "text/plain" && !content.Contains((byte)0))
        {
            try { _ = new UTF8Encoding(false, true).GetString(content); return declared; }
            catch (DecoderFallbackException) { }
        }
        throw new ConnectorProviderException("AttachmentTypeUnsupported");
    }
}
