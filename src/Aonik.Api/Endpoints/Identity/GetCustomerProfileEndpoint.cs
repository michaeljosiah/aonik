using FastEndpoints;

using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Models.Identity;
using Aonik.Application.Services.Identity;
using Aonik.SharedKernel.Abstractions;

using ApiCustomerProfileResponse = Aonik.Api.Contracts.Identity.CustomerProfileResponse;

namespace Aonik.Api.Endpoints.Identity;

public class GetCustomerProfileEndpoint : EndpointWithoutRequest<ApiCustomerProfileResponse>
{
    private readonly IUserProfileService _userProfileService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;

    public GetCustomerProfileEndpoint(
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
        Get("/profiles/customers/me");
        Policies("Users.Read");
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

        var result = await _userProfileService.GetCustomerProfileAsync(userId, tenantId, ct);
        if (result == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(MapResponse(result), ct);
    }

    private static ApiCustomerProfileResponse MapResponse(CustomerProfileResponse profile)
    {
        return new ApiCustomerProfileResponse(
            profile.PartyId,
            profile.UserId,
            profile.TenantId,
            profile.Email,
            profile.FirstName,
            profile.LastName,
            profile.Title,
            profile.Phone,
            profile.CountryCode,
            profile.PhotoUrl);
    }
}
