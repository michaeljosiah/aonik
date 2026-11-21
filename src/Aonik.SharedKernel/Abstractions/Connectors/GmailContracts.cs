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
public sealed record GmailSourceMessageResult(GmailMessageResult Message, DateTime ReceivedAtUtc, IReadOnlyList<string> LabelIds);

public static class GmailActionKeys
{
    public const string Connector = "gmail";
    public const string ReadScope = "https://www.googleapis.com/auth/gmail.readonly";
    public const string SendScope = "https://www.googleapis.com/auth/gmail.send";
    public const string ComposeScope = "https://www.googleapis.com/auth/gmail.compose";
    public const string ModifyScope = "https://www.googleapis.com/auth/gmail.modify";
    public const string FullScope = "https://mail.google.com/";
    public const string Search = "gmail.messages.search";
    public const string GetMessage = "gmail.messages.get";
    public const string GetAttachment = "gmail.attachments.get";
    public const string GetDraft = "gmail.drafts.get";
    public const string PrepareMessage = "gmail.messages.prepare";
    public const string CreateDraft = "gmail.drafts.create";
    public const string UpdateDraft = "gmail.drafts.update";
    public const string DeleteDraft = "gmail.drafts.delete";
    public const string SendMessage = "gmail.messages.send";
    public const string ReplyMessage = "gmail.messages.reply";
    public const string ForwardMessage = "gmail.messages.forward";
    public const string SendDraft = "gmail.drafts.send";
    public const string ModifyLabels = "gmail.messages.modify-labels";
    public const string MarkRead = "gmail.messages.mark-read";
    public const string MarkUnread = "gmail.messages.mark-unread";
    public const string Archive = "gmail.messages.archive";
    public const string Trash = "gmail.messages.trash";
    public const string Restore = "gmail.messages.restore";
    public const string PermanentDelete = "gmail.messages.permanently-delete";
    public static readonly IReadOnlyList<string> ReadActions = [Search, GetMessage, GetAttachment];
}

public sealed record GmailPrepareMessageRequest(string TargetAction, IReadOnlyList<string> To, string Subject, string Body,
    IReadOnlyList<string>? Cc = null, IReadOnlyList<string>? Bcc = null, IReadOnlyList<string>? AttachmentReferences = null,
    string? SourceMessageId = null);
public sealed record GmailPreparedMessage(string MessageReference, string ContentHash, string Rfc822MessageId, string TargetAction);
public sealed record GmailSendRequest(string IdempotencyKey, string MessageReference, string ContentHash);
public sealed record GmailGetDraftRequest(string DraftId);
public sealed record GmailDraftResult(string DraftId, string MessageId, string ThreadId, string ContentHash, string MessageReference, string ReviewedContentHash);
public sealed record GmailDraftMessageRequest(string IdempotencyKey, string DraftId, string ExpectedDraftHash, string MessageReference, string ContentHash);
public sealed record GmailDraftTargetRequest(string IdempotencyKey, string DraftId, string ExpectedDraftHash);
public sealed record GmailMessageTargets(string IdempotencyKey, IReadOnlyList<string> MessageIds);
public sealed record GmailModifyLabelsRequest(string IdempotencyKey, IReadOnlyList<string> MessageIds, IReadOnlyList<string> AddLabels, IReadOnlyList<string> RemoveLabels);
public sealed record GmailMutationReceipt(string ActionKey, Guid OperationId, string Status, IReadOnlyList<string> MessageIds,
    string? DraftId = null, string? ThreadId = null, string? ContentHash = null, bool Reconciled = false);
