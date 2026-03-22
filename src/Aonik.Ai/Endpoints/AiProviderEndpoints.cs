using Aonik.Ai.Contracts.Models;
using Aonik.Ai.Contracts.Services;
using FastEndpoints;

namespace Aonik.Ai.Endpoints;

// ── List Providers ───────────────────────────────────────────────────

internal sealed class ListAiProvidersEndpoint : EndpointWithoutRequest<ListAiProvidersResponse>
{
    private readonly IAiModelService _service;

    public ListAiProvidersEndpoint(IAiModelService service) => _service = service;

    public override void Configure()
    {
        Get("/ai/providers");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var providers = await _service.ListProvidersAsync(ct);
        await Send.OkAsync(new ListAiProvidersResponse { Providers = providers }, ct);
    }
}

public sealed record ListAiProvidersResponse
{
    public required IReadOnlyList<AiProviderResponse> Providers { get; init; }
}

// ── Get Provider ─────────────────────────────────────────────────────

internal sealed class GetAiProviderEndpoint : Endpoint<GetAiProviderRequest, AiProviderResponse>
{
    private readonly IAiModelService _service;

    public GetAiProviderEndpoint(IAiModelService service) => _service = service;

    public override void Configure()
    {
        Get("/ai/providers/{ProviderId}");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(GetAiProviderRequest req, CancellationToken ct)
    {
        var provider = await _service.GetProviderAsync(req.ProviderId, ct);
        if (provider is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(provider, ct);
    }
}

public sealed record GetAiProviderRequest
{
    public Guid ProviderId { get; init; }
}

// ── Create Provider ──────────────────────────────────────────────────

internal sealed class CreateAiProviderEndpoint : Endpoint<CreateAiProviderRequest, AiProviderResponse>
{
    private readonly IAiModelService _service;

    public CreateAiProviderEndpoint(IAiModelService service) => _service = service;

    public override void Configure()
    {
        Post("/ai/providers");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(CreateAiProviderRequest req, CancellationToken ct)
    {
        var provider = await _service.CreateProviderAsync(req, ct);
        await Send.CreatedAtAsync<GetAiProviderEndpoint>(
            routeValues: new { ProviderId = provider.Id },
            responseBody: provider,
            cancellation: ct);
    }
}

// ── Update Provider ──────────────────────────────────────────────────

internal sealed class UpdateAiProviderEndpoint : Endpoint<UpdateAiProviderEndpointRequest, AiProviderResponse>
{
    private readonly IAiModelService _service;

    public UpdateAiProviderEndpoint(IAiModelService service) => _service = service;

    public override void Configure()
    {
        Put("/ai/providers/{ProviderId}");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(UpdateAiProviderEndpointRequest req, CancellationToken ct)
    {
        var request = new UpdateAiProviderRequest
        {
            Name = req.Name,
            AuthConfigRef = req.AuthConfigRef,
            CapabilitiesJson = req.CapabilitiesJson,
            IsActive = req.IsActive,
        };

        var provider = await _service.UpdateProviderAsync(req.ProviderId, request, ct);
        await Send.OkAsync(provider, ct);
    }
}

public sealed record UpdateAiProviderEndpointRequest
{
    public Guid ProviderId { get; init; }
    public string? Name { get; init; }
    public string? AuthConfigRef { get; init; }
    public string? CapabilitiesJson { get; init; }
    public bool? IsActive { get; init; }
}

// ── Delete Provider ──────────────────────────────────────────────────

internal sealed class DeleteAiProviderEndpoint : Endpoint<DeleteAiProviderRequest>
{
    private readonly IAiModelService _service;

    public DeleteAiProviderEndpoint(IAiModelService service) => _service = service;

    public override void Configure()
    {
        Delete("/ai/providers/{ProviderId}");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(DeleteAiProviderRequest req, CancellationToken ct)
    {
        await _service.DeleteProviderAsync(req.ProviderId, ct);
        await Send.NoContentAsync(ct);
    }
}

public sealed record DeleteAiProviderRequest
{
    public Guid ProviderId { get; init; }
}
