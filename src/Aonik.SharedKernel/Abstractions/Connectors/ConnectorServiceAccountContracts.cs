namespace Aonik.SharedKernel.Abstractions.Connectors;

/// <summary>Trusted in-process proof of a currently active, platform-reviewed tenant extension. Contains no credential.</summary>
public sealed record ConnectorServiceAccount(string ConnectorKey, string ProviderAccountId, string DisplayName,
    int CredentialVersion, IReadOnlyList<ConnectorServiceAccountAction> Actions);
public sealed record ConnectorServiceAccountAction(ConnectorActionRef Action, ConnectorArtifactPin Artifact);

/// <summary>Reviewed MCP/HTTP service credentials stay in their existing tenant extension store, separate from native OAuth consent.</summary>
public interface IConnectorServiceAccountProvider : IConnectorActionProvider
{
    Task<ConnectorServiceAccount> RequireAccountAsync(string providerAccountId, ConnectorActionRef? action = null,
        ConnectorArtifactPin? artifact = null, CancellationToken cancellationToken = default);
}
