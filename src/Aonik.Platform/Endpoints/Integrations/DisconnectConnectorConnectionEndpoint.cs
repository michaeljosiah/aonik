using FastEndpoints;

using Aonik.Platform.Services.Integrations;
using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Platform.Endpoints.Integrations;

internal sealed class DisconnectConnectorConnectionEndpoint(ConnectorConnectionManagementService connections) : Endpoint<ConnectionRequestCommand>
{
    public override void Configure() => Post("/v1/integrations/connections/{ConnectionId:guid}/disconnect");
    public override async Task HandleAsync(ConnectionRequestCommand request, CancellationToken ct)
        => await Send.ResultAsync(await ConnectorHttp.ExecuteAsync(() => connections.DisconnectAsync(Route<Guid>("ConnectionId"), request.ExpectedVersion, ct)));
}
