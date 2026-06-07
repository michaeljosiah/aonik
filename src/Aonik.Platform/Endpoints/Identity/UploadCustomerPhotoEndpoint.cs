using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using FastEndpoints;

using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.SharedKernel.Abstractions;

using ApiCustomerPhotoUploadResponse = Aonik.Platform.Contracts.Api.Identity.CustomerPhotoUploadResponse;

namespace Aonik.Platform.Endpoints.Identity;

public class UploadCustomerPhotoEndpoint : EndpointWithoutRequest<ApiCustomerPhotoUploadResponse>
{
    private readonly IUserProfileService _userProfileService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<UploadCustomerPhotoEndpoint> _logger;

    public UploadCustomerPhotoEndpoint(
        IUserProfileService userProfileService,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        ILogger<UploadCustomerPhotoEndpoint> logger)
    {
        _userProfileService = userProfileService;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("/profiles/customers/me/photo");
        Policies("AdminUserPolicy");
        AllowFileUploads();
        Summary(s =>
        {
            s.Summary = "Upload customer profile photo";
            s.Description = "Uploads a new profile photo for the authenticated customer, replacing any existing photo.";
            s.Response(200, "Photo uploaded successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Customer not found");
            s.Response(422, "Invalid or missing photo file");
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
        catch (IOException ex)
        {
            _logger.LogError(ex, "Photo upload failed due to storage I/O error for user {UserId}", userId);
            HttpContext.Response.StatusCode = StatusCodes.Status502BadGateway;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "Unable to save photo — storage is temporarily unavailable." }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during photo upload for user {UserId}", userId);
            HttpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "An unexpected error occurred while saving your photo." }, ct);
        }
    }
}
