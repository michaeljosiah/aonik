using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Http;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Services.Workflows;

namespace Aonik.Agents.Endpoints.Workflows;

internal sealed record WorkflowRunHttpCommand(Guid RunId, string ExpectedVersion);

internal sealed class ResumeWorkflowRunEndpoint(IWorkflowRunService runs) : Endpoint<WorkflowRunHttpCommand, WorkflowRunDetail>
{
    public override void Configure() => Post("/ai/workflow-runs/{RunId:guid}/resume");

    public override async Task HandleAsync(WorkflowRunHttpCommand request, CancellationToken ct)
    {
        try { await Send.OkAsync(await runs.ResumeAsync(Route<Guid>("RunId"), new(request.ExpectedVersion), ct), ct); }
        catch (KeyNotFoundException) { await Send.NotFoundAsync(ct); }
        catch (WorkflowConflictException ex) { await Send.ResultAsync(Results.Conflict(new { error = ex.Message })); }
    }
}

internal sealed class WorkflowRunHttpCommandValidator : Validator<WorkflowRunHttpCommand>
{
    public WorkflowRunHttpCommandValidator()
    {
        RuleFor(request => request.RunId).NotEmpty();
        RuleFor(request => request.ExpectedVersion).NotNull().MaximumLength(128);
    }
}
