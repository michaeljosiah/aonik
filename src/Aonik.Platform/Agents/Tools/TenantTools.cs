using System.ComponentModel;
using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.SharedKernel.Abstractions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Platform.Agents.Tools;

/// <summary>
/// AI agent tools for tenant management operations.
/// Read-only tools are safe for autonomous use; mutating tools should go through
/// the proposal pattern at the agent level.
/// </summary>
internal sealed class TenantTools
{
    private readonly ITenantService _tenantService;

    private TenantTools(ITenantService tenantService) => _tenantService = tenantService;

    [Description("Retrieves a tenant by its unique identifier. Returns full tenant details including configuration, contact info, and setup status.")]
    public async Task<TenantResponse?> GetTenant(
        [Description("The unique identifier (GUID) of the tenant to retrieve")] Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return await _tenantService.GetTenantAsync(tenantId, cancellationToken);
    }

    [Description("Lists tenants with optional filtering by environment, status, or name. Returns a paged result.")]
    public async Task<PagedResult<TenantResponse>> ListTenants(
        [Description("Page number (1-based, default 1)")] int pageNumber = 1,
        [Description("Page size (default 20)")] int pageSize = 20,
        [Description("Filter by environment (e.g. Production, Sandbox)")] string? environment = null,
        [Description("Filter by status (e.g. Active, Inactive)")] string? status = null,
        [Description("Filter by tenant name (partial match)")] string? nameFilter = null,
        CancellationToken cancellationToken = default)
    {
        var request = new ListTenantsRequest(pageNumber, pageSize, environment, status, nameFilter);
        return await _tenantService.ListTenantsAsync(request, cancellationToken);
    }

    [Description("Lists active tenants available for login. Returns minimal info: ID, name, subdomain, environment.")]
    public async Task<TenantListForLoginResponse> ListTenantsForLogin(
        CancellationToken cancellationToken = default)
    {
        return await _tenantService.ListTenantsForLoginAsync(cancellationToken);
    }

    /// <summary>
    /// Creates <see cref="AITool"/> instances for all tenant tools.
    /// </summary>
    public static IEnumerable<AITool> CreateAll(IServiceProvider serviceProvider)
    {
        var tools = new TenantTools(serviceProvider.GetRequiredService<ITenantService>());

        yield return AIFunctionFactory.Create(tools.GetTenant, name: "platform_get_tenant");
        yield return AIFunctionFactory.Create(tools.ListTenants, name: "platform_list_tenants");
        yield return AIFunctionFactory.Create(tools.ListTenantsForLogin, name: "platform_list_tenants_for_login");
    }
}
