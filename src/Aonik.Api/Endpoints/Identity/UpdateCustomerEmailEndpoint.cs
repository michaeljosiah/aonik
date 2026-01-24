using FastEndpoints;

using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Services.Identity;
using Aonik.SharedKernel.Abstractions;

using ApiCustomerProfileResponse = Aonik.Api.Contracts.Identity.CustomerProfileResponse;
using ApiUpdateCustomerEmailRequest = Aonik.Api.Contracts.Identity.UpdateCustomerEmailRequest;

namespace Aonik.Api.Endpoints.Identity;

public class UpdateCustomerEmailEndpoint : Endpoint<ApiUpdateCustomerEmailRequest, ApiCustomerProfileResponse>
{
    private readonly IUserProfileService _userProfileService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;

    public UpdateCustomerEmailEndpoint(
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
        Put("/profiles/customers/me/email");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(ApiUpdateCustomerEmailRequest req, CancellationToken ct)
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
            var result = await _userProfileService.UpdateCustomerEmailAsync(
                userId,
                tenantId,
                new Aonik.Application.Models.Identity.UpdateCustomerEmailRequest(
                    req.CurrentEmail,
                    req.NewEmail,
                    req.Password),
                ct);

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

    private static ApiCustomerProfileResponse MapResponse(Aonik.Application.Models.Identity.CustomerProfileResponse profile)
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
