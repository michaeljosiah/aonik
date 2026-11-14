using FastEndpoints;

using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Platform.Endpoints.Integrations;

internal sealed record ConnectorReadinessHttpRequest(string ConnectorKey, string ActionKey, int Version = 1, Guid? ConnectionId = null);
internal sealed class GetConnectorReadinessEndpoint(IConnectorService connectors) : Endpoint<ConnectorReadinessHttpRequest>
{
    public override void Configure() => Get("/v1/integrations/connectors/{ConnectorKey}/readiness");
    public override async Task HandleAsync(ConnectorReadinessHttpRequest request, CancellationToken ct)
        => await Send.ResultAsync(await ConnectorHttp.ExecuteAsync(() => connectors.GetReadinessAsync(request.ConnectorKey,
            new(request.ActionKey, request.Version), request.ConnectionId, ct)));
}
