using FastEndpoints;

using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Platform.Endpoints.Integrations;

internal sealed class ListConnectorConnectionsEndpoint(IConnectorService connectors) : EndpointWithoutRequest
{
    public override void Configure() => Get("/v1/integrations/connectors/{ConnectorKey}/connections");
    public override async Task HandleAsync(CancellationToken ct)
        => await Send.ResultAsync(await ConnectorHttp.ExecuteAsync(() => connectors.ListConnectionsAsync(Route<string>("ConnectorKey")!, ct)));
}
