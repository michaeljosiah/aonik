using System.Net.Mail;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Aonik.SharedKernel.Abstractions.Connectors;

public static class GmailActionContracts
{
    public static JsonElement Normalize(ConnectorActionRef action, JsonElement arguments)
    {
        RequireUniqueProperties(arguments);
        if (action.Version != 1) throw new ArgumentException("Unsupported Gmail action version.");
        JsonElement normalized = action.Key switch
        {
            GmailActionKeys.PrepareMessage => ConnectorJson.Element(ConnectorJson.Read<GmailPrepareMessageRequest>(arguments)),
            GmailActionKeys.GetDraft => ConnectorJson.Element(ConnectorJson.Read<GmailGetDraftRequest>(arguments)),
            GmailActionKeys.CreateDraft or GmailActionKeys.SendMessage or GmailActionKeys.ReplyMessage or GmailActionKeys.ForwardMessage
                => ConnectorJson.Element(ConnectorJson.Read<GmailSendRequest>(arguments)),
            GmailActionKeys.UpdateDraft or GmailActionKeys.SendDraft => ConnectorJson.Element(ConnectorJson.Read<GmailDraftMessageRequest>(arguments)),
            GmailActionKeys.DeleteDraft => ConnectorJson.Element(ConnectorJson.Read<GmailDraftTargetRequest>(arguments)),
            GmailActionKeys.ModifyLabels => ConnectorJson.Element(ConnectorJson.Read<GmailModifyLabelsRequest>(arguments)),
            GmailActionKeys.MarkRead or GmailActionKeys.MarkUnread or GmailActionKeys.Archive or GmailActionKeys.Trash or GmailActionKeys.Restore or GmailActionKeys.PermanentDelete
                => ConnectorJson.Element(ConnectorJson.Read<GmailMessageTargets>(arguments)),
            _ => throw new ArgumentException("Unsupported Gmail mutation contract."),
        };
        Validate(action, normalized); return normalized;
    }

