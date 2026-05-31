using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Finance.Mcp.Hosting;

/// <summary>
/// Lightweight implementations of cross-cutting abstractions for the
/// MCP server process. These replace the HttpContext-based implementations
/// from Infrastructure and the Platform-provided services that are not
/// available in a headless console host.
/// </summary>

// ── Clock ────────────────────────────────────────────────────────────

internal sealed class McpSystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

// ── Tenant Provider ──────────────────────────────────────────────────

/// <summary>
/// Returns a fixed tenant ID, configurable via McpTenantId in appsettings
/// or environment variable. Defaults to a well-known dev tenant.
/// </summary>
internal sealed class McpTenantProvider : ITenantProvider
{
    private readonly Guid _tenantId;

    public McpTenantProvider(Guid tenantId) => _tenantId = tenantId;

    public Guid GetCurrentTenantId() => _tenantId;

    public bool TryGetCurrentTenantId(out Guid tenantId)
    {
        tenantId = _tenantId;
        return true;
    }
}

// ── Current User Provider ────────────────────────────────────────────

/// <summary>
/// Returns a fixed user ID representing the MCP agent user.
/// Configurable via McpUserId in appsettings.
/// </summary>
internal sealed class McpCurrentUserProvider : ICurrentUserProvider
{
    private readonly Guid _userId;

    public McpCurrentUserProvider(Guid userId) => _userId = userId;

    public Guid? GetCurrentUserId() => _userId;

    public bool TryGetCurrentUserId(out Guid userId)
    {
        userId = _userId;
        return true;
    }
}

// ── Permission Service ───────────────────────────────────────────────

/// <summary>
/// Grants all permissions in the MCP server context.
/// MCP tools are already gated by the agent framework's proposal/approval flow.
/// </summary>
internal sealed class McpPermissionService : IPermissionService
{
    public Task<bool> HasPermissionAsync(Guid userId, string permissionKey, CancellationToken ct = default)
        => Task.FromResult(true);

    public Task<List<string>> GetUserPermissionsAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult(new List<string> { "*" });
}

// ── Audit Log Writer ─────────────────────────────────────────────────

/// <summary>
/// Logs audit events to the console in the MCP server context.
/// In production this would write to the audit table or external sink.
/// </summary>
internal sealed class McpAuditLogWriter : IAuditLogWriter
{
    public Task LogAsync(
        string action,
        string resourceType,
        Guid resourceId,
        Guid tenantId,
        Guid? actorId,
        string? correlationId,
        string? detailsJson = null,
        CancellationToken cancellationToken = default)
    {
        Console.Error.WriteLine(
            $"[AUDIT] {action} {resourceType}/{resourceId} tenant={tenantId} actor={actorId} correlation={correlationId}");
        return Task.CompletedTask;
    }
}

// ── Party Service ────────────────────────────────────────────────────

/// <summary>
/// Stub party service for MCP context. Returns not-found for lookups.
/// Party mutations via MCP should go through the Platform MCP server instead.
/// </summary>
internal sealed class McpPartyService : IPartyService
{
    public Task<PartyResponse> CreatePartyAsync(CreatePartyRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Party creation is not available via the Finance MCP server. Use the Platform MCP server.");

    public Task<PartyResponse?> GetPartyAsync(Guid partyId, CancellationToken cancellationToken = default)
        => Task.FromResult<PartyResponse?>(null);

    public Task<RelatedPartyResponse> CreateRelatedPartyAsync(CreateRelatedPartyRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Related party creation is not available via the Finance MCP server. Use the Platform MCP server.");

    public Task<PartyRelationshipResponse> CreateRelationshipAsync(CreatePartyRelationshipRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Party relationship creation is not available via the Finance MCP server. Use the Platform MCP server.");

    public Task<IReadOnlyList<PartyRelationshipResponse>> GetRelationshipsAsync(Guid partyId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PartyRelationshipResponse>>(Array.Empty<PartyRelationshipResponse>());

    public Task AssignPartyRoleAsync(Guid partyId, string role, string contextType, Guid contextId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Party role assignment is not available via the Finance MCP server. Use the Platform MCP server.");
}

// ── Compliance Service ───────────────────────────────────────────────

/// <summary>
/// Stub compliance service for MCP context.
/// Returns "clear" results — compliance checks should be done via Platform MCP.
/// </summary>
internal sealed class McpComplianceService : IComplianceService
{
    public Task<ScreeningResult> ScreenPartyAsync(Guid partyId, string checkType, CancellationToken cancellationToken = default)
        => Task.FromResult(new ScreeningResult(Guid.NewGuid(), partyId, checkType, "Clear", "Auto-Clear-MCP", DateTime.UtcNow));

    public Task<ComplianceCaseResponse> CreateOrderReviewCaseAsync(Guid orderId, CancellationToken cancellationToken = default)
        => Task.FromResult(new ComplianceCaseResponse(Guid.NewGuid(), "OrderReview", "Open", orderId, null));

    public Task<bool> RequiresComplianceReviewAsync(Guid orderId, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}

// ── Tenant Currency Provider ─────────────────────────────────────────

/// <summary>
/// Returns a default currency list for the MCP context.
/// </summary>
internal sealed class McpTenantCurrencyProvider : ITenantCurrencyProvider
{
    public Task<List<string>> GetTenantCurrencyCodesAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => Task.FromResult(new List<string> { "USD", "NGN", "GBP", "EUR" });
}
