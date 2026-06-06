using Aonik.SharedKernel.Abstractions.Tasks;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Tasks;

/// <summary>GET /tasks/{id} — fetch a single task (Spec 034).</summary>
internal sealed class GetTaskEndpoint : EndpointWithoutRequest<TaskResponse>
{
    private readonly ITaskService _taskService;

    public GetTaskEndpoint(ITaskService taskService) => _taskService = taskService;

    public override void Configure()
    {
        Get("/tasks/{id}");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Get a task";
            s.Response(200, "Task");
            s.Response(404, "Task not found");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Tasks"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var result = await _taskService.GetAsync(id, ct);
        if (result is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}
