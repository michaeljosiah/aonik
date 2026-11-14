using FastEndpoints;

using Aonik.Platform.Services.Integrations;
using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Platform.Endpoints.Integrations;

internal sealed class RevokeConnectorGrantEndpoint(ConnectorGrantService grants) : Endpoint<ConnectionRequestCommand>
{
    public override void Configure() => Post("/v1/integrations/grants/{GrantId:guid}/revoke");
    public override async Task HandleAsync(ConnectionRequestCommand request, CancellationToken ct)
        => await Send.ResultAsync(await ConnectorHttp.ExecuteAsync(async () => { await grants.RevokeAsync(Route<Guid>("GrantId"), request, ct); return new { revoked = true }; }));
}
