using FastEndpoints;

using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Platform.Endpoints.Integrations;

internal sealed class CreateConnectionRequestEndpoint(IConnectorConnectionRequests requests) : Endpoint<CreateConnectionRequest>
{
    public override void Configure() => Post("/v1/integrations/connection-requests");
    public override async Task HandleAsync(CreateConnectionRequest request, CancellationToken ct)
        => await Send.ResultAsync(await ConnectorHttp.ExecuteAsync(() => requests.CreateAsync(request, ct)));
}
