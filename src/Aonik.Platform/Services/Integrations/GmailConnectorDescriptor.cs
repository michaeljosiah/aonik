using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Platform.Services.Integrations;

internal sealed class GmailConnectorDescriptor : IConnectorDescriptor
{
    public ConnectorDescriptor Descriptor { get; } = new(GmailActionKeys.Connector, "Gmail",
    [
        new(GmailActionKeys.Connector, GmailActionKeys.Search, 1, "gmail_search_messages", [GmailActionKeys.ReadScope], null,
            ConnectorJson.Schema<GmailSearchRequest>(), ConnectorJson.Schema<GmailSearchResult>(), ReadScopes()),
        new(GmailActionKeys.Connector, GmailActionKeys.GetMessage, 1, "gmail_get_message", [GmailActionKeys.ReadScope], null,
            ConnectorJson.Schema<GmailGetMessageRequest>(), ConnectorJson.Schema<GmailMessageResult>(), ReadScopes()),
        new(GmailActionKeys.Connector, GmailActionKeys.GetAttachment, 1, "gmail_get_attachment", [GmailActionKeys.ReadScope], null,
            ConnectorJson.Schema<GmailGetAttachmentRequest>(), ConnectorJson.Schema<GmailAttachmentResult>(), ReadScopes()),
        new(GmailActionKeys.Connector, GmailActionKeys.GetDraft, 1, GmailActionPolicies.ToolNames[GmailActionKeys.GetDraft], [GmailActionKeys.ReadScope], null,
            ConnectorJson.Schema<GmailGetDraftRequest>(), ConnectorJson.Schema<GmailDraftResult>(), [[GmailActionKeys.ReadScope], [GmailActionKeys.ComposeScope], [GmailActionKeys.ModifyScope], [GmailActionKeys.FullScope]]),
        new(GmailActionKeys.Connector, GmailActionKeys.PrepareMessage, 1, GmailActionPolicies.ToolNames[GmailActionKeys.PrepareMessage], [], GmailActionPolicies.Approval(GmailActionKeys.PrepareMessage),
            ConnectorJson.Schema<GmailPrepareMessageRequest>(), ConnectorJson.Schema<GmailPreparedMessage>()),
        Draft<GmailSendRequest>(GmailActionKeys.CreateDraft), Draft<GmailDraftMessageRequest>(GmailActionKeys.UpdateDraft), Draft<GmailDraftTargetRequest>(GmailActionKeys.DeleteDraft),
        Send<GmailSendRequest>(GmailActionKeys.SendMessage), Send<GmailSendRequest>(GmailActionKeys.ReplyMessage), Send<GmailSendRequest>(GmailActionKeys.ForwardMessage),
        Mutation<GmailDraftMessageRequest>(GmailActionKeys.SendDraft, [GmailActionKeys.ComposeScope, GmailActionKeys.ReadScope], [[GmailActionKeys.ComposeScope, GmailActionKeys.ReadScope], [GmailActionKeys.ModifyScope], [GmailActionKeys.FullScope]]),
        Modify<GmailModifyLabelsRequest>(GmailActionKeys.ModifyLabels), Modify<GmailMessageTargets>(GmailActionKeys.MarkRead), Modify<GmailMessageTargets>(GmailActionKeys.MarkUnread),
        Modify<GmailMessageTargets>(GmailActionKeys.Archive), Modify<GmailMessageTargets>(GmailActionKeys.Trash), Modify<GmailMessageTargets>(GmailActionKeys.Restore),
        Mutation<GmailMessageTargets>(GmailActionKeys.PermanentDelete, [GmailActionKeys.FullScope], [[GmailActionKeys.FullScope]]),
    ]);

    private static IReadOnlyList<IReadOnlyList<string>> ReadScopes() => [[GmailActionKeys.ReadScope], [GmailActionKeys.ModifyScope], [GmailActionKeys.FullScope]];
    private static ConnectorActionDescriptor Draft<T>(string key) => Mutation<T>(key, [GmailActionKeys.ComposeScope], [[GmailActionKeys.ComposeScope], [GmailActionKeys.ModifyScope], [GmailActionKeys.FullScope]]);
    private static ConnectorActionDescriptor Send<T>(string key) => Mutation<T>(key, [GmailActionKeys.SendScope, GmailActionKeys.ReadScope],
        [[GmailActionKeys.SendScope, GmailActionKeys.ReadScope], [GmailActionKeys.ComposeScope, GmailActionKeys.ReadScope], [GmailActionKeys.ModifyScope], [GmailActionKeys.FullScope]]);
    private static ConnectorActionDescriptor Modify<T>(string key) => Mutation<T>(key, [GmailActionKeys.ModifyScope], [[GmailActionKeys.ModifyScope], [GmailActionKeys.FullScope]]);
    private static ConnectorActionDescriptor Mutation<T>(string key, IReadOnlyList<string> scopes, IReadOnlyList<IReadOnlyList<string>> alternatives)
        => new(GmailActionKeys.Connector, key, 1, GmailActionPolicies.ToolNames[key], scopes, GmailActionPolicies.Approval(key),
            ConnectorJson.Schema<T>(), ConnectorJson.Schema<GmailMutationReceipt>(), alternatives);
}
