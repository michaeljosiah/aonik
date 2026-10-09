using Aonik.SharedKernel.Abstractions.Tasks;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Tasks;

/// <summary>POST /tasks — schedule a data-defined task (Spec 034). Rejects an unknown ActionType at the boundary.</summary>
internal sealed class ScheduleTaskEndpoint : Endpoint<ScheduleTaskApiRequest, TaskResponse>
{
    private readonly ITaskService _taskService;

    public ScheduleTaskEndpoint(ITaskService taskService) => _taskService = taskService;

    public override void Configure()
    {
        Post("/tasks");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Schedule a task";
            s.Description = "Creates a durable, data-defined unit of future work. An unknown ActionType is rejected.";
            s.Response(201, "Task scheduled");
            s.Response(400, "Invalid request (e.g. unknown action type or bad cron)");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Tasks"));
    }

    public override async Task HandleAsync(ScheduleTaskApiRequest req, CancellationToken ct)
    {
        try
        {
            var result = await _taskService.ScheduleAsync(new ScheduleTaskRequest(req.Title, req.Kind,
                req.ActionType, req.ActionPayloadJson, req.AssigneeType, req.AssigneeId, req.AssigneeKey,
                req.SubjectType, req.SubjectId, req.RunAtUtc, req.RecurrenceCron, req.Timezone,
                req.StartAtUtc, req.EndAtUtc, req.MaxRuns, req.Description, req.Priority,
                req.CorrelationId, req.SourceModule), ct);
            await Send.CreatedAtAsync<GetTaskEndpoint>(
                routeValues: new { id = result.Id },
                responseBody: result,
                cancellation: ct);
        }
        catch (ArgumentException ex)
        {
            AddError(ex.Message);
            await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
        }
    }
}

// Public clients cannot choose the internal source-bound task identity.
public sealed record ScheduleTaskApiRequest(
    string Title, string Kind, string ActionType, string ActionPayloadJson, string AssigneeType,
    Guid? AssigneeId = null, string? AssigneeKey = null, string? SubjectType = null, Guid? SubjectId = null,
    DateTime? RunAtUtc = null, string? RecurrenceCron = null, string? Timezone = null,
    DateTime? StartAtUtc = null, DateTime? EndAtUtc = null, int? MaxRuns = null,
    string? Description = null, int Priority = 0, string? CorrelationId = null, string? SourceModule = null);
