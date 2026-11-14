using FastEndpoints;
using FluentValidation;

using Aonik.Agents.Contracts.Services;

namespace Aonik.Agents.Endpoints.Workflows;

internal sealed record ListWorkflowTriggersHttpRequest(string Slug);
internal sealed class ListWorkflowTriggersEndpoint(IWorkflowTriggerService triggers) : Endpoint<ListWorkflowTriggersHttpRequest>
{
    public override void Configure() => Get("/ai/workflows/{Slug}/triggers");
    public override async Task HandleAsync(ListWorkflowTriggersHttpRequest request, CancellationToken ct)
        => await Send.ResultAsync(await WorkflowTriggerHttp.ExecuteAsync(() => triggers.ListAsync(request.Slug, ct)));
}
internal sealed class ListWorkflowTriggersHttpRequestValidator : Validator<ListWorkflowTriggersHttpRequest>
{
    public ListWorkflowTriggersHttpRequestValidator() => RuleFor(request => request.Slug).NotEmpty().MaximumLength(100);
}
