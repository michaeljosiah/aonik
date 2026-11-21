using System.Text.Json;

using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

using Aonik.Agents.Framework;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Agents.Services.Connectors.Catalog;

internal sealed class ConnectorPackageActionProvider(ConnectorPackageRegistration registration, ConnectorPackageAccountReader accounts,
    ConnectorPackageMcpClient mcp, ITenantCredentialProtector protector, ITenantEgressAllowList egress, IHttpClientFactory http) : IConnectorServiceAccountProvider
{
    public string ConnectorKey => registration.Descriptor.Key;

    public async Task<ConnectorServiceAccount> RequireAccountAsync(string providerAccountId, ConnectorActionRef? action = null,
        ConnectorArtifactPin? artifact = null, CancellationToken cancellationToken = default)
        => (await accounts.RequireAsync(ConnectorKey, providerAccountId, action, artifact, cancellationToken)).Describe();

    public void ValidateArguments(ConnectorActionRef action, JsonElement arguments) => Contract(action).RequireInput(arguments);

    public async Task<JsonElement> InvokeAsync(ConnectorProviderCall call, CancellationToken cancellationToken = default)
    {
        ConnectorPackageActionContract contract = Contract(call.Action);
        JsonElement arguments = contract.RequireInput(call.Arguments);
        ConnectorPackageAccount account = await RequireCurrentAsync(call, cancellationToken);
        ConnectorPackageAction actual = account.Actions.Single(item => item.Key == call.Action.Key && item.Version == call.Action.Version);
        ConnectorPackageManifest manifest = account.Package.Manifest;
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(manifest.Limits.TimeoutSeconds));
        try
        {
            JsonElement output;
            if (account.Mcp is { } server)
            {
                await using McpClient client = await mcp.ConnectAsync(server, account.Package, budget.Token);
                // Discovery is network I/O. Recheck disablement and rotation before the actual tool call.
                await RequireCurrentAsync(call, budget.Token);
                CallToolResult response = await client.CallToolAsync(actual.RemoteToolName!, arguments.EnumerateObject()
                    .ToDictionary(property => property.Name, property => (object?)property.Value, StringComparer.Ordinal), cancellationToken: budget.Token);
                if (response.IsError == true || response.StructuredContent is not { } structured)
                    throw new ConnectorProviderException("RemoteMcpResultInvalid");
                output = structured;
            }
            else
            {
                var function = new DeclarativeHttpAIFunction(account.Http!, protector, egress, http);
                output = await function.InvokePackageAsync(arguments, manifest.Authentication.HeaderName, manifest.Limits.MaximumOutputBytes, budget.Token);
            }
            contract.RequireOutput(output);
            return output;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new ConnectorProviderException("RemoteActionTimeout"); }
        catch (Exception error) when (error is McpException or HttpRequestException or IOException)
        { throw new ConnectorProviderException("RemoteTransportFailed", transient: contract.Descriptor.Approval is null); }
        catch (Exception error) when (error is JsonException or ArgumentException)
        { throw new ConnectorProviderException("RemoteActionContractMismatch"); }
    }

    public async Task<ToolApprovalPreview?> PreviewAsync(ConnectorProviderCall call, CancellationToken cancellationToken = default)
    {
        ConnectorPackageActionContract contract = Contract(call.Action);
        if (contract.Descriptor.Approval is null) return null;
        JsonElement arguments = contract.RequireInput(call.Arguments);
        ConnectorPackageAccount account = await RequireCurrentAsync(call, cancellationToken);
        ConnectorPackageAction action = account.Actions.Single(item => item.Key == call.Action.Key && item.Version == call.Action.Version);
        return new($"Review {action.DisplayName}", null, ConnectorJson.Element(new
        {
            call.ConnectionId, call.ProviderAccountId, call.ProviderAccountLabel, call.Artifact, call.ServiceCredentialVersion,
            call.Action, target = action.Endpoint, method = action.HttpMethod, tool = action.RemoteToolName, arguments
        }));
    }

    private async Task<ConnectorPackageAccount> RequireCurrentAsync(ConnectorProviderCall call, CancellationToken cancellationToken)
    {
        if (call.Artifact is null || call.ServiceCredentialVersion is null || !string.IsNullOrEmpty(call.AccessToken))
            throw new ConnectorAccessException("WorkflowBindingRequired", "The call needs current reviewed service-account evidence from the connector gateway.");
        ConnectorPackageAccount account = await accounts.RequireAsync(ConnectorKey, call.ProviderAccountId, call.Action, call.Artifact, cancellationToken);
        if (account.Describe().CredentialVersion != call.ServiceCredentialVersion)
            throw new ConnectorAccessException("ReviewedAccountChanged", "The service account credentials changed.");
        return account;
    }

    private ConnectorPackageActionContract Contract(ConnectorActionRef action) => registration.Contracts.GetValueOrDefault(action)
        ?? throw new ConnectorAccessException("ConnectorUnavailable", "The reviewed connector action is unavailable.");
}
