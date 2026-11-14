using FastEndpoints;
using FluentValidation;

using Aonik.Platform.Services.Integrations;

namespace Aonik.Platform.Endpoints.Integrations;

internal sealed record GetWorkflowConnectorBindingRequest(Guid ConnectionId, Guid WorkflowId, string ActionKey, int Version = 1);
internal sealed class GetWorkflowConnectorBindingEndpoint(ConnectorGrantService grants) : Endpoint<GetWorkflowConnectorBindingRequest>
{
    public override void Configure() => Get("/v1/integrations/connections/{ConnectionId:guid}/workflow-binding");
    public override async Task HandleAsync(GetWorkflowConnectorBindingRequest request, CancellationToken ct)
        => await Send.ResultAsync(await ConnectorHttp.ExecuteAsync(() => grants.GetWorkflowBindingAsync(request.ConnectionId, request.WorkflowId, new(request.ActionKey, request.Version), ct)));
}
internal sealed class GetWorkflowConnectorBindingRequestValidator : Validator<GetWorkflowConnectorBindingRequest>
{
    public GetWorkflowConnectorBindingRequestValidator()
    {
        RuleFor(request => request.ConnectionId).NotEmpty();
        RuleFor(request => request.WorkflowId).NotEmpty();
        RuleFor(request => request.ActionKey).NotEmpty().MaximumLength(100);
        RuleFor(request => request.Version).GreaterThan(0);
    }
}
