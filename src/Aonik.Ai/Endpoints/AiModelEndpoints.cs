using Aonik.Ai.Contracts.Models;
using Aonik.Ai.Contracts.Services;
using FastEndpoints;

namespace Aonik.Ai.Endpoints;

// ── List Models ─────────────────────────────────────────────────────

internal sealed class ListAiModelsEndpoint : Endpoint<ListAiModelsRequest, ListAiModelsResponse>
{
    private readonly IAiModelService _service;

    public ListAiModelsEndpoint(IAiModelService service) => _service = service;

    public override void Configure()
    {
        Get("/ai/models");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(ListAiModelsRequest req, CancellationToken ct)
    {
        var models = await _service.ListModelsAsync(req.ProviderId, ct);
        await Send.OkAsync(new ListAiModelsResponse { Models = models }, ct);
    }
}

public sealed record ListAiModelsRequest
{
    [QueryParam]
    public Guid? ProviderId { get; init; }
}

public sealed record ListAiModelsResponse
{
    public required IReadOnlyList<AiModelResponse> Models { get; init; }
}

// ── Get Model ───────────────────────────────────────────────────────

internal sealed class GetAiModelEndpoint : Endpoint<GetAiModelRequest, AiModelResponse>
{
    private readonly IAiModelService _service;

    public GetAiModelEndpoint(IAiModelService service) => _service = service;

    public override void Configure()
    {
        Get("/ai/models/{ModelId}");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(GetAiModelRequest req, CancellationToken ct)
    {
        var model = await _service.GetModelAsync(req.ModelId, ct);
        if (model is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(model, ct);
    }
}

public sealed record GetAiModelRequest
{
    public Guid ModelId { get; init; }
}

// ── Create Model ────────────────────────────────────────────────────

internal sealed class CreateAiModelEndpoint : Endpoint<CreateAiModelRequest, AiModelResponse>
{
    private readonly IAiModelService _service;

    public CreateAiModelEndpoint(IAiModelService service) => _service = service;

    public override void Configure()
    {
        Post("/ai/models");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(CreateAiModelRequest req, CancellationToken ct)
    {
        var model = await _service.CreateModelAsync(req, ct);
        await Send.CreatedAtAsync<GetAiModelEndpoint>(
            routeValues: new { ModelId = model.Id },
            responseBody: model,
            cancellation: ct);
    }
}

// ── Update Model ────────────────────────────────────────────────────

internal sealed class UpdateAiModelEndpoint : Endpoint<UpdateAiModelEndpointRequest, AiModelResponse>
{
    private readonly IAiModelService _service;

    public UpdateAiModelEndpoint(IAiModelService service) => _service = service;

    public override void Configure()
    {
        Put("/ai/models/{ModelId}");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(UpdateAiModelEndpointRequest req, CancellationToken ct)
    {
        var request = new UpdateAiModelRequest
        {
            ModelName = req.ModelName,
            ContextWindow = req.ContextWindow,
            CostProfileJson = req.CostProfileJson,
            LatencyProfileJson = req.LatencyProfileJson,
            PolicyTagsJson = req.PolicyTagsJson,
            IsActive = req.IsActive,
        };

        var model = await _service.UpdateModelAsync(req.ModelId, request, ct);
        await Send.OkAsync(model, ct);
    }
}

public sealed record UpdateAiModelEndpointRequest
{
    public Guid ModelId { get; init; }
    public string? ModelName { get; init; }
    public int? ContextWindow { get; init; }
    public string? CostProfileJson { get; init; }
    public string? LatencyProfileJson { get; init; }
    public string? PolicyTagsJson { get; init; }
    public bool? IsActive { get; init; }
}

// ── Delete Model ────────────────────────────────────────────────────

internal sealed class DeleteAiModelEndpoint : Endpoint<DeleteAiModelRequest>
{
    private readonly IAiModelService _service;

    public DeleteAiModelEndpoint(IAiModelService service) => _service = service;

    public override void Configure()
    {
        Delete("/ai/models/{ModelId}");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(DeleteAiModelRequest req, CancellationToken ct)
    {
        await _service.DeleteModelAsync(req.ModelId, ct);
        await Send.NoContentAsync(ct);
    }
}

public sealed record DeleteAiModelRequest
{
    public Guid ModelId { get; init; }
}
