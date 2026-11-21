using FastEndpoints;

using Aonik.Platform.Services.Integrations;

namespace Aonik.Platform.Endpoints.Integrations;

internal sealed record RenameConnectionHttpRequest(string DisplayName, string ExpectedVersion);
internal sealed class RenameConnectorConnectionEndpoint(ConnectorConnectionManagementService connections) : Endpoint<RenameConnectionHttpRequest>
{
    public override void Configure() => Put("/v1/integrations/connections/{ConnectionId:guid}");
    public override async Task HandleAsync(RenameConnectionHttpRequest request, CancellationToken ct)
        => await Send.ResultAsync(await ConnectorHttp.ExecuteAsync(() => connections.RenameAsync(Route<Guid>("ConnectionId"), request.DisplayName, request.ExpectedVersion, ct)));
}
