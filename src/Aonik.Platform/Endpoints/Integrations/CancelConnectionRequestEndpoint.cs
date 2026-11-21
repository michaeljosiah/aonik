using FastEndpoints;

using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Platform.Endpoints.Integrations;

internal sealed class CancelConnectionRequestEndpoint(IConnectorConnectionRequests requests) : Endpoint<ConnectionRequestCommand>
{
    public override void Configure() => Post("/v1/integrations/connection-requests/{RequestId:guid}/cancel");
    public override async Task HandleAsync(ConnectionRequestCommand request, CancellationToken ct)
        => await Send.ResultAsync(await ConnectorHttp.ExecuteAsync(() => requests.CancelAsync(Route<Guid>("RequestId"), request, ct)));
}
