using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Abstractions.Workflows;

namespace Aonik.Platform.Services.Integrations;

/// <summary>Rechecks source account authority without depending on the workflow action registry.</summary>
internal sealed class FinancialDocumentSourceAccess(ConnectorAuthorization authorization, ConnectorCatalog catalog)
    : IFinancialDocumentSourceAccess
{
    public async Task RequireAsync(FinancialDocumentSource source, WorkflowActor actor, CancellationToken cancellationToken = default)
    {
        if (source.ConnectionId == Guid.Empty || source.WorkflowId == Guid.Empty || source.WorkflowVersionId == Guid.Empty
            || source.WorkflowGrantId == Guid.Empty || source.AutomationIdentityId == Guid.Empty || source.BindingId == Guid.Empty || source.BindingRevision < 1
            || source.ContentHash is not { Length: 64 } || !source.ContentHash.All(Uri.IsHexDigit) || string.IsNullOrWhiteSpace(source.ProviderAccountId)
            || (actor.Type == "AutomationIdentity" && (actor.Id != source.AutomationIdentityId || actor.WorkflowVersionId != source.WorkflowVersionId)))
            throw new PermissionDeniedException("Integration.Use");
        ConnectorActionRef[] actions = [new(GmailActionKeys.Search, 1), new(GmailActionKeys.GetMessage, 1), new(GmailActionKeys.GetAttachment, 1)];
        foreach (ConnectorActionRef action in actions)
        {
            var connection = await authorization.RequireUseAsync(source.ConnectionId, catalog.Resolve(action),
                new(actor, source.WorkflowId, source.WorkflowVersionId, PinnedGrantId: source.WorkflowGrantId, AllowedActions: actions), cancellationToken);
            if (connection.ProviderAccountId != source.ProviderAccountId)
                throw new ConnectorAccessException("ProviderAccountChanged", "The financial source account changed. Review the source again.");
        }
    }
}
