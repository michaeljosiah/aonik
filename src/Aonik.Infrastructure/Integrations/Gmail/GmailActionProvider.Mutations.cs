using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Infrastructure.Integrations.Gmail;

internal sealed partial class GmailActionProvider
{
    public async Task<ToolApprovalPreview?> PreviewAsync(ConnectorProviderCall call, CancellationToken cancellationToken = default)
    {
        ValidateArguments(call.Action, call.Arguments);
        if (!GmailActionPolicies.IsMutation(call.Action.Key)) throw new ArgumentException("Only classified mutations have a reviewed effect preview.");
        GmailPreparedEnvelope? message = await PreparedAsync(call, cancellationToken);
        GmailRawDraft? draft = await RequireDraftAsync(call, cancellationToken);
        IReadOnlyList<string> targets = Targets(call);
        var current = new List<object>();
        foreach (string id in targets)
        {
            JsonElement item = await MessageStateAsync(call, id, cancellationToken);
            current.Add(new { messageId = id, labels = Labels(item) });
        }
        return new(call.Action.Key == GmailActionKeys.PermanentDelete ? $"Permanently delete {targets.Count} Gmail messages (cannot be undone)" : "Review " + call.Action.Key,
            ConnectorJson.Element(new { draftId = draft?.DraftId, draftHash = draft is null ? null : HashBytes(draft.Raw), messages = current }),
            ConnectorJson.Element(new { call.ConnectionId, call.ProviderAccountId, account = call.ProviderAccountLabel, action = call.Action.Key, message = message?.Evidence,
                contentHash = message?.RawHash, threadId = message?.ThreadId, messageIds = targets,
                labels = call.Action.Key == GmailActionKeys.ModifyLabels ? call.Arguments : (JsonElement?)null,
                irreversible = call.Action.Key == GmailActionKeys.PermanentDelete, targetCount = targets.Count,
                effect = call.Action.Key == GmailActionKeys.PermanentDelete ? "These exact messages are removed permanently and cannot be restored." : null }));
    }

    private async Task<GmailMutationReceipt> MutateAsync(ConnectorProviderCall call, CancellationToken cancellationToken)
    {
        GmailPreparedEnvelope? message = await PreparedAsync(call, cancellationToken);
        GmailRawDraft? draft = await RequireDraftAsync(call, cancellationToken);
        string key = call.Action.Key;
        if (message is not null)
        {
            object raw = new { raw = Base64Url(Convert.FromBase64String(message.RawBase64)), threadId = message.ThreadId };
            bool draftWrite = key is GmailActionKeys.CreateDraft or GmailActionKeys.UpdateDraft or GmailActionKeys.SendDraft;
            string path = key switch { GmailActionKeys.CreateDraft => "drafts", GmailActionKeys.UpdateDraft => "drafts/" + draft!.DraftId,
                GmailActionKeys.SendDraft => "drafts/send", _ => "messages/send" };
            object body = draftWrite ? new { id = draft?.DraftId, message = raw } : raw;
            JsonElement response = await WriteAsync(call, key == GmailActionKeys.UpdateDraft ? HttpMethod.Put : HttpMethod.Post, path, body, cancellationToken);
            JsonElement sent = key is GmailActionKeys.CreateDraft or GmailActionKeys.UpdateDraft ? response.GetProperty("message") : response;
            string id = Text(sent, "id", 256);
            return Receipt(call, [id], key is GmailActionKeys.CreateDraft or GmailActionKeys.UpdateDraft ? Text(response, "id", 256) : draft?.DraftId,
                OptionalText(sent, "threadId", 256), message.RawHash);
        }
        if (key == GmailActionKeys.DeleteDraft)
        {
            await WriteAsync(call, HttpMethod.Delete, "drafts/" + draft!.DraftId, null, cancellationToken);
            return Receipt(call, [draft.MessageId], draft.DraftId, draft.ThreadId, HashBytes(draft.Raw));
        }
        IReadOnlyList<string> targets = Targets(call);
        if (key == GmailActionKeys.PermanentDelete)
            await WriteAsync(call, HttpMethod.Post, "messages/batchDelete", new { ids = targets }, cancellationToken);
        else if (key is GmailActionKeys.Trash or GmailActionKeys.Restore)
        {
            foreach (string id in targets)
            {
                JsonElement response = await WriteAsync(call, HttpMethod.Post, "messages/" + id + (key == GmailActionKeys.Trash ? "/trash" : "/untrash"), new { }, cancellationToken);
                if (Text(response, "id", 256) != id) throw new ConnectorProviderException("ProviderItemMismatch");
            }
        }
        else
        {
            (IReadOnlyList<string> add, IReadOnlyList<string> remove) = LabelChanges(call);
            await WriteAsync(call, HttpMethod.Post, "messages/batchModify", new { ids = targets, addLabelIds = add, removeLabelIds = remove }, cancellationToken);
        }
        return Receipt(call, targets);
    }

