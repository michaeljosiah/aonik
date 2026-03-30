using Aonik.Ai.Contracts.Models;
using Aonik.Ai.Contracts.Services;
using FastEndpoints;

namespace Aonik.Ai.Endpoints;

// ── List Prompt Specs ───────────────────────────────────────────────

internal sealed class ListPromptSpecsEndpoint : EndpointWithoutRequest<ListPromptSpecsResponse>
{
    private readonly IPromptSpecService _service;

    public ListPromptSpecsEndpoint(IPromptSpecService service) => _service = service;

    public override void Configure()
    {
        Get("/ai/prompts");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var name = Query<string?>("name", isRequired: false);
        var prompts = await _service.ListAsync(name, ct);
        await Send.OkAsync(new ListPromptSpecsResponse { Prompts = prompts }, ct);
    }
}

public sealed record ListPromptSpecsResponse
{
    public required IReadOnlyList<PromptSpecResponse> Prompts { get; init; }
}

// ── Get Prompt Spec ─────────────────────────────────────────────────

internal sealed class GetPromptSpecEndpoint : Endpoint<GetPromptSpecRequest, PromptSpecResponse>
{
    private readonly IPromptSpecService _service;

    public GetPromptSpecEndpoint(IPromptSpecService service) => _service = service;

    public override void Configure()
    {
        Get("/ai/prompts/{PromptId}");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(GetPromptSpecRequest req, CancellationToken ct)
    {
        var prompt = await _service.GetAsync(req.PromptId, ct);
        if (prompt is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(prompt, ct);
    }
}

public sealed record GetPromptSpecRequest
{
    public Guid PromptId { get; init; }
}

// ── Create Prompt Spec ──────────────────────────────────────────────

internal sealed class CreatePromptSpecEndpoint : Endpoint<CreatePromptSpecRequest, PromptSpecResponse>
{
    private readonly IPromptSpecService _service;

    public CreatePromptSpecEndpoint(IPromptSpecService service) => _service = service;

    public override void Configure()
    {
        Post("/ai/prompts");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(CreatePromptSpecRequest req, CancellationToken ct)
    {
        var prompt = await _service.CreateAsync(req, ct);
        await Send.CreatedAtAsync<GetPromptSpecEndpoint>(
            routeValues: new { PromptId = prompt.Id },
            responseBody: prompt,
            cancellation: ct);
    }
}

// ── Update Prompt Spec ──────────────────────────────────────────────

internal sealed class UpdatePromptSpecEndpoint : Endpoint<UpdatePromptSpecEndpointRequest, PromptSpecResponse>
{
    private readonly IPromptSpecService _service;

    public UpdatePromptSpecEndpoint(IPromptSpecService service) => _service = service;

    public override void Configure()
    {
        Put("/ai/prompts/{PromptId}");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(UpdatePromptSpecEndpointRequest req, CancellationToken ct)
    {
        var request = new UpdatePromptSpecRequest
        {
            SystemTemplate = req.SystemTemplate,
            UserTemplate = req.UserTemplate,
            DeveloperTemplate = req.DeveloperTemplate,
            VariablesSchemaJson = req.VariablesSchemaJson,
            OutputSchemaJson = req.OutputSchemaJson,
            SafetyPolicyRef = req.SafetyPolicyRef,
            IsPublished = req.IsPublished,
        };

        var prompt = await _service.UpdateAsync(req.PromptId, request, ct);
        await Send.OkAsync(prompt, ct);
    }
}

public sealed record UpdatePromptSpecEndpointRequest
{
    public Guid PromptId { get; init; }
    public string? SystemTemplate { get; init; }
    public string? UserTemplate { get; init; }
    public string? DeveloperTemplate { get; init; }
    public string? VariablesSchemaJson { get; init; }
    public string? OutputSchemaJson { get; init; }
    public string? SafetyPolicyRef { get; init; }
    public bool? IsPublished { get; init; }
}

// ── Delete Prompt Spec ──────────────────────────────────────────────

internal sealed class DeletePromptSpecEndpoint : Endpoint<DeletePromptSpecRequest>
{
    private readonly IPromptSpecService _service;

    public DeletePromptSpecEndpoint(IPromptSpecService service) => _service = service;

    public override void Configure()
    {
        Delete("/ai/prompts/{PromptId}");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(DeletePromptSpecRequest req, CancellationToken ct)
    {
        await _service.DeleteAsync(req.PromptId, ct);
        await Send.NoContentAsync(ct);
    }
}

public sealed record DeletePromptSpecRequest
{
    public Guid PromptId { get; init; }
}
