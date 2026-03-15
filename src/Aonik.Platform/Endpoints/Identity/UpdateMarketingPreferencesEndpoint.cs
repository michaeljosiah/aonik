using Microsoft.AspNetCore.Http;
using FastEndpoints;

using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.SharedKernel.Abstractions;

using ApiUpdateMarketingPreferencesRequest = Aonik.Platform.Contracts.Api.Identity.UpdateMarketingPreferencesRequest;
using ApiMarketingPreferencesResponse = Aonik.Platform.Contracts.Api.Identity.MarketingPreferencesResponse;

namespace Aonik.Platform.Endpoints.Identity;

public class UpdateMarketingPreferencesEndpoint : Endpoint<ApiUpdateMarketingPreferencesRequest, ApiMarketingPreferencesResponse>
{
    private readonly IUserProfileService _userProfileService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;

    public UpdateMarketingPreferencesEndpoint(
        IUserProfileService userProfileService,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider)
    {
        _userProfileService = userProfileService;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
    }

    public override void Configure()
    {
        Put("/profiles/customers/me/marketing");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(ApiUpdateMarketingPreferencesRequest req, CancellationToken ct)
    {
        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "Authentication required." }, ct);
            return;
        }

        if (!_tenantProvider.TryGetCurrentTenantId(out var tenantId))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "Tenant context missing." }, ct);
            return;
        }

        var updateRequest = new UpdateMarketingPreferencesRequest(
            req.Email,
            req.News,
            req.Offers,
            req.Surveys);

        var result = await _userProfileService.UpdateMarketingPreferencesAsync(userId, tenantId, updateRequest, ct);
        if (result == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(MapResponse(result), ct);
    }

    private static ApiMarketingPreferencesResponse MapResponse(
        Contracts.Models.Identity.MarketingPreferencesResponse prefs)
    {
        return new ApiMarketingPreferencesResponse(
            prefs.Email,
            prefs.News,
            prefs.Offers,
            prefs.Surveys);
    }
}
