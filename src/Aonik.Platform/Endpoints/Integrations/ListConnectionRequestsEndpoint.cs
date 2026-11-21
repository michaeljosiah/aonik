using FastEndpoints;

using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Platform.Endpoints.Integrations;

internal sealed record ListConnectionRequestsHttpRequest(int Skip = 0, int Take = 20);
internal sealed class ListConnectionRequestsEndpoint(IConnectorConnectionRequests requests) : Endpoint<ListConnectionRequestsHttpRequest>
{
    public override void Configure() => Get("/v1/integrations/connection-requests");
    public override async Task HandleAsync(ListConnectionRequestsHttpRequest request, CancellationToken ct)
        => await Send.ResultAsync(await ConnectorHttp.ExecuteAsync(() => requests.ListAsync(request.Skip, request.Take, ct)));
}
