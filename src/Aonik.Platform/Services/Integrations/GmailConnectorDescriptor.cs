using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Platform.Services.Integrations;

internal sealed class GmailConnectorDescriptor : IConnectorDescriptor
{
    public ConnectorDescriptor Descriptor { get; } = new(GmailActionKeys.Connector, "Gmail",
    [
        new(GmailActionKeys.Connector, GmailActionKeys.Search, 1, "gmail_search_messages", [GmailActionKeys.ReadScope], null,
            ConnectorJson.Schema<GmailSearchRequest>(), ConnectorJson.Schema<GmailSearchResult>()),
        new(GmailActionKeys.Connector, GmailActionKeys.GetMessage, 1, "gmail_get_message", [GmailActionKeys.ReadScope], null,
            ConnectorJson.Schema<GmailGetMessageRequest>(), ConnectorJson.Schema<GmailMessageResult>()),
        new(GmailActionKeys.Connector, GmailActionKeys.GetAttachment, 1, "gmail_get_attachment", [GmailActionKeys.ReadScope], null,
            ConnectorJson.Schema<GmailGetAttachmentRequest>(), ConnectorJson.Schema<GmailAttachmentResult>()),
    ]);
}
