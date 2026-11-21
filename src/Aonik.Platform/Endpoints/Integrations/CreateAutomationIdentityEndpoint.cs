using FastEndpoints;
using FluentValidation;

using Aonik.Platform.Services.Integrations;
using Aonik.SharedKernel.Abstractions.Workflows;

namespace Aonik.Platform.Endpoints.Integrations;

internal sealed class CreateAutomationIdentityEndpoint(AutomationIdentityService identities) : Endpoint<AutomationIdentityCommand>
{
    public override void Configure() => Post("/v1/integrations/automation-identities");
    public override async Task HandleAsync(AutomationIdentityCommand request, CancellationToken ct)
        => await Send.ResultAsync(await ConnectorHttp.ExecuteAsync(() => identities.SaveAsync(null, request, ct)));
}
internal sealed class AutomationIdentityCommandValidator : Validator<AutomationIdentityCommand>
{
    public AutomationIdentityCommandValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(160);
        RuleFor(request => request.AccountableUserId).NotEmpty();
        RuleFor(request => request.Grants).NotNull().Must(grants => grants is not null && grants.Count <= 20 && grants.All(grant => grant is not null
            && grant.WorkflowVersionId != Guid.Empty && grant.Actions is { Count: <= 100 } && grant.Actions.All(action => action is not null && action.Key is { Length: > 0 and <= 100 } && action.Version > 0)
            && grant.SubjectIds is { Count: <= 1000 }));
        RuleFor(request => request.ExpectedVersion).MaximumLength(128);
    }
}
