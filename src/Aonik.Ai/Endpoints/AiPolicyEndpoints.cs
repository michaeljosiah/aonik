using Aonik.Ai.Entities;
using Aonik.Ai.Persistence;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Ai.Endpoints;

// ── List AI Policies ───────────────────────────────────────────────────

/// <summary>
/// Returns all <see cref="AiPolicy"/> rows. Policies are global (no tenant
/// scoping on the entity), so this is a flat list. Used by the AI Policies
/// admin page (Wave 7b).
/// </summary>
internal sealed class ListAiPoliciesEndpoint : EndpointWithoutRequest<ListAiPoliciesResponse>
{
    private readonly AiDbContext _dbContext;

    public ListAiPoliciesEndpoint(AiDbContext dbContext) => _dbContext = dbContext;

    public override void Configure()
    {
        Get("/admin/ai/policies");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "List AI policies";
            s.Description =
                "Returns the global registry of AI policies (PII redaction, banned actions, escalation rules). " +
                "Read-only — mutation lives behind dedicated configuration tooling.";
            s.Response(200, "Success");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("AI Configuration"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var page = Query<int?>("page", isRequired: false) ?? 1;
        var pageSize = Query<int?>("pageSize", isRequired: false) ?? 50;
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 200) pageSize = 200;

        var totalCount = await _dbContext.AiPolicies.CountAsync(ct);

        var rows = await _dbContext.AiPolicies
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new AiPolicySummaryResponse
            {
                Id = p.Id,
                Name = p.Name,
                IsActive = p.IsActive,
                AllowedDataFieldsJson = p.AllowedDataFieldsJson,
                RedactionRulesJson = p.RedactionRulesJson,
                BannedActionsJson = p.BannedActionsJson,
                EscalationRulesJson = p.EscalationRulesJson,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
            })
            .ToListAsync(ct);

        await Send.OkAsync(new ListAiPoliciesResponse(rows, totalCount, page, pageSize), ct);
    }
}

public sealed record ListAiPoliciesResponse(
    IReadOnlyList<AiPolicySummaryResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed class AiPolicySummaryResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public string AllowedDataFieldsJson { get; init; } = string.Empty;
    public string RedactionRulesJson { get; init; } = string.Empty;
    public string BannedActionsJson { get; init; } = string.Empty;
    public string EscalationRulesJson { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

// ── Update AI Policy (toggle IsActive) ─────────────────────────────────

/// <summary>
/// Toggles the <c>IsActive</c> flag on an <see cref="AiPolicy"/>. Scoped
/// down to the single mutable field the Policies UI exposes today; the
/// JSON columns are still owned by configuration tooling.
/// </summary>
internal sealed class UpdateAiPolicyEndpoint : Endpoint<UpdateAiPolicyEndpointRequest, AiPolicySummaryResponse>
{
    private readonly AiDbContext _dbContext;

    public UpdateAiPolicyEndpoint(AiDbContext dbContext) => _dbContext = dbContext;

    public override void Configure()
    {
        Patch("/admin/ai/policies/{Id}");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Update an AI policy";
            s.Description =
                "Currently exposes only IsActive — flips a policy on or off. " +
                "JSON column edits are routed through configuration tooling.";
            s.Response(200, "Updated");
            s.Response(401, "Not authenticated");
            s.Response(404, "Policy not found");
        });
        Options(x => x.WithTags("AI Configuration"));
    }

    public override async Task HandleAsync(UpdateAiPolicyEndpointRequest req, CancellationToken ct)
    {
        var policy = await _dbContext.AiPolicies.FirstOrDefaultAsync(p => p.Id == req.Id, ct);
        if (policy is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (req.IsActive.HasValue)
        {
            policy.IsActive = req.IsActive.Value;
        }

        await _dbContext.SaveChangesAsync(ct);

        await Send.OkAsync(new AiPolicySummaryResponse
        {
            Id = policy.Id,
            Name = policy.Name,
            IsActive = policy.IsActive,
            AllowedDataFieldsJson = policy.AllowedDataFieldsJson,
            RedactionRulesJson = policy.RedactionRulesJson,
            BannedActionsJson = policy.BannedActionsJson,
            EscalationRulesJson = policy.EscalationRulesJson,
            CreatedAt = policy.CreatedAt,
            UpdatedAt = policy.UpdatedAt,
        }, ct);
    }
}

public sealed record UpdateAiPolicyEndpointRequest
{
    public Guid Id { get; init; }
    public bool? IsActive { get; init; }
}
