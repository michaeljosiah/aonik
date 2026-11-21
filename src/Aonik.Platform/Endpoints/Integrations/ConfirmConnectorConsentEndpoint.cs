using FastEndpoints;

using Aonik.Platform.Services.Integrations;
using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Platform.Endpoints.Integrations;

internal sealed class ConfirmConnectorConsentEndpoint(ConnectorConsentService consent) : Endpoint<ConnectionRequestCommand>
{
    public override void Configure() => Post("/v1/integrations/connection-requests/{RequestId:guid}/confirm-account");
    public override async Task HandleAsync(ConnectionRequestCommand request, CancellationToken ct)
        => await Send.ResultAsync(await ConnectorHttp.ExecuteAsync(() => consent.ConfirmAsync(Route<Guid>("RequestId"), request, ct)));
}