    private async Task<GmailPreparedEnvelope?> PreparedAsync(ConnectorProviderCall call, CancellationToken cancellationToken)
    {
        if (call.Action.Key is GmailActionKeys.CreateDraft or GmailActionKeys.SendMessage or GmailActionKeys.ReplyMessage or GmailActionKeys.ForwardMessage)
        {
            GmailSendRequest input = ConnectorJson.Read<GmailSendRequest>(call.Arguments);
            return await ReviewedMessageAsync(call, input.MessageReference, input.ContentHash, cancellationToken);
        }
        if (call.Action.Key is GmailActionKeys.UpdateDraft or GmailActionKeys.SendDraft)
        {
            GmailDraftMessageRequest input = ConnectorJson.Read<GmailDraftMessageRequest>(call.Arguments);
            return await ReviewedMessageAsync(call, input.MessageReference, input.ContentHash, cancellationToken);
        }
        return null;
    }

    private async Task<GmailRawDraft?> RequireDraftAsync(ConnectorProviderCall call, CancellationToken cancellationToken)
    {
        string id, hash;
        if (call.Action.Key is GmailActionKeys.UpdateDraft or GmailActionKeys.SendDraft)
        { GmailDraftMessageRequest input = ConnectorJson.Read<GmailDraftMessageRequest>(call.Arguments); id = input.DraftId; hash = input.ExpectedDraftHash; }
        else if (call.Action.Key == GmailActionKeys.DeleteDraft)
        { GmailDraftTargetRequest input = ConnectorJson.Read<GmailDraftTargetRequest>(call.Arguments); id = input.DraftId; hash = input.ExpectedDraftHash; }
        else return null;
        GmailRawDraft draft = await RawDraftAsync(call, id, cancellationToken);
        if (!string.Equals(HashBytes(draft.Raw), hash, StringComparison.OrdinalIgnoreCase))
            throw new ConnectorAccessException("DraftChanged", "The draft changed after review. Read and approve its current content before another attempt.");
        return draft;
    }

    private static IReadOnlyList<string> Targets(ConnectorProviderCall call) => call.Action.Key switch
    {
        GmailActionKeys.ModifyLabels => ConnectorJson.Read<GmailModifyLabelsRequest>(call.Arguments).MessageIds,
        GmailActionKeys.MarkRead or GmailActionKeys.MarkUnread or GmailActionKeys.Archive or GmailActionKeys.Trash or GmailActionKeys.Restore or GmailActionKeys.PermanentDelete
            => ConnectorJson.Read<GmailMessageTargets>(call.Arguments).MessageIds,
        _ => [],
    };
    private static (IReadOnlyList<string> Add, IReadOnlyList<string> Remove) LabelChanges(ConnectorProviderCall call) => call.Action.Key switch
    {
        GmailActionKeys.MarkRead => ([], ["UNREAD"]), GmailActionKeys.MarkUnread => (["UNREAD"], []), GmailActionKeys.Archive => ([], ["INBOX"]),
        GmailActionKeys.ModifyLabels => (ConnectorJson.Read<GmailModifyLabelsRequest>(call.Arguments).AddLabels, ConnectorJson.Read<GmailModifyLabelsRequest>(call.Arguments).RemoveLabels),
        _ => throw new ArgumentException("Unsupported label change."),
    };
    private async Task<JsonElement> MessageStateAsync(ConnectorProviderCall call, string id, CancellationToken cancellationToken)
    {
        JsonElement result = await GetAsync(call, "messages/" + Uri.EscapeDataString(id) + "?format=minimal&fields=id,labelIds", 32 * 1024, cancellationToken);
        if (Text(result, "id", 256) != id) throw new ConnectorProviderException("ProviderItemMismatch");
        return result;
    }
    private static string[] Labels(JsonElement json) => json.TryGetProperty("labelIds", out JsonElement labels) ? labels.EnumerateArray().Select(label => label.GetString()!).ToArray() : [];
    private static string Base64Url(byte[] raw) => Convert.ToBase64String(raw).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static GmailMutationReceipt Receipt(ConnectorProviderCall call, IReadOnlyList<string> ids, string? draftId = null, string? threadId = null, string? hash = null, bool reconciled = false)
        => new(call.Action.Key, call.OperationId, "Completed", ids, draftId, threadId, hash, reconciled);

    private async Task<JsonElement> WriteAsync(ConnectorProviderCall call, HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        using HttpClient client = clients.CreateClient("Gmail");
        using var request = new HttpRequestMessage(method, "https://gmail.googleapis.com/gmail/v1/users/me/" + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", call.AccessToken);
        if (body is not null) request.Content = new StringContent(ConnectorJson.Serialize(body), Encoding.UTF8, "application/json");
        // Provider writes are issued once. Timeout/rate-limit/transport errors are reconciled with read evidence, never retried here.
        return await GmailHttp.JsonAsync(client, request, 128 * 1024, cancellationToken, allowEmpty: true);
    }
}
