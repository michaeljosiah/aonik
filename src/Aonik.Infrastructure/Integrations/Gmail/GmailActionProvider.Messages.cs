using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using MimeKit;

using Aonik.Platform.Contracts.Models.ContactEnquiries;
using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Infrastructure.Integrations.Gmail;

internal sealed partial class GmailActionProvider
{
    private const int MaximumPreparedAttachmentBytes = 5 * 1024 * 1024;
    private const int MaximumMimeBytes = 8 * 1024 * 1024;
    private const string ReviewedMessageType = "application/vnd.aonik.gmail-message+json";

    private async Task<GmailPreparedMessage> PrepareMessageAsync(ConnectorProviderCall call, CancellationToken cancellationToken)
    {
        GmailPrepareMessageRequest input = ConnectorJson.Read<GmailPrepareMessageRequest>(call.Arguments);
        if (string.IsNullOrWhiteSpace(call.ProviderAccountLabel)) throw new ConnectorProviderException("ProviderIdentityRequired");
        using var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(call.ProviderAccountLabel));
        foreach (string recipient in input.To) message.To.Add(MailboxAddress.Parse(recipient));
        foreach (string recipient in input.Cc ?? []) message.Cc.Add(MailboxAddress.Parse(recipient));
        foreach (string recipient in input.Bcc ?? []) message.Bcc.Add(MailboxAddress.Parse(recipient));
        message.Subject = input.Subject; message.MessageId = call.OperationId.ToString("N") + "@aonik.invalid";
        message.Date = new DateTimeOffset(DateTime.SpecifyKind(clock?.UtcNow ?? DateTime.UtcNow, DateTimeKind.Utc));
        string body = input.Body; string? threadId = null;
        if (input.SourceMessageId is { } sourceId)
        {
            GmailMessageResult source = await MessageAsync(call, sourceId, cancellationToken);
            if (source.Truncated) throw new ConnectorProviderException("SourceMessageTooLarge");
            if (input.TargetAction == GmailActionKeys.ReplyMessage)
            {
                string? original = source.Headers.GetValueOrDefault("Message-ID")?.Trim('<', '>');
                if (original is null || original.Length > 254 || original.Any(char.IsControl) || original.Contains(' ') || !original.Contains('@'))
                    throw new ConnectorProviderException("ReplyReferenceUnavailable");
                message.InReplyTo = original; message.References.Add(original); threadId = source.ThreadId;
                string subject = source.Headers.GetValueOrDefault("Subject") ?? "";
                if (!string.Equals(input.Subject, subject, StringComparison.Ordinal) && !string.Equals(input.Subject, "Re: " + subject, StringComparison.Ordinal))
                    throw new ConnectorProviderException("ReplySubjectChanged");
            }
            else body += "\n\nForwarded message\nFrom: " + source.Headers.GetValueOrDefault("From") + "\nSubject: "
                + source.Headers.GetValueOrDefault("Subject") + "\n\n" + string.Join('\n', source.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Select(line => "> " + line));
        }
        if (body.Length > 16_384) throw new ConnectorProviderException("PreparedBodyTooLarge");
        var builder = new BodyBuilder { TextBody = body }; int total = 0;
        foreach (string reference in input.AttachmentReferences ?? [])
        {
            ConnectorStoredContent metadata = contentStore.Describe(reference);
            if (metadata.ConnectionId != call.ConnectionId || metadata.SizeBytes is < 1 or > MaximumPreparedAttachmentBytes)
                throw new ConnectorAccessException("AttachmentBindingMismatch", "The attachment must belong to the selected account and fit the reviewed size bound.");
            total = checked(total + (int)metadata.SizeBytes);
            if (total > MaximumPreparedAttachmentBytes) throw new ConnectorProviderException("AttachmentTooLarge");
            await using Stream file = await contentStore.OpenAsync(reference, cancellationToken);
            byte[] bytes = await ReadBoundedAsync(file, (int)metadata.SizeBytes, cancellationToken);
            if (HashBytes(bytes) != metadata.Sha256) throw new ConnectorProviderException("AttachmentHashMismatch");
            string mime = SafeType(bytes, metadata.ContentType); await ScanPreparedAsync(bytes, cancellationToken);
            builder.Attachments.Add(metadata.FileName, bytes, ContentType.Parse(mime));
        }
        message.Body = builder.ToMessageBody();
        using var stream = new MemoryStream(); var format = FormatOptions.Default.Clone(); format.NewLineFormat = NewLineFormat.Dos;
        format.HiddenHeaders.Remove(HeaderId.Bcc);
        await message.WriteToAsync(format, stream, cancellationToken);
        byte[] raw = stream.ToArray();
        using MimeMessage reviewed = ParseMime(raw);
        GmailMessageEvidence evidence = await EvidenceAsync(reviewed, cancellationToken);
        return await StoreMessageAsync(call, new(1, input.TargetAction, Convert.ToBase64String(raw), HashBytes(raw), evidence,
            threadId, input.SourceMessageId), cancellationToken);
    }

    private async Task<GmailDraftResult> DraftAsync(ConnectorProviderCall call, string draftId, CancellationToken cancellationToken)
    {
        GmailRawDraft draft = await RawDraftAsync(call, draftId, cancellationToken);
        using MimeMessage message = ParseMime(draft.Raw);
        GmailMessageEvidence evidence = await EvidenceAsync(message, cancellationToken);
        GmailPreparedMessage reviewed = await StoreMessageAsync(call, new(1, GmailActionKeys.SendDraft, Convert.ToBase64String(draft.Raw), HashBytes(draft.Raw), evidence,
            draft.ThreadId, null), cancellationToken);
        return new(draftId, draft.MessageId, draft.ThreadId, HashBytes(draft.Raw), reviewed.MessageReference, reviewed.ContentHash);
    }

    private async Task<GmailRawDraft> RawDraftAsync(ConnectorProviderCall call, string draftId, CancellationToken cancellationToken)
    {
        JsonElement json = await GetAsync(call, "drafts/" + Uri.EscapeDataString(draftId) + "?format=raw", 12 * 1024 * 1024, cancellationToken);
        if (Text(json, "id", 256) != draftId || !json.TryGetProperty("message", out JsonElement message)) throw new ConnectorProviderException("ProviderItemMismatch");
        return new(draftId, Text(message, "id", 256), Text(message, "threadId", 256), Decode(Text(message, "raw", 12 * 1024 * 1024), MaximumMimeBytes));
    }

    private async Task<GmailPreparedMessage> StoreMessageAsync(ConnectorProviderCall call, GmailPreparedEnvelope envelope, CancellationToken cancellationToken)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(ConnectorJson.Serialize(envelope));
        ConnectorStoredContent stored = await contentStore.SaveAsync(call.ConnectionId, envelope.Evidence.MessageId, null, bytes,
            "reviewed-message.json", ReviewedMessageType, cancellationToken);
        return new(stored.FileReference, stored.Sha256, envelope.Evidence.MessageId, envelope.TargetAction);
    }

    private async Task<GmailPreparedEnvelope> ReviewedMessageAsync(ConnectorProviderCall call, string reference, string expectedHash, CancellationToken cancellationToken)
    {
        ConnectorStoredContent metadata = contentStore.Describe(reference);
        if (metadata.ConnectionId != call.ConnectionId || metadata.ContentType != ReviewedMessageType || metadata.Sha256 != expectedHash
            || metadata.SizeBytes is < 1 or > 10 * 1024 * 1024) throw new ConnectorAccessException("PreparedMessageMismatch", "The reviewed message reference, account and hash must match exactly.");
        await using Stream input = await contentStore.OpenAsync(reference, cancellationToken);
        byte[] bytes = await ReadBoundedAsync(input, (int)metadata.SizeBytes, cancellationToken);
        if (HashBytes(bytes) != expectedHash) throw new ConnectorProviderException("PreparedMessageHashMismatch");
        GmailPreparedEnvelope envelope = ConnectorJson.Read<GmailPreparedEnvelope>(Encoding.UTF8.GetString(bytes));
        if (envelope.SchemaVersion != 1 || envelope.TargetAction != call.Action.Key || envelope.RawBase64.Length > 12 * 1024 * 1024)
            throw new ConnectorAccessException("PreparedMessageMismatch", "Prepare this exact message for the selected action.");
        byte[] raw = Convert.FromBase64String(envelope.RawBase64);
        if (raw.Length > MaximumMimeBytes || HashBytes(raw) != envelope.RawHash) throw new ConnectorProviderException("PreparedMessageHashMismatch");
        using MimeMessage parsed = ParseMime(raw);
        GmailMessageEvidence actual = await EvidenceAsync(parsed, cancellationToken);
        if (ConnectorJson.Serialize(actual) != ConnectorJson.Serialize(envelope.Evidence) || !string.Equals(actual.From, call.ProviderAccountLabel, StringComparison.OrdinalIgnoreCase))
            throw new ConnectorAccessException("PreparedSenderMismatch", "The immutable message must use the verified account sender and reviewed content.");
        return envelope;
    }

    private async Task<GmailMessageEvidence> EvidenceAsync(MimeMessage message, CancellationToken cancellationToken)
    {
        if (message.Headers.Count > 100 || message.BodyParts.Take(21).Count() > 20 || message.HtmlBody is not null
            || message.Subject?.Length > 998 || message.TextBody?.Length > 16_384 || string.IsNullOrEmpty(message.MessageId)
            || message.From.Mailboxes.Count() != 1 || message.To.Mailboxes.Count() + message.Cc.Mailboxes.Count() + message.Bcc.Mailboxes.Count() is < 1 or > 50)
            throw new ConnectorProviderException("DraftContentUnsupported");
        string[] boundHeaders = ["From", "To", "Cc", "Bcc", "Subject", "Message-Id", "In-Reply-To", "References"];
        if (boundHeaders.Any(name => message.Headers.Count(header => header.Field.Equals(name, StringComparison.OrdinalIgnoreCase)) > 1)
            || message.Headers.Any(header => header.Field.StartsWith("Resent-", StringComparison.OrdinalIgnoreCase)
                || header.Id is HeaderId.Sender or HeaderId.ReplyTo)
            || message.BodyParts.Count(part => !part.IsAttachment && part is TextPart) != 1
            || message.BodyParts.OfType<TextPart>().Any(part => !part.IsAttachment && !part.IsPlain))
            throw new ConnectorProviderException("DraftContentUnsupported");
        var attachments = new List<GmailReviewedAttachment>(); long total = 0;
        foreach (MimeEntity entity in message.BodyParts.Where(part => part.IsAttachment || part is not TextPart))
        {
            if (entity is not MimePart part || attachments.Count >= 5 || part.Content is null) throw new ConnectorProviderException("DraftContentUnsupported");
            using var file = new MemoryStream(); await part.Content.DecodeToAsync(file, cancellationToken); byte[] bytes = file.ToArray();
            total += bytes.Length; if (total > MaximumPreparedAttachmentBytes) throw new ConnectorProviderException("AttachmentTooLarge");
            string type = SafeType(bytes, part.ContentType.MimeType); await ScanPreparedAsync(bytes, cancellationToken);
            string fileName = FileName(part.FileName ?? "", type);
            if (fileName != part.FileName) throw new ConnectorProviderException("DraftContentUnsupported");
            attachments.Add(new(fileName, type, bytes.Length, HashBytes(bytes)));
        }
        return new(message.MessageId, message.From.Mailboxes.Single().Address.ToLowerInvariant(), Addresses(message.To), Addresses(message.Cc), Addresses(message.Bcc),
            message.Subject ?? "", (message.TextBody ?? "").Replace("\r\n", "\n", StringComparison.Ordinal), message.InReplyTo, message.References.ToArray(),
            attachments.OrderBy(item => item.FileName, StringComparer.Ordinal).ThenBy(item => item.Sha256, StringComparer.Ordinal).ToList());
    }

    private async Task ScanPreparedAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        try { await scanner.ScanAsync(bytes, cancellationToken); }
        catch (ContactImageValidationException) { throw new ConnectorProviderException("UnsafeAttachment"); }
        catch (ContactEnquiryUnavailableException) { throw new ConnectorProviderException("AttachmentSafetyUnavailable"); }
    }
    private static MimeMessage ParseMime(byte[] raw)
    {
        using var input = new MemoryStream(raw, writable: false);
        return MimeMessage.Load(new ParserOptions { MaxMimeDepth = 8, MaxHeaderLength = 8192, MaxAddressGroupDepth = 1 }, input);
    }
    private static string[] Addresses(InternetAddressList addresses) => addresses.Mailboxes.Select(address => address.Address.ToLowerInvariant()).Order(StringComparer.Ordinal).ToArray();
    private static string HashBytes(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static async Task<byte[]> ReadBoundedAsync(Stream input, int expectedBytes, CancellationToken cancellationToken)
    {
        using var output = new MemoryStream(); byte[] buffer = new byte[8192]; int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
        { if (output.Length + read > expectedBytes) throw new ConnectorProviderException("ContentSizeMismatch"); await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken); }
        if (output.Length != expectedBytes) throw new ConnectorProviderException("ContentSizeMismatch"); return output.ToArray();
    }
}

internal sealed record GmailRawDraft(string DraftId, string MessageId, string ThreadId, byte[] Raw);
internal sealed record GmailReviewedAttachment(string FileName, string ContentType, int SizeBytes, string Sha256);
internal sealed record GmailMessageEvidence(string MessageId, string From, IReadOnlyList<string> To, IReadOnlyList<string> Cc,
    IReadOnlyList<string> Bcc, string Subject, string Body, string? InReplyTo, IReadOnlyList<string> References, IReadOnlyList<GmailReviewedAttachment> Attachments);
internal sealed record GmailPreparedEnvelope(int SchemaVersion, string TargetAction, string RawBase64, string RawHash,
    GmailMessageEvidence Evidence, string? ThreadId, string? SourceMessageId);
