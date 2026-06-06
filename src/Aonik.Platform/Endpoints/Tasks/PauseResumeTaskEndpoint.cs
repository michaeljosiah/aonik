using Aonik.SharedKernel.Abstractions.Tasks;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Tasks;

/// <summary>POST /tasks/{id}/pause — pause a scheduled task so the dispatcher skips it (Spec 034).</summary>
internal sealed class PauseTaskEndpoint : EndpointWithoutRequest<TaskResponse>
{
    private readonly ITaskService _taskService;

    public PauseTaskEndpoint(ITaskService taskService) => _taskService = taskService;

    public override void Configure()
    {
        Post("/tasks/{id}/pause");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Pause a task";
            s.Response(200, "Paused task");
            s.Response(404, "Task not found");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Tasks"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        try
        {
            await _taskService.PauseAsync(id, ct);
        }
        catch (KeyNotFoundException)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var updated = await _taskService.GetAsync(id, ct);
        if (updated is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(updated, ct);
    }
}

/// <summary>POST /tasks/{id}/resume — resume a paused task, re-arming its next occurrence (Spec 034).</summary>
internal sealed class ResumeTaskEndpoint : EndpointWithoutRequest<TaskResponse>
{
    private readonly ITaskService _taskService;

    public ResumeTaskEndpoint(ITaskService taskService) => _taskService = taskService;

    public override void Configure()
    {
        Post("/tasks/{id}/resume");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Resume a task";
            s.Response(200, "Resumed task");
            s.Response(404, "Task not found");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Tasks"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        try
        {
            await _taskService.ResumeAsync(id, ct);
        }
        catch (KeyNotFoundException)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var updated = await _taskService.GetAsync(id, ct);
        if (updated is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(updated, ct);
    }
}
