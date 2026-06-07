using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using FastEndpoints;

using Aonik.Platform.Contracts.Services.Identity;
using Aonik.Platform.Contracts.Api.Identity;

namespace Aonik.Platform.Endpoints.Admin.Users;

internal class UploadUserPhotoEndpoint : EndpointWithoutRequest<CustomerPhotoUploadResponse>
{
    private readonly IAccessManagementService _accessManagementService;
    private readonly ILogger<UploadUserPhotoEndpoint> _logger;

    public UploadUserPhotoEndpoint(
        IAccessManagementService accessManagementService,
        ILogger<UploadUserPhotoEndpoint> logger)
    {
        _accessManagementService = accessManagementService;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("/admin/users/{userId}/photo");
        Policies("AdminUserPolicy");
        AllowFileUploads();
        Summary(s =>
        {
            s.Summary = "Upload user profile photo";
            s.Description = "Uploads a new profile photo for the specified user. Accepts JPEG, PNG, GIF, WebP, BMP, or SVG up to 5 MB.";
            s.Response(200, "Photo uploaded");
            s.Response(401, "Not authenticated");
            s.Response(404, "User not found");
            s.Response(422, "Invalid file");
        });
        Options(x => x.WithTags("User Administration"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = Route<Guid>("userId");

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

        const long maxFileSize = 5 * 1024 * 1024; // 5MB
        if (file.Length > maxFileSize)
        {
            HttpContext.Response.StatusCode = 422;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "Profile photo must be less than 5MB." }, ct);
            return;
        }

        // Validate image content type
        var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp", "image/bmp", "image/svg+xml" };
        var contentType = file.ContentType ?? "application/octet-stream";
        if (!allowedTypes.Contains(contentType.ToLowerInvariant()))
        {
            HttpContext.Response.StatusCode = 422;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "Profile photo must be an image file (JPG, PNG, GIF, WebP, BMP, or SVG)." }, ct);
            return;
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _accessManagementService.UploadUserPhotoAsync(
                userId,
                stream,
                file.FileName,
                contentType,
                ct);

            if (result == null)
            {
                await Send.NotFoundAsync(ct);
                return;
            }

            await Send.OkAsync(new CustomerPhotoUploadResponse(result.PhotoUrl), ct);
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
