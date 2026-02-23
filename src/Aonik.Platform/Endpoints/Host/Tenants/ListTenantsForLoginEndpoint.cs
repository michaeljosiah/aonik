using FastEndpoints;

using Aonik.Platform.Contracts.Api.Host;
using Aonik.Platform.Contracts.Services.Identity;

namespace Aonik.Platform.Endpoints.Host.Tenants;

/// <summary>
/// Public endpoint to list active tenants for login dropdown.
/// No authentication required - returns minimal tenant info.
/// </summary>
public class ListTenantsForLoginEndpoint : EndpointWithoutRequest<TenantListForLoginResponse>
{
    private readonly ITenantService _tenantService;

    public ListTenantsForLoginEndpoint(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    public override void Configure()
    {
        Get("/host/tenants/list-for-login");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        try
        {
            var result = await _tenantService.ListTenantsForLoginAsync(ct);

            if (ct.IsCancellationRequested)
            {
                return;
            }

            var response = new TenantListForLoginResponse(
                result.Tenants.Select(t => new TenantListItemForLoginResponse(
                    t.TenantId,
                    t.Name,
                    t.Subdomain,
                    t.Environment)).ToList());

            await Send.OkAsync(response, ct);
        }
        catch (OperationCanceledException)
        {
            // Request was canceled; no response required.
        }
    }
}
