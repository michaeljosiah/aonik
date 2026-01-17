using FastEndpoints;

using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Services.Identity;
using Aonik.SharedKernel.Abstractions;

using ApiUpdateCustomerPasswordRequest = Aonik.Api.Contracts.Identity.UpdateCustomerPasswordRequest;
using ApiUpdateCustomerPasswordResponse = Aonik.Api.Contracts.Identity.UpdateCustomerPasswordResponse;

namespace Aonik.Api.Endpoints.Identity;

public class UpdateCustomerPasswordEndpoint : Endpoint<ApiUpdateCustomerPasswordRequest, ApiUpdateCustomerPasswordResponse>
{
    private readonly IUserProfileService _userProfileService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;

    public UpdateCustomerPasswordEndpoint(
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
        Put("/profiles/customers/me/password");
        Policies("Users.Read");
    }

    public override async Task HandleAsync(ApiUpdateCustomerPasswordRequest req, CancellationToken ct)
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
            var result = await _userProfileService.UpdateCustomerPasswordAsync(
                userId,
                tenantId,
                new Aonik.Application.Models.Identity.UpdateCustomerPasswordRequest(
                    req.CurrentPassword,
                    req.NewPassword),
                ct);

            await Send.OkAsync(new ApiUpdateCustomerPasswordResponse(result.Status), ct);
        }
        catch (InvalidOperationException ex)
        {
            HttpContext.Response.StatusCode = 409;
            await HttpContext.Response.WriteAsJsonAsync(new { error = ex.Message }, ct);
        }
    }
}
