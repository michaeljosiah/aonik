using FastEndpoints;

using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Models.Identity;
using Aonik.Application.Services.Identity;
using Aonik.SharedKernel.Abstractions;

using ApiUpdateCustomerProfileRequest = Aonik.Api.Contracts.Identity.UpdateCustomerProfileRequest;
using ApiCustomerProfileResponse = Aonik.Api.Contracts.Identity.CustomerProfileResponse;

namespace Aonik.Api.Endpoints.Identity;

public class UpdateCustomerProfileEndpoint : Endpoint<ApiUpdateCustomerProfileRequest, ApiCustomerProfileResponse>
{
    private readonly IUserProfileService _userProfileService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;

    public UpdateCustomerProfileEndpoint(
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
        Put("/profiles/customers/me");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(ApiUpdateCustomerProfileRequest req, CancellationToken ct)
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

        try
        {
            var updateRequest = new UpdateCustomerProfileRequest(
                req.FirstName,
                req.LastName,
                req.Title,
                req.Phone,
                req.CountryCode);

            var result = await _userProfileService.UpdateCustomerProfileAsync(userId, tenantId, updateRequest, ct);

            if (result == null)
            {
                await Send.NotFoundAsync(ct);
                return;
            }

            await Send.OkAsync(MapResponse(result), ct);
        }
        catch (ArgumentException ex)
        {
            HttpContext.Response.StatusCode = 422;
            await HttpContext.Response.WriteAsJsonAsync(new { error = ex.Message }, ct);
        }
        catch (InvalidOperationException ex)
        {
            HttpContext.Response.StatusCode = 409;
            await HttpContext.Response.WriteAsJsonAsync(new { error = ex.Message }, ct);
        }
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
