using Microsoft.AspNetCore.Http;
using FastEndpoints;

using Aonik.Platform.Contracts.Services.Identity;
using Aonik.Platform.Contracts.Api.Identity;

namespace Aonik.Platform.Endpoints.Admin.Users;

internal class UploadUserPhotoEndpoint : EndpointWithoutRequest<CustomerPhotoUploadResponse>
{
    private readonly IAccessManagementService _accessManagementService;

    public UploadUserPhotoEndpoint(IAccessManagementService accessManagementService)
    {
        _accessManagementService = accessManagementService;
    }

    public override void Configure()
    {
        Post("/admin/users/{userId}/photo");
        Policies("AdminUserPolicy");
        AllowFileUploads();
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
    }
}
