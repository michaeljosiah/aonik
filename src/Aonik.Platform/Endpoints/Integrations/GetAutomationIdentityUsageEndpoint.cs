using FastEndpoints;

using Aonik.Platform.Services.Integrations;

namespace Aonik.Platform.Endpoints.Integrations;

internal sealed class GetAutomationIdentityUsageEndpoint(AutomationIdentityService identities) : Endpoint<AutomationIdentityIdRequest>
{
    public override void Configure() => Get("/v1/integrations/automation-identities/{Id:guid}/usage");
    public override async Task HandleAsync(AutomationIdentityIdRequest request, CancellationToken ct)
        => await Send.ResultAsync(await ConnectorHttp.ExecuteAsync(() => identities.UsageAsync(request.Id, ct)));
}
