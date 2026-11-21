using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Http;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Services.Workflows;

namespace Aonik.Agents.Endpoints.Workflows;

internal static class WorkflowTriggerHttp
{
    public static async Task<IResult> ExecuteAsync<T>(Func<Task<T>> action, bool accepted = false)
    {
        try { T value = await action(); return value is null ? Results.NotFound() : Results.Json(value, statusCode: accepted ? 202 : 200); }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (WorkflowConflictException exception) { return Results.Conflict(new { error = exception.Message }); }
        catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
    }
}
internal sealed record WorkflowTriggerReadHttpRequest(string Slug, Guid BindingId);
internal sealed class WorkflowTriggerReadHttpRequestValidator : Validator<WorkflowTriggerReadHttpRequest>
{
    public WorkflowTriggerReadHttpRequestValidator() { RuleFor(request => request.Slug).NotEmpty().MaximumLength(100); RuleFor(request => request.BindingId).NotEmpty(); }
}
internal sealed class SaveWorkflowTriggerRequestValidator : Validator<SaveWorkflowTriggerRequest>
{
    public SaveWorkflowTriggerRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(160);
        RuleFor(request => request.Definition).NotNull();
        RuleFor(request => request.ExpectedVersion).MaximumLength(128);
    }
}
internal sealed class WorkflowTriggerCommandValidator : Validator<WorkflowTriggerCommand>
{
    public WorkflowTriggerCommandValidator() { RuleFor(request => request.ExpectedVersion).NotNull().MaximumLength(128); RuleFor(request => request.DefinitionHash).Matches("^[a-f0-9]{64}$"); }
}
