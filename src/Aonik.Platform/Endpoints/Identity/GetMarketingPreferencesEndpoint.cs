using Microsoft.AspNetCore.Http;
using FastEndpoints;

using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.SharedKernel.Abstractions;

using ApiMarketingPreferencesResponse = Aonik.Platform.Contracts.Api.Identity.MarketingPreferencesResponse;

namespace Aonik.Platform.Endpoints.Identity;

public class GetMarketingPreferencesEndpoint : EndpointWithoutRequest<ApiMarketingPreferencesResponse>
{
    private readonly IUserProfileService _userProfileService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;

    public GetMarketingPreferencesEndpoint(
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
        Get("/profiles/customers/me/marketing");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "Get marketing preferences";
            s.Description = "Returns the current customer's marketing communication preferences for news, offers, and surveys.";
            s.Response(200, "Preferences returned");
            s.Response(401, "Not authenticated");
            s.Response(404, "Customer not found");
        });
        Options(x => x.WithTags("Identity"));
    }

    public override async Task HandleAsync(CancellationToken ct)
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

        var result = await _userProfileService.GetMarketingPreferencesAsync(userId, tenantId, ct);
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
