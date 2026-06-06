using Aonik.SharedKernel.Abstractions.Tasks;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Tasks;

/// <summary>POST /tasks/{id}/cancel — cancel a task permanently (Spec 034).</summary>
internal sealed class CancelTaskEndpoint : EndpointWithoutRequest<TaskResponse>
{
    private readonly ITaskService _taskService;

    public CancelTaskEndpoint(ITaskService taskService) => _taskService = taskService;

    public override void Configure()
    {
        Post("/tasks/{id}/cancel");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Cancel a task";
            s.Response(200, "Cancelled task");
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
            await _taskService.CancelAsync(id, ct);
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
