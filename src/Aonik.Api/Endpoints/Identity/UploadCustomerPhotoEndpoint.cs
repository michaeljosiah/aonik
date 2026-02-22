using FastEndpoints;

using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Application.Services.Identity;
using Aonik.SharedKernel.Abstractions;

using ApiCustomerPhotoUploadResponse = Aonik.Api.Contracts.Identity.CustomerPhotoUploadResponse;

namespace Aonik.Api.Endpoints.Identity;

public class UploadCustomerPhotoEndpoint : EndpointWithoutRequest<ApiCustomerPhotoUploadResponse>
{
    private readonly IUserProfileService _userProfileService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;

    public UploadCustomerPhotoEndpoint(
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
        Post("/profiles/customers/me/photo");
        Policies("AdminUserPolicy");
        AllowFileUploads();
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

        if (Files.Count == 0)
        {
            HttpContext.Response.StatusCode = 422;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "Profile photo is required." }, ct);
            return;
        }

        var file = Files[0];
        if (file.Length == 0)
        {
            HttpContext.Response.StatusCode = 422;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "Profile photo is empty." }, ct);
            return;
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _userProfileService.UploadCustomerPhotoAsync(
                userId,
                tenantId,
                stream,
                file.FileName,
                file.ContentType ?? "application/octet-stream",
                ct);

            if (result == null)
            {
                await Send.NotFoundAsync(ct);
                return;
            }

            await Send.OkAsync(new ApiCustomerPhotoUploadResponse(result.PhotoUrl), ct);
        }
        catch (ArgumentException ex)
        {
            HttpContext.Response.StatusCode = 422;
            await HttpContext.Response.WriteAsJsonAsync(new { error = ex.Message }, ct);
        }
    }
}
