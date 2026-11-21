using FastEndpoints;

using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Platform.Endpoints.Integrations;

internal sealed class GetConnectionRequestEndpoint(IConnectorConnectionRequests requests) : EndpointWithoutRequest
{
    public override void Configure() => Get("/v1/integrations/connection-requests/{RequestId:guid}");
    public override async Task HandleAsync(CancellationToken ct)
        => await Send.ResultAsync(await ConnectorHttp.ExecuteAsync(() => requests.GetAsync(Route<Guid>("RequestId"), ct)));
}
