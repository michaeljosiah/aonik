using FastEndpoints;

using Aonik.Platform.Services.Integrations;

namespace Aonik.Platform.Endpoints.Integrations;

internal sealed class ListAutomationIdentitiesEndpoint(AutomationIdentityService identities) : EndpointWithoutRequest
{
    public override void Configure() => Get("/v1/integrations/automation-identities");
    public override async Task HandleAsync(CancellationToken ct)
        => await Send.ResultAsync(await ConnectorHttp.ExecuteAsync(() => identities.ListAsync(ct)));
}
