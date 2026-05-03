using Aonik.Ai.Entities;
using Aonik.Ai.Persistence;
using Aonik.Ai.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ZiggyCreatures.Caching.Fusion;

namespace Aonik.Ai.Endpoints;

// ── Get Agent Settings ─────────────────────────────────────────────────

/// <summary>
/// Returns the current per-tenant agent runtime settings, primarily the
/// global kill-switch state. Always returns 200 — tenants without a row
/// see the default disengaged state. The Policies UI banner reads this
/// to decide whether to render the red engaged variant.
/// </summary>
internal sealed class GetTenantAgentSettingsEndpoint : EndpointWithoutRequest<TenantAgentSettingsResponse>
{
    private readonly AiDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;

    public GetTenantAgentSettingsEndpoint(AiDbContext dbContext, ITenantProvider tenantProvider)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
    }

    public override void Configure()
    {
        Get("/admin/ai/agent-settings");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Get tenant agent settings";
            s.Description = "Returns kill-switch state for the current tenant. Defaults to disengaged.";
            s.Response(200, "Settings (or defaults)");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("AI Configuration"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var row = await _dbContext.TenantAgentSettings
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);

        await Send.OkAsync(MapToResponse(row), ct);
    }

    internal static TenantAgentSettingsResponse MapToResponse(TenantAgentSettings? row) => new()
    {
        KillSwitchEngaged = row?.KillSwitchEngaged ?? false,
        KillSwitchEngagedAt = row?.KillSwitchEngagedAt,
        KillSwitchEngagedByUserId = row?.KillSwitchEngagedByUserId,
        UpdatedAt = row?.UpdatedAt ?? row?.CreatedAt,
    };
}

// ── Update Agent Settings (kill switch toggle) ─────────────────────────

/// <summary>
/// Toggles the kill-switch state for the current tenant. Creates the row
/// on first engage and updates the timestamp + actor on every transition.
///
/// NOTE: This currently only persists state — the orchestration pipeline
/// does not yet check the kill switch before invoking a model. Wiring
/// that enforcement is a separate task; this endpoint provides the API
/// + persistence so the UI surface is honest about what's stored.
/// </summary>
internal sealed class UpdateTenantAgentSettingsEndpoint : Endpoint<UpdateTenantAgentSettingsRequest, TenantAgentSettingsResponse>
{
    private readonly AiDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IClock _clock;
    private readonly IFusionCache _cache;

    public UpdateTenantAgentSettingsEndpoint(
        AiDbContext dbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        IClock clock,
        IFusionCache cache)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _clock = clock;
        _cache = cache;
    }

    public override void Configure()
    {
        Patch("/admin/ai/agent-settings");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Update tenant agent settings";
            s.Description =
                "Engage or release the kill switch for the current tenant. " +
                "Engagement stamps the engaging user and timestamp.";
            s.Response(200, "Updated");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("AI Configuration"));
    }

    public override async Task HandleAsync(UpdateTenantAgentSettingsRequest req, CancellationToken ct)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var row = await _dbContext.TenantAgentSettings
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);

        if (row is null)
        {
            row = new TenantAgentSettings
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                KillSwitchEngaged = false,
            };
            _dbContext.TenantAgentSettings.Add(row);
        }

        if (req.KillSwitchEngaged.HasValue && req.KillSwitchEngaged.Value != row.KillSwitchEngaged)
        {
            row.KillSwitchEngaged = req.KillSwitchEngaged.Value;
            if (row.KillSwitchEngaged)
            {
                row.KillSwitchEngagedAt = _clock.UtcNow;
                row.KillSwitchEngagedByUserId = _currentUserProvider.GetCurrentUserId();
            }
            else
            {
                row.KillSwitchEngagedAt = null;
                row.KillSwitchEngagedByUserId = null;
            }
        }

        await _dbContext.SaveChangesAsync(ct);

        // Invalidate the kill-switch cache so AiRunWriter sees the new
        // state on the immediately following AiRun start.
        await _cache.RemoveAsync(AiRunWriter.KillSwitchCacheKey(tenantId), token: ct);

        await Send.OkAsync(GetTenantAgentSettingsEndpoint.MapToResponse(row), ct);
    }
}

public sealed record UpdateTenantAgentSettingsRequest
{
    public bool? KillSwitchEngaged { get; init; }
}

public sealed class TenantAgentSettingsResponse
{
    public bool KillSwitchEngaged { get; init; }
    public DateTime? KillSwitchEngagedAt { get; init; }
    public Guid? KillSwitchEngagedByUserId { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
