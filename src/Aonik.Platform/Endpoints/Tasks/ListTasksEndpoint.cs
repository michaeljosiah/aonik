using Aonik.Platform.Contracts.Services.Tasks;
using Aonik.SharedKernel.Abstractions.Tasks;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Tasks;

internal sealed record ListTasksRequest
{
    [QueryParam] public string? Status { get; init; }
    [QueryParam] public int Take { get; init; } = 100;
}

/// <summary>GET /tasks — list the current tenant's tasks for the admin UI (Spec 034).</summary>
internal sealed class ListTasksEndpoint : Endpoint<ListTasksRequest, IReadOnlyList<TaskResponse>>
{
    private readonly IWorkItemAdminService _adminService;

    public ListTasksEndpoint(IWorkItemAdminService adminService) => _adminService = adminService;

    public override void Configure()
    {
        Get("/tasks");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "List tasks";
            s.Description = "Lists the current tenant's scheduled tasks, optionally filtered by status, soonest-due first.";
            s.Response(200, "Task list");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Tasks"));
    }

    public override async Task HandleAsync(ListTasksRequest req, CancellationToken ct)
    {
        var result = await _adminService.ListAsync(req.Status, req.Take, ct);
        await Send.OkAsync(result, ct);
    }
}
