using FastEndpoints;

using Aonik.Platform.Services.Integrations;

namespace Aonik.Platform.Endpoints.Integrations;

internal sealed class GetAutomationWorkflowCapabilityEndpoint(AutomationIdentityService identities) : Endpoint<AutomationIdentityIdRequest>
{
    public override void Configure() => Get("/v1/integrations/automation-identities/workflow-versions/{Id:guid}");
    public override async Task HandleAsync(AutomationIdentityIdRequest request, CancellationToken ct)
        => await Send.ResultAsync(await ConnectorHttp.ExecuteAsync(() => identities.CapabilitiesAsync(request.Id, ct)));
}
