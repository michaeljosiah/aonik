using System.ComponentModel;
using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.SharedKernel.Abstractions;
using ModelContextProtocol.Server;

namespace Aonik.Platform.Mcp.Tools;

/// <summary>
/// MCP tools for tenant management operations.
/// Domain services are injected via DI into method parameters.
/// </summary>
[McpServerToolType]
public static class TenantMcpTools
{
    [McpServerTool(Name = "platform_get_tenant"), Description("Retrieves a tenant by its unique identifier. Returns full tenant details including configuration, contact info, and setup status.")]
    public static async Task<TenantResponse?> GetTenant(
        ITenantService tenantService,
        [Description("The unique identifier (GUID) of the tenant to retrieve")] Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return await tenantService.GetTenantAsync(tenantId, cancellationToken);
    }

    [McpServerTool(Name = "platform_list_tenants"), Description("Lists tenants with optional filtering by environment, status, or name. Returns a paged result.")]
    public static async Task<PagedResult<TenantResponse>> ListTenants(
        ITenantService tenantService,
        [Description("Page number (1-based, default 1)")] int pageNumber = 1,
        [Description("Page size (default 20)")] int pageSize = 20,
        [Description("Filter by environment (e.g. Production, Sandbox)")] string? environment = null,
        [Description("Filter by status (e.g. Active, Inactive)")] string? status = null,
        [Description("Filter by tenant name (partial match)")] string? nameFilter = null,
        CancellationToken cancellationToken = default)
    {
        var request = new ListTenantsRequest(pageNumber, pageSize, environment, status, nameFilter);
        return await tenantService.ListTenantsAsync(request, cancellationToken);
    }

    // platform_list_tenants_for_login was retired alongside the public
    // /host/tenants/list-for-login endpoint — exposing a directory of all
    // tenants to MCP tool calls is the same enumeration leak the web/desktop
    // org pickers were rewritten to avoid. If we need a per-user tenant
    // lookup in MCP context later, add a `ListMyTenants` tool keyed on
    // the calling user's identity.
}
