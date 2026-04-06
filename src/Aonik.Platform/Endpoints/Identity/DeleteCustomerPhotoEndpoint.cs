using Microsoft.AspNetCore.Http;
using FastEndpoints;

using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.SharedKernel.Abstractions;

using ApiCustomerPhotoDeleteResponse = Aonik.Platform.Contracts.Api.Identity.CustomerPhotoDeleteResponse;

namespace Aonik.Platform.Endpoints.Identity;

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
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "Delete customer profile photo";
            s.Description = "Removes the current customer's profile photo.";
            s.Response(200, "Photo deleted successfully");
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

        var result = await _userProfileService.DeleteCustomerPhotoAsync(userId, tenantId, ct);
        if (result == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(new ApiCustomerPhotoDeleteResponse(result.Status), ct);
    }
}
