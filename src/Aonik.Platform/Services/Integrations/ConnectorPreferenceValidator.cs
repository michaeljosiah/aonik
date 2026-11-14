using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Platform.Services.Integrations;

internal sealed class ConnectorPreferenceValidator(ConnectorCatalog catalog, ConnectorAuthorization authorization) : IConnectorPreferenceValidator
{
    public async Task RequireEligibleAsync(Guid connectionId, Guid userId, CancellationToken cancellationToken = default)
    {
        // Selecting a default proves ordinary use of the account; it never creates a grant.
        await authorization.RequireUseAsync(connectionId, catalog.Resolve(new(GmailActionKeys.Search, 1)),
            new(new("User", userId, userId)), cancellationToken);
    }
}
