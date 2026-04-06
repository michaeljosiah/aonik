using Microsoft.AspNetCore.Http;
using FastEndpoints;

using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.SharedKernel.Abstractions;

using ApiCustomerProfileResponse = Aonik.Platform.Contracts.Api.Identity.CustomerProfileResponse;
using ApiUpdateCustomerEmailRequest = Aonik.Platform.Contracts.Api.Identity.UpdateCustomerEmailRequest;

namespace Aonik.Platform.Endpoints.Identity;

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
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "Update customer email address";
            s.Description = "Changes the current customer's email address after verifying the provided password.";
            s.Response(200, "Email updated successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Customer not found");
            s.Response(422, "Validation error");
        });
        Options(x => x.WithTags("Identity"));
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
                new Aonik.Platform.Contracts.Models.Identity.UpdateCustomerEmailRequest(
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

    private static ApiCustomerProfileResponse MapResponse(Aonik.Platform.Contracts.Models.Identity.CustomerProfileResponse profile)
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
