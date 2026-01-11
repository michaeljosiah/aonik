using FastEndpoints;
using Aonik.Api.Contracts.Identity;
using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Models.Identity;
using Aonik.Application.Services.Identity;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Api.Endpoints.Identity;

public class UpdateCustomerProfileEndpoint : Endpoint<UpdateCustomerProfileRequest, CustomerProfileResponse>
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
        Put("/v1/customers/me/profile");
        Policies("Users.Read");
    }

    public override async Task HandleAsync(UpdateCustomerProfileRequest req, CancellationToken ct)
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

        var updateRequest = MapRequest(req);
        var result = await _userProfileService.UpdateCustomerProfileAsync(userId, tenantId, updateRequest, ct);

        if (result == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(MapResponse(result), ct);
    }

    private static CustomerProfileUpdateRequest MapRequest(UpdateCustomerProfileRequest request)
    {
        CustomerAddress? address = null;
        if (request.Address != null)
        {
            address = new CustomerAddress(
                request.Address.Line1,
                request.Address.Line2,
                request.Address.Line3,
                request.Address.City,
                request.Address.State,
                request.Address.Postcode,
                request.Address.Country);
        }

        return new CustomerProfileUpdateRequest(
            request.DisplayName,
            request.Email,
            request.Phone,
            address);
    }

    private static CustomerProfileResponse MapResponse(CustomerProfile profile)
    {
        CustomerAddressResponse? address = null;
        if (profile.Address != null)
        {
            address = new CustomerAddressResponse(
                profile.Address.Line1,
                profile.Address.Line2,
                profile.Address.Line3,
                profile.Address.City,
                profile.Address.State,
                profile.Address.Postcode,
                profile.Address.Country);
        }

        return new CustomerProfileResponse(
            profile.PartyId,
            profile.DisplayName,
            profile.Email,
            profile.Phone,
            address);
    }
}
