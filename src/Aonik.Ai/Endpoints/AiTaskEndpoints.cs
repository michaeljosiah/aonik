using Aonik.Ai.Contracts.Models;
using Aonik.Ai.Contracts.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Ai.Endpoints;

// ── List AI Tasks ──────────────────────────────────────────────────

internal sealed class ListAiTasksEndpoint : EndpointWithoutRequest<ListAiTasksResponse>
{
    private readonly IAiTaskService _service;

    public ListAiTasksEndpoint(IAiTaskService service) => _service = service;

    public override void Configure()
    {
        Get("/ai/tasks");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "List AI tasks";
            s.Description = "Returns all AI task definitions, optionally filtered by category.";
            s.Response(200, "Success");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("AI Configuration"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var category = Query<string?>("category", isRequired: false);
        var tasks = await _service.ListAsync(category, ct);
        await Send.OkAsync(new ListAiTasksResponse { Tasks = tasks }, ct);
    }
}

public sealed record ListAiTasksResponse
{
    public required IReadOnlyList<AiTaskResponse> Tasks { get; init; }
}

// ── Get AI Task ────────────────────────────────────────────────────

internal sealed class GetAiTaskEndpoint : Endpoint<GetAiTaskRequest, AiTaskDetailResponse>
{
    private readonly IAiTaskService _service;

    public GetAiTaskEndpoint(IAiTaskService service) => _service = service;

    public override void Configure()
    {
        Get("/ai/tasks/{TaskId}");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "Get AI task by ID";
            s.Description = "Returns a specific AI task definition including its prompt templates, route policy details, and execution statistics.";
            s.Response(200, "Success");
            s.Response(401, "Not authenticated");
            s.Response(404, "Task not found");
        });
        Options(x => x.WithTags("AI Configuration"));
    }

    public override async Task HandleAsync(GetAiTaskRequest req, CancellationToken ct)
    {
        var task = await _service.GetDetailAsync(req.TaskId, ct);
        if (task is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(task, ct);
    }
}

public sealed record GetAiTaskRequest
{
    public Guid TaskId { get; init; }
}

// ── Create AI Task ─────────────────────────────────────────────────

internal sealed class CreateAiTaskEndpoint : Endpoint<CreateAiTaskRequest, AiTaskResponse>
{
    private readonly IAiTaskService _service;

    public CreateAiTaskEndpoint(IAiTaskService service) => _service = service;

    public override void Configure()
    {
        Post("/ai/tasks");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "Create an AI task";
            s.Description = "Creates a new AI task definition with prompt templates, variable schemas, and task metadata.";
            s.Response(201, "Task created");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("AI Configuration"));
    }

    public override async Task HandleAsync(CreateAiTaskRequest req, CancellationToken ct)
    {
        var task = await _service.CreateAsync(req, ct);
        await Send.CreatedAtAsync<GetAiTaskEndpoint>(
            routeValues: new { TaskId = task.Id },
            responseBody: task,
            cancellation: ct);
    }
}

// ── Update AI Task ─────────────────────────────────────────────────

internal sealed class UpdateAiTaskEndpoint : Endpoint<UpdateAiTaskEndpointRequest, AiTaskResponse>
{
    private readonly IAiTaskService _service;

    public UpdateAiTaskEndpoint(IAiTaskService service) => _service = service;

    public override void Configure()
    {
        Put("/ai/tasks/{TaskId}");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "Update an AI task";
            s.Description = "Updates an existing AI task definition's prompt templates, variable schemas, metadata, or active/published status.";
            s.Response(200, "Success");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
            s.Response(404, "Task not found");
        });
        Options(x => x.WithTags("AI Configuration"));
    }

    public override async Task HandleAsync(UpdateAiTaskEndpointRequest req, CancellationToken ct)
    {
        var request = new UpdateAiTaskRequest
        {
            DisplayName = req.DisplayName,
            Description = req.Description,
            Category = req.Category,
            ExecutionMode = req.ExecutionMode,
            PromptName = req.PromptName,
            PromptVersion = req.PromptVersion,
            SystemTemplate = req.SystemTemplate,
            UserTemplate = req.UserTemplate,
            DeveloperTemplate = req.DeveloperTemplate,
            VariablesSchemaJson = req.VariablesSchemaJson,
            OutputSchemaJson = req.OutputSchemaJson,
            IsPublished = req.IsPublished,
            IsActive = req.IsActive,
        };

        var task = await _service.UpdateAsync(req.TaskId, request, ct);
        await Send.OkAsync(task, ct);
    }
}

public sealed record UpdateAiTaskEndpointRequest
{
    public Guid TaskId { get; init; }
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
    public string? Category { get; init; }
    public string? ExecutionMode { get; init; }
    public string? PromptName { get; init; }
    public string? PromptVersion { get; init; }
    public string? SystemTemplate { get; init; }
    public string? UserTemplate { get; init; }
    public string? DeveloperTemplate { get; init; }
    public string? VariablesSchemaJson { get; init; }
    public string? OutputSchemaJson { get; init; }
    public bool? IsPublished { get; init; }
    public bool? IsActive { get; init; }
}

// ── Delete AI Task ─────────────────────────────────────────────────

internal sealed class DeleteAiTaskEndpoint : Endpoint<DeleteAiTaskRequest>
{
    private readonly IAiTaskService _service;

    public DeleteAiTaskEndpoint(IAiTaskService service) => _service = service;

    public override void Configure()
    {
        Delete("/ai/tasks/{TaskId}");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "Delete an AI task";
            s.Description = "Removes an AI task definition from the system.";
            s.Response(204, "Task deleted");
            s.Response(401, "Not authenticated");
            s.Response(404, "Task not found");
        });
        Options(x => x.WithTags("AI Configuration"));
    }

    public override async Task HandleAsync(DeleteAiTaskRequest req, CancellationToken ct)
    {
        await _service.DeleteAsync(req.TaskId, ct);
        await Send.NoContentAsync(ct);
    }
}

public sealed record DeleteAiTaskRequest
{
    public Guid TaskId { get; init; }
}
