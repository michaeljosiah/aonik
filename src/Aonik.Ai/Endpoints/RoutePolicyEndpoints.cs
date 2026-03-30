using Aonik.Ai.Contracts.Models;
using Aonik.Ai.Contracts.Services;
using FastEndpoints;

namespace Aonik.Ai.Endpoints;

// ── List Route Policies ─────────────────────────────────────────────

internal sealed class ListRoutePoliciesEndpoint : EndpointWithoutRequest<ListRoutePoliciesResponse>
{
    private readonly IRoutePolicyService _service;

    public ListRoutePoliciesEndpoint(IRoutePolicyService service) => _service = service;

    public override void Configure()
    {
        Get("/ai/route-policies");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var useCase = Query<string?>("useCase", isRequired: false);
        var policies = await _service.ListAsync(useCase, ct);
        await Send.OkAsync(new ListRoutePoliciesResponse { Policies = policies }, ct);
    }
}

public sealed record ListRoutePoliciesResponse
{
    public required IReadOnlyList<RoutePolicyResponse> Policies { get; init; }
}

// ── Get Route Policy ────────────────────────────────────────────────

internal sealed class GetRoutePolicyEndpoint : Endpoint<GetRoutePolicyRequest, RoutePolicyResponse>
{
    private readonly IRoutePolicyService _service;

    public GetRoutePolicyEndpoint(IRoutePolicyService service) => _service = service;

    public override void Configure()
    {
        Get("/ai/route-policies/{PolicyId}");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(GetRoutePolicyRequest req, CancellationToken ct)
    {
        var policy = await _service.GetAsync(req.PolicyId, ct);
        if (policy is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(policy, ct);
    }
}

public sealed record GetRoutePolicyRequest
{
    public Guid PolicyId { get; init; }
}

// ── Create Route Policy ─────────────────────────────────────────────

internal sealed class CreateRoutePolicyEndpoint : Endpoint<CreateRoutePolicyRequest, RoutePolicyResponse>
{
    private readonly IRoutePolicyService _service;

    public CreateRoutePolicyEndpoint(IRoutePolicyService service) => _service = service;

    public override void Configure()
    {
        Post("/ai/route-policies");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(CreateRoutePolicyRequest req, CancellationToken ct)
    {
        var policy = await _service.CreateAsync(req, ct);
        await Send.CreatedAtAsync<GetRoutePolicyEndpoint>(
            routeValues: new { PolicyId = policy.Id },
            responseBody: policy,
            cancellation: ct);
    }
}

// ── Update Route Policy ─────────────────────────────────────────────

internal sealed class UpdateRoutePolicyEndpoint : Endpoint<UpdateRoutePolicyEndpointRequest, RoutePolicyResponse>
{
    private readonly IRoutePolicyService _service;

    public UpdateRoutePolicyEndpoint(IRoutePolicyService service) => _service = service;

    public override void Configure()
    {
        Put("/ai/route-policies/{PolicyId}");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(UpdateRoutePolicyEndpointRequest req, CancellationToken ct)
    {
        var request = new UpdateRoutePolicyRequest
        {
            RiskTier = req.RiskTier,
            DataSensitivity = req.DataSensitivity,
            CostCeiling = req.CostCeiling,
            PrimaryModelId = req.PrimaryModelId,
            FallbackModelIdsJson = req.FallbackModelIdsJson,
            IsActive = req.IsActive,
        };

        var policy = await _service.UpdateAsync(req.PolicyId, request, ct);
        await Send.OkAsync(policy, ct);
    }
}

public sealed record UpdateRoutePolicyEndpointRequest
{
    public Guid PolicyId { get; init; }
    public string? RiskTier { get; init; }
    public string? DataSensitivity { get; init; }
    public decimal? CostCeiling { get; init; }
    public Guid? PrimaryModelId { get; init; }
    public string? FallbackModelIdsJson { get; init; }
    public bool? IsActive { get; init; }
}

// ── Delete Route Policy ─────────────────────────────────────────────

internal sealed class DeleteRoutePolicyEndpoint : Endpoint<DeleteRoutePolicyRequest>
{
    private readonly IRoutePolicyService _service;

    public DeleteRoutePolicyEndpoint(IRoutePolicyService service) => _service = service;

    public override void Configure()
    {
        Delete("/ai/route-policies/{PolicyId}");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(DeleteRoutePolicyRequest req, CancellationToken ct)
    {
        await _service.DeleteAsync(req.PolicyId, ct);
        await Send.NoContentAsync(ct);
    }
}

public sealed record DeleteRoutePolicyRequest
{
    public Guid PolicyId { get; init; }
}
