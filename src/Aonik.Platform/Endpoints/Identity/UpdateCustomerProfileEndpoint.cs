using Microsoft.AspNetCore.Http;
using FastEndpoints;

using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Platform.Contracts.Models.Identity;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.SharedKernel.Abstractions;

using ApiUpdateCustomerProfileRequest = Aonik.Platform.Contracts.Api.Identity.UpdateCustomerProfileRequest;
using ApiCustomerProfileResponse = Aonik.Platform.Contracts.Api.Identity.CustomerProfileResponse;

namespace Aonik.Platform.Endpoints.Identity;

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
        Summary(s =>
        {
            s.Summary = "Update customer profile";
            s.Description = "Updates the current customer's profile details such as name, title, phone, and country.";
            s.Response(200, "Profile updated successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Customer not found");
            s.Response(422, "Validation error");
        });
        Options(x => x.WithTags("Identity"));
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
