using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Tenants;

internal class GetTenantEndpoint : EndpointWithoutRequest<TenantResponse>
{
    private readonly ITenantService _tenantService;

    public GetTenantEndpoint(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    public override void Configure()
    {
        Get("/admin/tenants/{tenantId}");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Get tenant by ID";
            s.Description = "Retrieves the details of a specific tenant by its unique identifier.";
            s.Response(200, "Tenant details");
            s.Response(401, "Not authenticated");
            s.Response(404, "Tenant not found");
        });
        Options(x => x.WithTags("Tenant Administration"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var tenantId = Route<Guid>("tenantId");

        var result = await _tenantService.GetTenantAsync(tenantId, ct);

        if (result == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}
