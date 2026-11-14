using FastEndpoints;

using Aonik.Platform.Services.Integrations;

namespace Aonik.Platform.Endpoints.Integrations;

internal sealed class ListConnectorGrantsEndpoint(ConnectorGrantService grants) : EndpointWithoutRequest
{
    public override void Configure() => Get("/v1/integrations/connections/{ConnectionId:guid}/grants");
    public override async Task HandleAsync(CancellationToken ct)
        => await Send.ResultAsync(await ConnectorHttp.ExecuteAsync(() => grants.ListAsync(Route<Guid>("ConnectionId"), ct)));
}
