using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Aonik.Commerce.Endpoints.Admin.Catalog;

public sealed class UploadProductImageEndpoint(IProductImageService images)
    : EndpointWithoutRequest<ProductImageUploadDto>
{
    public override void Configure()
    {
        Post("/commerce/admin/products/{productId:guid}/images");
        Policies("AdminWritePolicy");
        AllowFileUploads();
        Options(builder => builder.WithMetadata(
            new RequestSizeLimitAttribute(ProductImageService.MaxBytes + 65536),
            new RequestFormLimitsAttribute { MultipartBodyLengthLimit = ProductImageService.MaxBytes + 65536 }));
        Summary(s =>
        {
            s.Summary = "Upload a product image draft with alt text.";
            s.Description = "Send one JPEG/PNG file and altText. Save the returned URL and alt text through the ordered media replacement route.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!HttpContext.Request.HasFormContentType || Files.Count != 1 || Files[0].Name != "file")
            throw new StorefrontValidationException("Send one image in the file field and its altText.");
        if (Files[0].Length > ProductImageService.MaxBytes)
        {
            await Send.StatusCodeAsync(StatusCodes.Status413PayloadTooLarge, ct);
            return;
        }
        var altText = HttpContext.Request.Form["altText"];
        if (altText.Count != 1)
            throw new StorefrontValidationException("Send one altText value.");
        await using var content = Files[0].OpenReadStream();
        await Send.OkAsync(await images.UploadAsync(Route<Guid>("productId"), content, altText.ToString(), ct), ct);
    }
}
