using FastEndpoints;

using Aonik.Platform.Services.Integrations;
using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Platform.Endpoints.Integrations;

internal sealed class DisableAutomationIdentityEndpoint(AutomationIdentityService identities) : Endpoint<ConnectionRequestCommand>
{
    public override void Configure() => Post("/v1/integrations/automation-identities/{Id:guid}/disable");
    public override async Task HandleAsync(ConnectionRequestCommand request, CancellationToken ct)
        => await Send.ResultAsync(await ConnectorHttp.ExecuteAsync(() => identities.DisableAsync(Route<Guid>("Id"), request, ct)));
}
