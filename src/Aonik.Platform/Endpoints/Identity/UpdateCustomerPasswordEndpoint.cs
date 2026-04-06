using Microsoft.AspNetCore.Http;
using FastEndpoints;

using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.SharedKernel.Abstractions;

using ApiUpdateCustomerPasswordRequest = Aonik.Platform.Contracts.Api.Identity.UpdateCustomerPasswordRequest;
using ApiUpdateCustomerPasswordResponse = Aonik.Platform.Contracts.Api.Identity.UpdateCustomerPasswordResponse;

namespace Aonik.Platform.Endpoints.Identity;

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
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "Update customer password";
            s.Description = "Changes the current customer's password after verifying the current password.";
            s.Response(200, "Password updated successfully");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Identity"));
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
                new Aonik.Platform.Contracts.Models.Identity.UpdateCustomerPasswordRequest(
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
