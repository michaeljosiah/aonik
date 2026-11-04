using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

using Aonik.Publishing.Contracts;
using Aonik.Publishing.Services;

namespace Aonik.Publishing.Endpoints;

internal sealed class PutPublicationUploadPartEndpoint(IPublicationIntakeService intake) : PublishingEndpointWithoutRequest<UploadReceipt>
{
    public override void Configure()
    {
        Put("/publishing/submissions/{Id:guid}/uploads/{UploadId:guid}/parts/{Part:int}"); Policies("UserPolicy"); Tags("Publishing");
        Options(builder => builder.Accepts<EmptyRequest>("application/octet-stream").WithMetadata(new RequestSizeLimitAttribute(32 * 1024 * 1024)));
    }
    public override Task HandleAsync(CancellationToken ct) => GuardedAsync(async () =>
    {
        if (HttpContext.Request.ContentLength is not { } length || length < 1 || length > 32L * 1024 * 1024)
            throw new PublishingException(413, "invalid-part-length", "A bounded Content-Length is required.");
        if (!string.Equals(HttpContext.Request.ContentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
            throw new PublishingException(415, "invalid-part-type", "Upload raw application/octet-stream bytes.");
        var size = HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (size is { IsReadOnly: false }) size.MaxRequestBodySize = 32 * 1024 * 1024;
        var hash = HttpContext.Request.Headers["X-Part-Sha256"].ToString();
        if (hash.Length != 64) throw new PublishingException(422, "invalid-part-digest", "X-Part-Sha256 is required.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        await Send.OkAsync(await intake.PutPartAsync(Route<Guid>("Id"), Route<Guid>("UploadId"), Route<int>("Part"), hash, length, HttpContext.Request.Body, timeout.Token), ct);
    });
}
