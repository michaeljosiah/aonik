using FastEndpoints;

using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Services.Identity;
using Aonik.SharedKernel.Abstractions;

using ApiCustomerPhotoDeleteResponse = Aonik.Api.Contracts.Identity.CustomerPhotoDeleteResponse;

namespace Aonik.Api.Endpoints.Identity;

public class DeleteCustomerPhotoEndpoint : EndpointWithoutRequest<ApiCustomerPhotoDeleteResponse>
{
    private readonly IUserProfileService _userProfileService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;

    public DeleteCustomerPhotoEndpoint(
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
        Delete("/profiles/customers/me/photo");
        Policies("UserPolicy");
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

        var result = await _userProfileService.DeleteCustomerPhotoAsync(userId, tenantId, ct);
        if (result == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(new ApiCustomerPhotoDeleteResponse(result.Status), ct);
    }
}
