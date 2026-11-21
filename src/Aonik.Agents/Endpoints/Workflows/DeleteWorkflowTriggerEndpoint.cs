using FastEndpoints;
using FluentValidation;

using Aonik.Agents.Contracts.Services;

namespace Aonik.Agents.Endpoints.Workflows;

internal sealed record DeleteWorkflowTriggerHttpRequest(string Slug, Guid BindingId, string ExpectedVersion);
internal sealed class DeleteWorkflowTriggerEndpoint(IWorkflowTriggerService triggers) : Endpoint<DeleteWorkflowTriggerHttpRequest>
{
    public override void Configure() => Delete("/ai/workflows/{Slug}/triggers/{BindingId:guid}");
    public override async Task HandleAsync(DeleteWorkflowTriggerHttpRequest request, CancellationToken ct)
        => await Send.ResultAsync(await WorkflowTriggerHttp.ExecuteAsync(async () => { await triggers.DeleteAsync(request.Slug, request.BindingId, request.ExpectedVersion, ct); return new { deleted = true }; }));
}
internal sealed class DeleteWorkflowTriggerHttpRequestValidator : Validator<DeleteWorkflowTriggerHttpRequest>
{
    public DeleteWorkflowTriggerHttpRequestValidator()
    { RuleFor(request => request.Slug).NotEmpty().MaximumLength(100); RuleFor(request => request.BindingId).NotEmpty(); RuleFor(request => request.ExpectedVersion).NotNull().MaximumLength(128); }
}