    private static void RequireUniqueProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (JsonProperty property in value.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new ArgumentException("Connector arguments cannot contain duplicate or case-aliased properties.");
                RequireUniqueProperties(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (JsonElement item in value.EnumerateArray()) RequireUniqueProperties(item);
    }

    public static void Validate(ConnectorActionRef action, JsonElement arguments)
    {
        if (action.Version != 1) throw new ArgumentException("Unsupported Gmail action version.");
        if (action.Key == GmailActionKeys.PrepareMessage)
        {
            GmailPrepareMessageRequest input = ConnectorJson.Read<GmailPrepareMessageRequest>(arguments);
            if (input.TargetAction is not (GmailActionKeys.CreateDraft or GmailActionKeys.UpdateDraft or GmailActionKeys.SendMessage or GmailActionKeys.ReplyMessage or GmailActionKeys.ForwardMessage)
                || input.To is null || input.Subject is null || input.Subject.Length > 998 || input.Subject.Any(char.IsControl)
                || input.Body is null || input.Body.Length > 16_384 || input.Body.Contains('\0')) throw new ArgumentException("Select a supported target action, bounded subject and plain-text body.");
            List<string> recipients = [.. input.To, .. input.Cc ?? [], .. input.Bcc ?? []];
            if (recipients.Count is < 1 or > 50 || recipients.Any(value => value is null || value.Length > 254 || value.Any(char.IsControl)
                    || !MailAddress.TryCreate(value, out MailAddress? address) || !string.Equals(value, address.Address, StringComparison.OrdinalIgnoreCase))
                || recipients.Distinct(StringComparer.OrdinalIgnoreCase).Count() != recipients.Count)
                throw new ArgumentException("Use 1–50 distinct plain email addresses across To/Cc/Bcc.");
            if (input.AttachmentReferences?.Count > 5 || input.AttachmentReferences?.Any(reference => reference is not { Length: > 0 and <= 8192 }) == true)
                throw new ArgumentException("Use at most five protected attachment references.");
            if (input.TargetAction is GmailActionKeys.ReplyMessage or GmailActionKeys.ForwardMessage) Identifier(input.SourceMessageId);
            else if (input.SourceMessageId is not null) throw new ArgumentException("Only reply and forward preparation may reference an original message.");
            return;
        }
        if (action.Key == GmailActionKeys.GetDraft) { Identifier(ConnectorJson.Read<GmailGetDraftRequest>(arguments).DraftId); return; }
        if (GmailActionPolicies.IsMutation(action.Key)) _ = IdempotencyKey(arguments);
        switch (action.Key)
        {
            case GmailActionKeys.CreateDraft:
            case GmailActionKeys.SendMessage:
            case GmailActionKeys.ReplyMessage:
            case GmailActionKeys.ForwardMessage:
                GmailSendRequest send = ConnectorJson.Read<GmailSendRequest>(arguments); Message(send.MessageReference, send.ContentHash); break;
            case GmailActionKeys.UpdateDraft:
            case GmailActionKeys.SendDraft:
                GmailDraftMessageRequest draft = ConnectorJson.Read<GmailDraftMessageRequest>(arguments);
                Identifier(draft.DraftId); Hash(draft.ExpectedDraftHash); Message(draft.MessageReference, draft.ContentHash); break;
            case GmailActionKeys.DeleteDraft:
                GmailDraftTargetRequest target = ConnectorJson.Read<GmailDraftTargetRequest>(arguments); Identifier(target.DraftId); Hash(target.ExpectedDraftHash); break;
            case GmailActionKeys.ModifyLabels:
                GmailModifyLabelsRequest labels = ConnectorJson.Read<GmailModifyLabelsRequest>(arguments); Targets(labels.MessageIds);
                if (labels.AddLabels is null || labels.RemoveLabels is null || labels.AddLabels.Count + labels.RemoveLabels.Count is < 1 or > 20
                    || labels.AddLabels.Intersect(labels.RemoveLabels, StringComparer.Ordinal).Any()
                    || labels.AddLabels.Concat(labels.RemoveLabels).Any(label => label is "DRAFT" or "SENT" or "TRASH" || !ValidIdentifier(label, 128))
                    || labels.AddLabels.Distinct(StringComparer.Ordinal).Count() != labels.AddLabels.Count || labels.RemoveLabels.Distinct(StringComparer.Ordinal).Count() != labels.RemoveLabels.Count)
                    throw new ArgumentException("Declare 1–20 distinct label additions/removals. Draft, sent and trash transitions use their explicit actions.");
                break;
            case GmailActionKeys.MarkRead:
            case GmailActionKeys.MarkUnread:
            case GmailActionKeys.Archive:
            case GmailActionKeys.Trash:
            case GmailActionKeys.Restore:
            case GmailActionKeys.PermanentDelete:
                Targets(ConnectorJson.Read<GmailMessageTargets>(arguments).MessageIds); break;
            default: throw new ArgumentException("Unsupported Gmail mutation contract.");
        }
    }

    public static string IdempotencyKey(JsonElement arguments)
    {
        string? key = arguments.TryGetProperty("idempotencyKey", out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        if (!ValidIdentifier(key, 128, allowDot: true)) throw new ArgumentException("A stable operation key of 1–128 letters, digits, dots, underscores or hyphens is required.");
        return key!;
    }
    private static void Targets(IReadOnlyList<string>? ids)
    {
        if (ids is null || ids.Count is < 1 or > 50 || ids.Any(id => !ValidIdentifier(id, 256)) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Count)
            throw new ArgumentException("Select 1–50 distinct provider message IDs; queries never substitute for the reviewed target set.");
    }
    private static void Message(string? reference, string? hash)
    { if (reference is not { Length: > 0 and <= 8192 }) throw new ArgumentException("Prepare the exact immutable message first."); Hash(hash); }
    private static void Hash(string? value)
    { if (value is not { Length: 64 } || !value.All(Uri.IsHexDigit)) throw new ArgumentException("An exact SHA-256 content version is required."); }
    private static void Identifier(string? value)
    { if (!ValidIdentifier(value, 256)) throw new ArgumentException("A valid provider item identifier is required."); }
    private static bool ValidIdentifier(string? value, int maximum, bool allowDot = false) => value is { Length: > 0 } && value.Length <= maximum
        && Regex.IsMatch(value, allowDot ? "^[A-Za-z0-9_.-]+$" : "^[A-Za-z0-9_-]+$", RegexOptions.NonBacktracking);
}
