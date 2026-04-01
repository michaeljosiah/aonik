using Aonik.Platform.Contracts.Api.Host;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Identity;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Platform.Endpoints.Host.Tenants;

internal class GetTenantRegistrationCountriesEndpoint : EndpointWithoutRequest<TenantRegistrationCountriesResponse>
{
    private readonly PlatformDbContext _dbContext;

    public GetTenantRegistrationCountriesEndpoint(PlatformDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public override void Configure()
    {
        Get("/host/tenants/{tenantId}/registration-countries");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var tenantId = Route<Guid>("tenantId");

        var tenant = await _dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == tenantId && item.Status == TenantStatus.Active, ct);

        if (tenant == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var supportedCountries = TenantCountryCodeSerializer.Deserialize(tenant.SupportedCountriesJson);
        var allowedOriginCountries = TenantCountryCodeSerializer.ResolveWithFallback(
            tenant.AllowedOriginCountriesJson,
            supportedCountries);

        await Send.OkAsync(
            new TenantRegistrationCountriesResponse(
                tenant.Id,
                tenant.Name,
                allowedOriginCountries),
            ct);
    }
}
