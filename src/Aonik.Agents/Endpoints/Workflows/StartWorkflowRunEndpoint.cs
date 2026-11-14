using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Http;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Services.Workflows;

namespace Aonik.Agents.Endpoints.Workflows;

internal sealed record StartWorkflowRunHttpRequest(string Slug, Guid WorkflowVersionId, string SubjectType, Guid SubjectId, string IdempotencyKey, string? ExpectedFactsHash = null);

internal sealed class StartWorkflowRunEndpoint(IWorkflowRunService runs) : Endpoint<StartWorkflowRunHttpRequest, WorkflowRunDetail>
{
    public override void Configure() => Post("/ai/workflows/{Slug}/runs");

    public override async Task HandleAsync(StartWorkflowRunHttpRequest request, CancellationToken ct)
    {
        try
        {
            WorkflowRunDetail result = await runs.StartAsync(Route<string>("Slug")!,
                new(request.WorkflowVersionId, request.SubjectType, request.SubjectId, request.IdempotencyKey, request.ExpectedFactsHash), ct);
            await Send.ResultAsync(Results.Json(result, statusCode: StatusCodes.Status202Accepted));
        }
        catch (KeyNotFoundException) { await Send.NotFoundAsync(ct); }
        catch (WorkflowConflictException ex) { await Send.ResultAsync(Results.Conflict(new { error = ex.Message })); }
        catch (ArgumentException ex) { await Send.ResultAsync(Results.BadRequest(new { error = ex.Message })); }
    }
}

internal sealed class StartWorkflowRunHttpRequestValidator : Validator<StartWorkflowRunHttpRequest>
{
    public StartWorkflowRunHttpRequestValidator()
    {
        RuleFor(request => request.Slug).NotEmpty().MaximumLength(100);
        RuleFor(request => request.WorkflowVersionId).NotEmpty();
        RuleFor(request => request.SubjectType).Equal("Invoice");
        RuleFor(request => request.SubjectId).NotEmpty();
        RuleFor(request => request.IdempotencyKey).NotEmpty().MaximumLength(128).Matches("^[a-zA-Z0-9][a-zA-Z0-9_.-]*$");
        RuleFor(request => request.ExpectedFactsHash).Matches("^[A-F0-9]{64}$").When(request => request.ExpectedFactsHash is not null);
    }
}
