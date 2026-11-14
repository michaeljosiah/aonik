using FastEndpoints;

using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Platform.Endpoints.Integrations;

internal sealed class DecideConnectionRequestEndpoint(IConnectorConnectionRequests requests) : Endpoint<DecideConnectionRequest>
{
    public override void Configure() => Post("/v1/integrations/connection-requests/{RequestId:guid}/decide");
    public override async Task HandleAsync(DecideConnectionRequest request, CancellationToken ct)
        => await Send.ResultAsync(await ConnectorHttp.ExecuteAsync(() => requests.DecideAsync(Route<Guid>("RequestId"), request, ct)));
}
