using Aonik.Platform.Contracts.Models.Settings;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using FastEndpoints;
using Microsoft.Extensions.Primitives;

namespace Aonik.Platform.Endpoints.Settings;

public class GetPublicBusinessProfileEndpoint(
    IPublicBusinessProfileService profiles,
    ITenantProvider tenantProvider) : EndpointWithoutRequest<PublicBusinessProfileDto>
{
    public override void Configure()
    {
        Get("/v1/business-profile");
        AllowAnonymous();
        Summary(s => s.Summary = "Get the current tenant's explicitly published business profile.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        const string tenantHeader = "X-Tenant-Id";
        HttpContext.Response.Headers.CacheControl = "no-store";
        HttpContext.Response.Headers.Vary = StringValues.Concat(HttpContext.Response.Headers.Vary, tenantHeader);

        var profile = await profiles.GetCurrentAsync(ct);
        if (profile is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        // A headerless authenticated response cannot share a cache key with another tenant.
        if (Guid.TryParse(HttpContext.Request.Headers[tenantHeader], out var headerTenantId)
            && tenantProvider.TryGetCurrentTenantId(out var tenantId) && tenantId == headerTenantId)
            HttpContext.Response.Headers.CacheControl = "public, max-age=300";

        await Send.OkAsync(profile, ct);
    }
}
