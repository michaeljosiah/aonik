using FastEndpoints;
using FluentValidation;

using Aonik.Platform.Services.Integrations;

namespace Aonik.Platform.Endpoints.Integrations;

internal sealed record AutomationIdentityIdRequest(Guid Id);
internal sealed class GetAutomationIdentityEndpoint(AutomationIdentityService identities) : Endpoint<AutomationIdentityIdRequest>
{
    public override void Configure() => Get("/v1/integrations/automation-identities/{Id:guid}");
    public override async Task HandleAsync(AutomationIdentityIdRequest request, CancellationToken ct)
        => await Send.ResultAsync(await ConnectorHttp.ExecuteAsync(() => identities.GetAsync(request.Id, ct)));
}
internal sealed class AutomationIdentityIdRequestValidator : Validator<AutomationIdentityIdRequest>
{
    public AutomationIdentityIdRequestValidator() => RuleFor(request => request.Id).NotEmpty();
}
