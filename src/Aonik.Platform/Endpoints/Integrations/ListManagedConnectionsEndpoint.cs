using FastEndpoints;

using Aonik.Platform.Services.Integrations;

namespace Aonik.Platform.Endpoints.Integrations;

internal sealed class ListManagedConnectionsEndpoint(ConnectorConnectionManagementService connections) : EndpointWithoutRequest
{
    public override void Configure() => Get("/v1/integrations/managed-connections");
    public override async Task HandleAsync(CancellationToken ct)
        => await Send.ResultAsync(await ConnectorHttp.ExecuteAsync(() => connections.ListAsync(ct)));
}
