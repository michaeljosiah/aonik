using System.Text;

using Aonik.Platform.Contracts.Models.ContactEnquiries;
using Aonik.Platform.Contracts.Services.ContactEnquiries;

using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace Aonik.Platform.Endpoints.ContactEnquiries;

public sealed class SubmitContactEnquiryEndpoint(IContactEnquiryService enquiries)
    : EndpointWithoutRequest<ContactEnquiryReceipt>
{
    public const string RateLimitPolicyName = "contact-enquiries";
    public const int MaxRequestBytes = 32 * 1024 * 1024;
    private const int MaxImageBytes = 10 * 1024 * 1024;
    private static readonly SemaphoreSlim UploadSlots = new(4);
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public override void Configure()
    {
        Post("/v1/contact-enquiries");
        AllowAnonymous();
        AllowFileUploads(dontAutoBindFormData: true);
        Options(builder => builder.RequireRateLimiting(RateLimitPolicyName)
            .WithMetadata(new RequestSizeLimitAttribute(MaxRequestBytes)));
        Summary(s => s.Summary = "Accept an enquiry and queue its notifications. Send multipart fields and up to three images.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!await UploadSlots.WaitAsync(0, ct))
        {
            HttpContext.Response.Headers.RetryAfter = "1";
            await Send.StatusCodeAsync(StatusCodes.Status429TooManyRequests, ct);
            return;
        }
        try
        {
            var size = HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (size is { IsReadOnly: false }) size.MaxRequestBodySize = MaxRequestBytes;
            if (HttpContext.Request.ContentLength > MaxRequestBytes)
                throw new BadHttpRequestException("The enquiry is too large.", StatusCodes.Status413PayloadTooLarge);
            var (fields, images) = await ReadMultipartAsync(ct);
            if (!string.IsNullOrWhiteSpace(fields.GetValueOrDefault("website")))
            {
                // A filled invisible field is rejected without persistence or email work.
                await Send.StatusCodeAsync(StatusCodes.Status202Accepted, ct);
                return;
            }
            if (!Guid.TryParse(fields.GetValueOrDefault("submission_id"), out var submissionId) || submissionId == Guid.Empty)
                throw Invalid("submission_id", "Send a valid submission reference and retain it for unchanged retries.");
            var result = await enquiries.SubmitAsync(new ContactEnquirySubmission(submissionId,
                fields.GetValueOrDefault("name") ?? "", fields.GetValueOrDefault("email") ?? "",
                fields.GetValueOrDefault("topic") ?? "", fields.GetValueOrDefault("order_number"),
                fields.GetValueOrDefault("message") ?? "", images), ct);
            await Send.ResultAsync(Results.Accepted(value: result));
        }
        catch (BadHttpRequestException error) when (error.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            await Send.StatusCodeAsync(StatusCodes.Status413PayloadTooLarge, ct);
        }
        catch (InvalidDataException)
        {
            throw Invalid("form", "Send a valid bounded multipart enquiry.");
        }
        catch (DecoderFallbackException)
        {
            throw Invalid("form", "Use UTF-8 text in the enquiry fields.");
        }
        finally { UploadSlots.Release(); }
    }

    private async Task<(Dictionary<string, string> Fields, List<ContactImageUpload> Images)> ReadMultipartAsync(CancellationToken ct)
    {
        if (!MediaTypeHeaderValue.TryParse(HttpContext.Request.ContentType, out var contentType)
            || !contentType.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            throw Invalid("form", "Send multipart form data.");
        var boundary = HeaderUtilities.RemoveQuotes(contentType.Boundary).Value;
        if (string.IsNullOrWhiteSpace(boundary) || boundary.Length > 128)
            throw Invalid("form", "Send a valid multipart boundary.");
        var reader = new MultipartReader(boundary, HttpContext.Request.Body)
        {
            HeadersCountLimit = 8, HeadersLengthLimit = 2048, BodyLengthLimit = MaxRequestBytes
        };
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        var images = new List<ContactImageUpload>();
        while (await reader.ReadNextSectionAsync(ct) is { } section)
        {
            if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition)
                || !disposition.DispositionType.Equals("form-data", StringComparison.OrdinalIgnoreCase))
                throw Invalid("form", "Every part must be form data.");
            var name = HeaderUtilities.RemoveQuotes(disposition.Name).Value ?? "";
            if (disposition.FileName.HasValue || disposition.FileNameStar.HasValue)
            {
                if (name != "images" || images.Count >= 3)
                    throw Invalid("images", "Send at most three files in the images field.");
                var fileName = HeaderUtilities.RemoveQuotes(disposition.FileNameStar.HasValue
                    ? disposition.FileNameStar : disposition.FileName).Value ?? "";
                images.Add(new ContactImageUpload(fileName, section.ContentType ?? "",
                    await ReadBoundedAsync(section.Body, MaxImageBytes, ct)));
            }
            else
            {
                if (name is not ("submission_id" or "name" or "email" or "topic" or "order_number" or "message" or "website")
                    || fields.ContainsKey(name))
                    throw Invalid("form", "Send each supported enquiry field once.");
                // Seven fixed field names, three measured files and bounded headers keep even
                // chunked requests below the aggregate cap without buffering arbitrary parts.
                fields.Add(name, Utf8.GetString(await ReadBoundedAsync(section.Body, 20000, ct)));
            }
        }
        return (fields, images);
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream stream, int limit, CancellationToken ct)
    {
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(buffer, ct)) > 0)
        {
            if (output.Length + read > limit)
                throw new BadHttpRequestException("An enquiry field or image is too large.", StatusCodes.Status413PayloadTooLarge);
            await output.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        return output.ToArray();
    }

    private static ContactEnquiryValidationException Invalid(string field, string message)
        => new(new Dictionary<string, string[]> { [field] = [message] });
}
