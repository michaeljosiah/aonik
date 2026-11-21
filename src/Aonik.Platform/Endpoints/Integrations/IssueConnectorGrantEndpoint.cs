using FastEndpoints;

using Aonik.Platform.Services.Integrations;
using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Platform.Endpoints.Integrations;

internal sealed class IssueConnectorGrantEndpoint(ConnectorGrantService grants) : Endpoint<ConnectorGrantCommand>
{
    public override void Configure() => Post("/v1/integrations/connections/{ConnectionId:guid}/grants");
    public override async Task HandleAsync(ConnectorGrantCommand request, CancellationToken ct)
        => await Send.ResultAsync(await ConnectorHttp.ExecuteAsync(() => grants.IssueAsync(Route<Guid>("ConnectionId"), request, ct)));
}
