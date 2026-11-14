namespace Aonik.SharedKernel.Abstractions.Connectors;

public sealed record GmailSearchRequest(string Query, int MaxResults = 20, string? PageToken = null);
public sealed record GmailMessageRef(string Id, string ThreadId);
public sealed record GmailSearchResult(IReadOnlyList<GmailMessageRef> Messages, string? NextPageToken);
public sealed record GmailGetMessageRequest(string MessageId);
public sealed record GmailAttachmentMetadata(string Id, string FileName, string ContentType, long SizeBytes);
public sealed record GmailMessageResult(string Id, string ThreadId, IReadOnlyDictionary<string, string> Headers,
    string Text, bool Truncated, IReadOnlyList<GmailAttachmentMetadata> Attachments, string? ContentReference = null,
    string SourceKind = "UntrustedEmail");
public sealed record GmailGetAttachmentRequest(string MessageId, string AttachmentId);
public sealed record GmailAttachmentResult(string MessageId, string AttachmentId, string FileReference, string ContentType,
    long SizeBytes, string Sha256);

public static class GmailActionKeys
{
    public const string Connector = "gmail";
    public const string ReadScope = "https://www.googleapis.com/auth/gmail.readonly";
    public const string Search = "gmail.messages.search";
    public const string GetMessage = "gmail.messages.get";
    public const string GetAttachment = "gmail.attachments.get";
    public static readonly IReadOnlyList<string> ReadActions = [Search, GetMessage, GetAttachment];
}
