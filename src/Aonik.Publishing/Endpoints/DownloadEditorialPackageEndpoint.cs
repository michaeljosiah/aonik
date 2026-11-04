using FastEndpoints;
using Microsoft.AspNetCore.Http;

using Aonik.Publishing.Contracts;

namespace Aonik.Publishing.Endpoints;

public sealed record EditorialPackagePath(Guid Id, Guid RevisionId);

internal sealed class DownloadEditorialPackageEndpoint(IEditorialReviewService reviews) : PublishingEndpoint<EditorialPackagePath, EmptyResponse>
{
    public override void Configure()
    {
        Get("/publishing/review/submissions/{Id:guid}/revisions/{RevisionId:guid}/package"); Policies("PublishingReviewPolicy"); Tags("Publishing review");
    }
    public override Task HandleAsync(EditorialPackagePath req, CancellationToken ct) => GuardedAsync(async () =>
    {
        var download = await reviews.DownloadAsync(req.Id, req.RevisionId, ct);
        HttpContext.Response.Headers.CacheControl = "private, no-store";
        HttpContext.Response.Headers.XContentTypeOptions = "nosniff";
        HttpContext.Response.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
        await Send.ResultAsync(Results.File(download.Content, "application/zip", download.FileName, enableRangeProcessing: false));
    });
}
