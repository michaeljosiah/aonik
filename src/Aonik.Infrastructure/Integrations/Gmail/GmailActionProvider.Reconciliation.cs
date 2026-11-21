using System.Text.Json;

using MimeKit;

using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Infrastructure.Integrations.Gmail;

internal sealed partial class GmailActionProvider
{
    public async Task<ConnectorMutationReconciliation> ReconcileAsync(ConnectorProviderCall call, CancellationToken cancellationToken = default)
    {
        ValidateArguments(call.Action, call.Arguments);
        string key = call.Action.Key;
        GmailPreparedEnvelope? message = await PreparedAsync(call, cancellationToken);
        if (message is not null)
        {
            string query = Uri.EscapeDataString("rfc822msgid:" + message.Evidence.MessageId);
            if (key is GmailActionKeys.CreateDraft or GmailActionKeys.UpdateDraft)
            {
                JsonElement drafts = await GetAsync(call, "drafts?maxResults=3&q=" + query, 32 * 1024, cancellationToken);
                if (!drafts.TryGetProperty("drafts", out JsonElement items) || items.GetArrayLength() != 1) return new(null);
                string id = Text(items[0], "id", 256);
                if (key == GmailActionKeys.UpdateDraft && id != ConnectorJson.Read<GmailDraftMessageRequest>(call.Arguments).DraftId) return new(null);
                GmailRawDraft draft = await RawDraftAsync(call, id, cancellationToken);
                using MimeMessage parsed = ParseMime(draft.Raw);
                if (!SameEvidence(await EvidenceAsync(parsed, cancellationToken), message.Evidence)) return new(null);
                return new(ConnectorJson.Element(Receipt(call, [draft.MessageId], id, draft.ThreadId, message.RawHash, reconciled: true)));
            }
            // An RFC Message-ID is only a lookup key. A unique authenticated SENT item must also match all reviewed content.
            JsonElement sent = await GetAsync(call, "messages?maxResults=3&includeSpamTrash=true&q=" + query, 32 * 1024, cancellationToken);
            if (!sent.TryGetProperty("messages", out JsonElement candidates) || candidates.GetArrayLength() != 1) return new(null);
            string sentId = Text(candidates[0], "id", 256);
            JsonElement source = await GetAsync(call, "messages/" + Uri.EscapeDataString(sentId) + "?format=raw&fields=id,threadId,labelIds,raw", 12 * 1024 * 1024, cancellationToken);
            string? threadId = OptionalText(source, "threadId", 256);
            if (Text(source, "id", 256) != sentId || !Labels(source).Contains("SENT", StringComparer.Ordinal)
                || (message.ThreadId is not null && message.ThreadId != threadId)) return new(null);
            using MimeMessage verified = ParseMime(Decode(Text(source, "raw", 12 * 1024 * 1024), MaximumMimeBytes));
            if (!SameEvidence(await EvidenceAsync(verified, cancellationToken), message.Evidence)) return new(null);
            string? draftId = key == GmailActionKeys.SendDraft ? ConnectorJson.Read<GmailDraftMessageRequest>(call.Arguments).DraftId : null;
            return new(ConnectorJson.Element(Receipt(call, [sentId], draftId, threadId, message.RawHash, reconciled: true)));
        }
        if (key == GmailActionKeys.DeleteDraft)
        {
            string draftId = ConnectorJson.Read<GmailDraftTargetRequest>(call.Arguments).DraftId;
            try { await RawDraftAsync(call, draftId, cancellationToken); return new(null); }
            catch (ConnectorProviderException exception) when (exception.Code == "ProviderItemNotFound")
            { return new(ConnectorJson.Element(Receipt(call, [], draftId, reconciled: true))); }
        }
        IReadOnlyList<string> targets = Targets(call);
        if (targets.Count == 0) return new(null);
        foreach (string id in targets)
        {
            JsonElement state;
            try { state = await MessageStateAsync(call, id, cancellationToken); }
            catch (ConnectorProviderException exception) when (exception.Code == "ProviderItemNotFound" && key == GmailActionKeys.PermanentDelete) { continue; }
            if (key == GmailActionKeys.PermanentDelete) return new(null);
            string[] labels = Labels(state);
            if (key == GmailActionKeys.Trash) { if (!labels.Contains("TRASH", StringComparer.Ordinal)) return new(null); }
            else if (key == GmailActionKeys.Restore) { if (labels.Contains("TRASH", StringComparer.Ordinal)) return new(null); }
            else
            {
                (IReadOnlyList<string> add, IReadOnlyList<string> remove) = LabelChanges(call);
                if (add.Any(label => !labels.Contains(label, StringComparer.Ordinal)) || remove.Any(label => labels.Contains(label, StringComparer.Ordinal))) return new(null);
            }
        }
        return new(ConnectorJson.Element(Receipt(call, targets, reconciled: true)));
    }

    private static bool SameEvidence(GmailMessageEvidence actual, GmailMessageEvidence expected)
        => ConnectorJson.Serialize(actual) == ConnectorJson.Serialize(expected);
}
