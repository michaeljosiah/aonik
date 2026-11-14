using FastEndpoints;

using Aonik.Platform.Services.Integrations;
using Aonik.SharedKernel.Abstractions.Workflows;

namespace Aonik.Platform.Endpoints.Integrations;

internal sealed class UpdateAutomationIdentityEndpoint(AutomationIdentityService identities) : Endpoint<AutomationIdentityCommand>
{
    public override void Configure() => Put("/v1/integrations/automation-identities/{Id:guid}");
    public override async Task HandleAsync(AutomationIdentityCommand request, CancellationToken ct)
        => await Send.ResultAsync(await ConnectorHttp.ExecuteAsync(() => identities.SaveAsync(Route<Guid>("Id"), request, ct)));
}
